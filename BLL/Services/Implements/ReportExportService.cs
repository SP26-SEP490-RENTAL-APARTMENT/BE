using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BLL.Services.Implements;

public class ReportExportService : IReportExportService
{
    private enum ExportColumnKind
    {
        Index,
        Dimension,
        CurrentMetric,
        PreviousMetric,
        DeltaMetric,
        DeltaPercentMetric
    }

    private sealed record ExportColumn(ExportColumnKind Kind, string Header, string LookupKey);

    public Task<ReportExportContentDto> ExportAsync(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        CancellationToken cancellationToken = default)
    {
        var format = Normalize(request.Format);
        return format switch
        {
            "csv" => Task.FromResult(ExportCsv(reportName, request, report, comparison)),
            "excel" or "xlsx" => Task.FromResult(ExportExcel(reportName, request, report, comparison)),
            _ => throw new NotSupportedException("Unsupported export format. Use csv or excel (xlsx).")
        };
    }

    public async Task StreamExportAsync(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        var format = Normalize(request.Format);
            switch (format)
            {
                case "csv":
                    await StreamCsvAsync(request, report, comparison, output, cancellationToken).ConfigureAwait(false);
                    break;
                case "excel":
                case "xlsx":
                    // Excel streaming is not fully supported via OpenXML on non-seekable streams in this implementation.
                    // Fall back to buffered approach and write bytes to output to preserve behavior.
                    var content = ExportExcel(reportName, request, report, comparison).Content;
                    await output.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException("Unsupported export format for streaming. Use csv or excel (xlsx) for streaming.");
            }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ReportExportContentDto ExportCsv(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison)
    {
        // Keep existing behavior for callers that expect the full content
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, 1024, true);
        if (request.IncludeComparison)
        {
            if (comparison == null) throw new ArgumentException("Comparison data is required when IncludeComparison is true.");
            var headers = BuildComparisonColumns(request, comparison);
            writer.WriteLine(string.Join(",", headers.Select(column => EscapeCsv(column.Header))));
            for (var i = 0; i < comparison.Rows.Count; i++)
            {
                var values = BuildComparisonRowValues(comparison.Rows[i], headers, i + 1);
                writer.WriteLine(string.Join(",", values.Select(value => EscapeCsv(value))));
            }
        }
        else
        {
            if (report == null) throw new ArgumentException("Report data is required for non-comparison export.");
            var headers = BuildReportColumns(request, report);
            writer.WriteLine(string.Join(",", headers.Select(column => EscapeCsv(column.Header))));
            for (var i = 0; i < report.Rows.Count; i++)
            {
                var values = BuildReportRowValues(report.Rows[i], headers, i + 1);
                writer.WriteLine(string.Join(",", values.Select(value => EscapeCsv(value))));
            }
        }

        writer.Flush();
        return new ReportExportContentDto
        {
            Content = ms.ToArray(),
            ContentType = "text/csv",
            FileName = BuildFileName(request, reportName, "csv")
        };
    }

    private static async Task StreamCsvAsync(
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        Stream output,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, Encoding.UTF8, 8192, leaveOpen: true);

        if (request.IncludeComparison)
        {
            if (comparison == null) throw new ArgumentException("Comparison data is required when IncludeComparison is true.");
            var headers = BuildComparisonColumns(request, comparison);
            await writer.WriteLineAsync(string.Join(",", headers.Select(column => EscapeCsv(column.Header)))).ConfigureAwait(false);
            for (var i = 0; i < comparison.Rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = BuildComparisonRowValues(comparison.Rows[i], headers, i + 1);
                await writer.WriteLineAsync(string.Join(",", values.Select(value => EscapeCsv(value)))).ConfigureAwait(false);
            }
        }
        else
        {
            if (report == null) throw new ArgumentException("Report data is required for non-comparison export.");
            var headers = BuildReportColumns(request, report);
            await writer.WriteLineAsync(string.Join(",", headers.Select(column => EscapeCsv(column.Header)))).ConfigureAwait(false);
            for (var i = 0; i < report.Rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = BuildReportRowValues(report.Rows[i], headers, i + 1);
                await writer.WriteLineAsync(string.Join(",", values.Select(value => EscapeCsv(value)))).ConfigureAwait(false);
            }
        }

        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static ReportExportContentDto ExportExcel(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Report"
            };
            sheets.Append(sheet);

            List<string> headers;
            List<List<string>> rows;

            if (request.IncludeComparison)
            {
                if (comparison == null)
                {
                    throw new ArgumentException("Comparison data is required when IncludeComparison is true.");
                }

                headers = BuildComparisonColumns(request, comparison).Select(c => c.Header).ToList();
                rows = comparison.Rows.Select((ReportComparisonRowDto r, int index) =>
                    BuildComparisonRowValues(r, BuildComparisonColumns(request, comparison), index + 1)).ToList();
            }
            else
            {
                if (report == null)
                {
                    throw new ArgumentException("Report data is required for non-comparison export.");
                }

                headers = BuildReportColumns(request, report).Select(c => c.Header).ToList();
                rows = report.Rows.Select((ReportResultRowDto r, int index) =>
                    BuildReportRowValues(r, BuildReportColumns(request, report), index + 1)).ToList();
            }

            AppendRow(sheetData, headers);
            foreach (var rowValues in rows)
            {
                AppendRow(sheetData, rowValues);
            }

            workbookPart.Workbook.Save();
        }

        return new ReportExportContentDto
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = BuildFileName(request, reportName, "xlsx")
        };
    }

    private static ReportExportContentDto ExportPdf(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison)
    {
        var headers = request.IncludeComparison
            ? BuildComparisonColumns(request, comparison ?? throw new ArgumentException("Comparison data is required when IncludeComparison is true."))
            : BuildReportColumns(request, report ?? throw new ArgumentException("Report data is required for non-comparison export."));

        var rows = request.IncludeComparison
            ? comparison!.Rows.Select((ReportComparisonRowDto r, int index) =>
                BuildComparisonRowValues(r, headers, index + 1)).ToList()
            : report!.Rows.Select((ReportResultRowDto r, int index) =>
                BuildReportRowValues(r, headers, index + 1)).ToList();

        var documentDef = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(column =>
                {
                    column.Item().Text($"Report Export: {reportName}").Bold().FontSize(14);
                    column.Item().Text($"Generated at: {Common.Utils.VietnamTime.Now:yyyy-MM-dd HH:mm:ss}");
                    column.Item().PaddingVertical(8).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    column.Item().Text(string.Join(" | ", headers)).SemiBold();
                    foreach (var row in rows)
                    {
                        column.Item().Text(string.Join(" | ", row));
                    }
                });
            });
        });

        // Keep existing buffered API
        var pdfBytes = documentDef.GeneratePdf();
        return new ReportExportContentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(request, reportName, "pdf")
        };
    }

    private static async Task StreamPdfAsync(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        Stream output,
        CancellationToken cancellationToken)
    {
        var headers = request.IncludeComparison
            ? BuildComparisonColumns(request, comparison ?? throw new ArgumentException("Comparison data is required when IncludeComparison is true."))
            : BuildReportColumns(request, report ?? throw new ArgumentException("Report data is required for non-comparison export."));

        var rows = request.IncludeComparison
            ? comparison!.Rows.Select((ReportComparisonRowDto r, int index) =>
                BuildComparisonRowValues(r, headers, index + 1)).ToList()
            : report!.Rows.Select((ReportResultRowDto r, int index) =>
                BuildReportRowValues(r, headers, index + 1)).ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(column =>
                {
                    column.Item().Text($"Report Export: {reportName}").Bold().FontSize(14);
                    column.Item().Text($"Generated at: {Common.Utils.VietnamTime.Now:yyyy-MM-dd HH:mm:ss}");
                    column.Item().PaddingVertical(8).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    column.Item().Text(string.Join(" | ", headers)).SemiBold();
                    foreach (var row in rows)
                    {
                        column.Item().Text(string.Join(" | ", row));
                    }
                });
            });
        });

        // QuestPDF supports writing to stream via GeneratePdf(stream)
        var pdfBytes = doc.GeneratePdf();  // generates bytes synchronously in memory
        await output.WriteAsync(pdfBytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AppendRow(SheetData sheetData, IEnumerable<string> values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.String,
                CellValue = new CellValue(value ?? string.Empty)
            });
        }
        sheetData.Append(row);
    }

    private static List<ExportColumn> BuildReportColumns(ReportExportRequestDto request, ReportResultDto report)
    {
        var columns = new List<ExportColumn> { new(ExportColumnKind.Index, "#", "__index") };
        var first = report.Rows.FirstOrDefault();

        var dimensions = request.RunRequest?.Dimensions?.Count > 0
            ? request.RunRequest.Dimensions.Select(d => new ExportColumn(ExportColumnKind.Dimension, GetColumnHeader(d.Field, d.Alias), GetColumnKey(d.Field, d.Alias))).ToList()
            : first == null
                ? new List<ExportColumn>()
                : first.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Select(k => new ExportColumn(ExportColumnKind.Dimension, GetColumnHeader(k, null), k))
                    .ToList();

        var metrics = request.RunRequest?.Metrics?.Count > 0
            ? request.RunRequest.Metrics.Select(m =>
            {
                var key = string.IsNullOrWhiteSpace(m.Alias) ? BuildMetricKey(m.Field, m.Aggregation) : m.Alias!;
                return new ExportColumn(ExportColumnKind.CurrentMetric, GetColumnHeader(m.Field, m.Alias), key);
            }).ToList()
            : first == null
                ? new List<ExportColumn>()
                : first.Metrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Select(k => new ExportColumn(ExportColumnKind.CurrentMetric, GetColumnHeader(k, null), k))
                    .ToList();

        columns.AddRange(dimensions);
        columns.AddRange(metrics);
        return columns;
    }

    private static List<ExportColumn> BuildComparisonColumns(ReportExportRequestDto request, ReportComparisonResultDto comparison)
    {
        var columns = new List<ExportColumn> { new(ExportColumnKind.Index, "#", "__index") };
        var first = comparison.Rows.FirstOrDefault();

        var comparisonRunRequest = request.ComparisonRequest?.RunRequest ?? request.RunRequest;

        var dimensions = comparisonRunRequest?.Dimensions?.Count > 0
            ? comparisonRunRequest.Dimensions.Select(d => new ExportColumn(ExportColumnKind.Dimension, GetColumnHeader(d.Field, d.Alias), GetColumnKey(d.Field, d.Alias))).ToList()
            : first == null
                ? new List<ExportColumn>()
                : first.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Select(k => new ExportColumn(ExportColumnKind.Dimension, GetColumnHeader(k, null), k))
                    .ToList();

        var metrics = comparisonRunRequest?.Metrics?.Count > 0
            ? comparisonRunRequest.Metrics.Select(m =>
            {
                var key = string.IsNullOrWhiteSpace(m.Alias) ? BuildMetricKey(m.Field, m.Aggregation) : m.Alias!;
                return new ExportColumn(ExportColumnKind.CurrentMetric, GetColumnHeader(m.Field, m.Alias), key);
            }).ToList()
            : first == null
                ? new List<ExportColumn>()
                : first.CurrentMetrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .Select(k => new ExportColumn(ExportColumnKind.CurrentMetric, GetColumnHeader(k, null), k))
                    .ToList();

        columns.AddRange(dimensions);
        columns.AddRange(metrics.Select(m => m with { Kind = ExportColumnKind.CurrentMetric, Header = $"current_{m.Header}" }));
        columns.AddRange(metrics.Select(m => m with { Kind = ExportColumnKind.PreviousMetric, Header = $"previous_{m.Header}" }));
        columns.AddRange(metrics.Select(m => m with { Kind = ExportColumnKind.DeltaMetric, Header = $"delta_{m.Header}" }));
        columns.AddRange(metrics.Select(m => m with { Kind = ExportColumnKind.DeltaPercentMetric, Header = $"delta_pct_{m.Header}" }));
        return columns;
    }

    private static List<string> BuildReportRowValues(ReportResultRowDto row, IReadOnlyList<ExportColumn> columns, int rowNumber)
    {
        var values = new List<string>();
        foreach (var column in columns)
        {
            if (column.Kind == ExportColumnKind.Index)
            {
                values.Add(rowNumber.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (column.Kind == ExportColumnKind.Dimension)
            {
                values.Add(row.Dimensions.TryGetValue(column.LookupKey, out var v) ? FormatDimensionValue(v) : string.Empty);
                continue;
            }

            if (column.Kind == ExportColumnKind.CurrentMetric)
            {
                values.Add(row.Metrics.TryGetValue(column.LookupKey, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            values.Add(string.Empty);
        }

        return values;
    }

    private static List<string> BuildComparisonRowValues(ReportComparisonRowDto row, IReadOnlyList<ExportColumn> columns, int rowNumber)
    {
        var values = new List<string>();
        foreach (var column in columns)
        {
            if (column.Kind == ExportColumnKind.Index)
            {
                values.Add(rowNumber.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (column.Kind == ExportColumnKind.Dimension)
            {
                values.Add(row.Dimensions.TryGetValue(column.LookupKey, out var v) ? FormatDimensionValue(v) : string.Empty);
                continue;
            }

            if (column.Kind == ExportColumnKind.CurrentMetric)
            {
                values.Add(row.CurrentMetrics.TryGetValue(column.LookupKey, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (column.Kind == ExportColumnKind.PreviousMetric)
            {
                values.Add(row.PreviousMetrics.TryGetValue(column.LookupKey, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (column.Kind == ExportColumnKind.DeltaPercentMetric)
            {
                values.Add(row.DeltaPercentMetrics.TryGetValue(column.LookupKey, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (column.Kind == ExportColumnKind.DeltaMetric)
            {
                values.Add(row.DeltaMetrics.TryGetValue(column.LookupKey, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            values.Add(string.Empty);
        }

        return values;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string GetColumnKey(string field, string? alias)
        => string.IsNullOrWhiteSpace(alias) ? field : alias;

    private static string GetColumnHeader(string field, string? alias)
        => string.IsNullOrWhiteSpace(alias) ? field : alias;

    private static string BuildMetricKey(string field, string? aggregation)
    {
        var f = (field ?? string.Empty).Trim().ToLowerInvariant();
        var a = (aggregation ?? string.Empty).Trim().ToLowerInvariant();
        return a == "count" && f == "booking_count" ? f : $"{a}_{f}";
    }

    private static string FormatDimensionValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (value is string text)
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedOffset))
            {
                return parsedOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDateTime))
            {
                return parsedDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private static string BuildFileName(ReportExportRequestDto request, string reportName, string extension)
    {
        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            return request.FileName.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase)
                ? request.FileName
                : $"{request.FileName}.{extension}";
        }

        var sanitized = new string(reportName.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "report";
        }

        // If the run request includes an explicit date range, use it as a filename preset
        var runFrom = request.RunRequest?.From;
        var runTo = request.RunRequest?.To;
        if (runFrom.HasValue || runTo.HasValue)
        {
            var fromPart = runFrom.HasValue ? runFrom.Value.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) : "from-unknown";
            var toPart = runTo.HasValue ? runTo.Value.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) : "to-unknown";
            return $"{sanitized}_{fromPart}-{toPart}.{extension}";
        }

        // Fallback to timestamped filename
        return $"{sanitized}_{Common.Utils.VietnamTime.Now:yyyyMMddHHmmss}.{extension}";
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

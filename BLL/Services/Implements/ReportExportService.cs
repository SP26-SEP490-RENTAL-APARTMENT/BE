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
            "pdf" => Task.FromResult(ExportPdf(reportName, request, report, comparison)),
            _ => throw new NotSupportedException("Unsupported export format. Use csv, excel, or pdf.")
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
            case "pdf":
                await StreamPdfAsync(reportName, request, report, comparison, output, cancellationToken).ConfigureAwait(false);
                break;
            case "excel":
            case "xlsx":
                // Excel streaming is not fully supported via OpenXML on non-seekable streams in this implementation.
                // Fall back to buffered approach and write bytes to output to preserve behavior.
                var content = ExportExcel(reportName, request, report, comparison).Content;
                await output.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException("Unsupported export format for streaming. Use csv or pdf for streaming.");
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
            var headers = BuildComparisonHeaders(comparison);
            writer.WriteLine(string.Join(",", headers.Select(EscapeCsv)));
            foreach (var row in comparison.Rows)
            {
                var values = BuildComparisonRowValues(row, headers);
                writer.WriteLine(string.Join(",", values.Select(EscapeCsv)));
            }
        }
        else
        {
            if (report == null) throw new ArgumentException("Report data is required for non-comparison export.");
            var headers = BuildReportHeaders(report);
            writer.WriteLine(string.Join(",", headers.Select(EscapeCsv)));
            foreach (var row in report.Rows)
            {
                var values = BuildReportRowValues(row, headers);
                writer.WriteLine(string.Join(",", values.Select(EscapeCsv)));
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
            var headers = BuildComparisonHeaders(comparison);
            await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsv))).ConfigureAwait(false);
            foreach (var row in comparison.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = BuildComparisonRowValues(row, headers);
                await writer.WriteLineAsync(string.Join(",", values.Select(EscapeCsv))).ConfigureAwait(false);
            }
        }
        else
        {
            if (report == null) throw new ArgumentException("Report data is required for non-comparison export.");
            var headers = BuildReportHeaders(report);
            await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsv))).ConfigureAwait(false);
            foreach (var row in report.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = BuildReportRowValues(row, headers);
                await writer.WriteLineAsync(string.Join(",", values.Select(EscapeCsv))).ConfigureAwait(false);
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

                headers = BuildComparisonHeaders(comparison);
                rows = comparison.Rows.Select(r => BuildComparisonRowValues(r, headers)).ToList();
            }
            else
            {
                if (report == null)
                {
                    throw new ArgumentException("Report data is required for non-comparison export.");
                }

                headers = BuildReportHeaders(report);
                rows = report.Rows.Select(r => BuildReportRowValues(r, headers)).ToList();
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
            ? BuildComparisonHeaders(comparison ?? throw new ArgumentException("Comparison data is required when IncludeComparison is true."))
            : BuildReportHeaders(report ?? throw new ArgumentException("Report data is required for non-comparison export."));

        var rows = request.IncludeComparison
            ? comparison!.Rows.Select(r => BuildComparisonRowValues(r, headers)).ToList()
            : report!.Rows.Select(r => BuildReportRowValues(r, headers)).ToList();

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
            ? BuildComparisonHeaders(comparison ?? throw new ArgumentException("Comparison data is required when IncludeComparison is true."))
            : BuildReportHeaders(report ?? throw new ArgumentException("Report data is required for non-comparison export."));

        var rows = request.IncludeComparison
            ? comparison!.Rows.Select(r => BuildComparisonRowValues(r, headers)).ToList()
            : report!.Rows.Select(r => BuildReportRowValues(r, headers)).ToList();

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
        doc.GeneratePdf(output);
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

    private static List<string> BuildReportHeaders(ReportResultDto report)
    {
        var first = report.Rows.FirstOrDefault();
        if (first == null)
        {
            return ["No data"];
        }

        var headers = new List<string>();
        headers.AddRange(first.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"dim_{k}"));
        headers.AddRange(first.Metrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"metric_{k}"));
        return headers;
    }

    private static List<string> BuildComparisonHeaders(ReportComparisonResultDto comparison)
    {
        var first = comparison.Rows.FirstOrDefault();
        if (first == null)
        {
            return ["No data"];
        }

        var headers = new List<string>();
        headers.AddRange(first.Dimensions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"dim_{k}"));
        headers.AddRange(first.CurrentMetrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"current_{k}"));
        headers.AddRange(first.PreviousMetrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"previous_{k}"));
        headers.AddRange(first.DeltaMetrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"delta_{k}"));
        headers.AddRange(first.DeltaPercentMetrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(k => $"delta_pct_{k}"));
        return headers;
    }

    private static List<string> BuildReportRowValues(ReportResultRowDto row, IEnumerable<string> headers)
    {
        var values = new List<string>();
        foreach (var header in headers)
        {
            if (header.StartsWith("dim_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[4..];
                values.Add(row.Dimensions.TryGetValue(key, out var v) ? (v?.ToString() ?? string.Empty) : string.Empty);
                continue;
            }

            if (header.StartsWith("metric_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[7..];
                values.Add(row.Metrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            values.Add(string.Empty);
        }

        return values;
    }

    private static List<string> BuildComparisonRowValues(ReportComparisonRowDto row, IEnumerable<string> headers)
    {
        var values = new List<string>();
        foreach (var header in headers)
        {
            if (header.StartsWith("dim_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[4..];
                values.Add(row.Dimensions.TryGetValue(key, out var v) ? (v?.ToString() ?? string.Empty) : string.Empty);
                continue;
            }

            if (header.StartsWith("current_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[8..];
                values.Add(row.CurrentMetrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (header.StartsWith("previous_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[9..];
                values.Add(row.PreviousMetrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (header.StartsWith("delta_pct_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[10..];
                values.Add(row.DeltaPercentMetrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
                continue;
            }

            if (header.StartsWith("delta_", StringComparison.OrdinalIgnoreCase))
            {
                var key = header[6..];
                values.Add(row.DeltaMetrics.TryGetValue(key, out var v) ? v.ToString(CultureInfo.InvariantCulture) : string.Empty);
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

        return $"{sanitized}_{Common.Utils.VietnamTime.Now:yyyyMMddHHmmss}.{extension}";
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

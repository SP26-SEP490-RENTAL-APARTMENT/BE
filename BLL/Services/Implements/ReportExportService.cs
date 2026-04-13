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

    private static ReportExportContentDto ExportCsv(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison)
    {
        var sb = new StringBuilder();

        if (request.IncludeComparison)
        {
            if (comparison == null)
            {
                throw new ArgumentException("Comparison data is required when IncludeComparison is true.");
            }

            var headers = BuildComparisonHeaders(comparison);
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

            foreach (var row in comparison.Rows)
            {
                var values = BuildComparisonRowValues(row, headers);
                sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }
        }
        else
        {
            if (report == null)
            {
                throw new ArgumentException("Report data is required for non-comparison export.");
            }

            var headers = BuildReportHeaders(report);
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

            foreach (var row in report.Rows)
            {
                var values = BuildReportRowValues(row, headers);
                sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }
        }

        return new ReportExportContentDto
        {
            Content = Encoding.UTF8.GetBytes(sb.ToString()),
            ContentType = "text/csv",
            FileName = BuildFileName(request, reportName, "csv")
        };
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

        var pdfBytes = Document.Create(container =>
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
        }).GeneratePdf();

        return new ReportExportContentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(request, reportName, "pdf")
        };
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

using System.Threading;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IReportExportService
{
    Task<ReportExportContentDto> ExportAsync(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the export content directly into the provided output stream. Implementations should write
    /// the appropriate content-type bytes and flush as they stream. Supports CSV and PDF streaming; Excel
    /// may still be buffered depending on OpenXML capabilities.
    /// </summary>
    Task StreamExportAsync(
        string reportName,
        ReportExportRequestDto request,
        ReportResultDto? report,
        ReportComparisonResultDto? comparison,
        Stream output,
        CancellationToken cancellationToken = default);
}

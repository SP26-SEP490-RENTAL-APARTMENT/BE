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
}

using System;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IReportExecutionService
{
    Task<ReportResultDto> RunReportAsync(Guid reportId, ReportRunRequestDto request, Guid requestedByUserId);
    Task<ReportComparisonResultDto> CompareReportAsync(Guid reportId, ReportComparisonRequestDto request, Guid requestedByUserId);
}

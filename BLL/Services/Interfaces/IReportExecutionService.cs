using System;
using System.Threading.Tasks;
using Common.DTOs;

namespace BLL.Services.Interfaces;

public interface IReportExecutionService
{
    Task<ReportResultDto> RunReportAsync(Guid reportId, ReportRunRequestDto request, Guid requestedByUserId, Guid? landlordId = null);
    Task<ReportComparisonResultDto> CompareReportAsync(Guid reportId, ReportComparisonRequestDto request, Guid requestedByUserId, Guid? landlordId = null);
    Task<ReportResultPageDto> RunReportPageAsync(Guid reportId, ReportRunRequestDto request, int page, int pageSize, Guid requestedByUserId, Guid? landlordId = null);
}

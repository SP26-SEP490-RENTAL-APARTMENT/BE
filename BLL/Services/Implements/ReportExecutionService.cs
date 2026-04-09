using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ReportExecutionService : IReportExecutionService
{
    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IRepository<GeneratedReport> _generatedReportRepository;
    private readonly IRepository<Booking> _bookingRepository;

    public ReportExecutionService(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IRepository<GeneratedReport> generatedReportRepository,
        IRepository<Booking> bookingRepository)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _generatedReportRepository = generatedReportRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<ReportResultDto> RunReportAsync(Guid reportId, ReportRunRequestDto request, Guid requestedByUserId)
    {
        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId);
        if (definition == null)
        {
            throw new ArgumentException("Report definition not found.");
        }

        // For initial implementation, support a simple standard report:
        // daily booking counts within optional date range.
        var from = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
        var to = request.To ?? Common.Utils.VietnamTime.Now;

        var normalizedFrom = DateOnly.FromDateTime(from.Date);
        var normalizedTo = DateOnly.FromDateTime(to.Date.AddDays(1));

        var bookings = await _bookingRepository.FindAsync(b =>
            b.CreatedAt.HasValue &&
            b.CreatedAt.Value >= normalizedFrom.ToDateTime(TimeOnly.MinValue) &&
            b.CreatedAt.Value < normalizedTo.ToDateTime(TimeOnly.MinValue));

        var grouped = bookings
            .GroupBy(b => DateOnly.FromDateTime(b.CreatedAt!.Value.Date))
            .OrderBy(g => g.Key)
            .Select(g => new ReportResultRowDto
            {
                Dimensions = new Dictionary<string, object?>
                {
                    ["date"] = g.Key.ToDateTime(TimeOnly.MinValue)
                },
                Metrics = new Dictionary<string, decimal>
                {
                    ["booking_count"] = g.Count()
                }
            })
            .ToList();

        var result = new ReportResultDto
        {
            ReportId = reportId,
            Name = definition.Name,
            Rows = grouped
        };

        // Persist a simple GeneratedReport entry with summary only for now.
        var generated = new GeneratedReport
        {
            GeneratedReportId = Guid.NewGuid(),
            ReportId = reportId,
            RequestedBy = requestedByUserId,
            RequestedAt = Common.Utils.VietnamTime.Now,
            Status = "completed",
            ResultSummaryJson = $"{{\"rowCount\":{grouped.Count}}}",
            ResultJson = null,
            RetentionUntil = Common.Utils.VietnamTime.Now.AddDays(30)
        };

        await _generatedReportRepository.AddAsync(generated);
        await _generatedReportRepository.SaveChangesAsync();

        return result;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ReportExecutionService : IReportExecutionService
{
    private static readonly HashSet<string> AllowedDimensionFields =
    [
        "date",
        "status",
        "payment_mode",
        "apartment_id",
        "apartment_name",
        "tenant_id",
        "tenant_name",
        "nights"
    ];

    private static readonly HashSet<string> AllowedMetricFields =
    [
        "booking_count",
        "total_revenue",
        "avg_booking_value",
        "min_booking_value",
        "max_booking_value",
        "paid_booking_count",
        "unique_tenant_count",
        "unique_apartment_count",
        "unique_paid_tenant_count",
        "occupancy_percent",
        "adr",
        "avg_length_of_stay",
        "review_avg_rating",
        "review_count",
        "package_revenue",
        "package_count",
        "subscription_active_count",
        "subscription_revenue",
        "subscription_churn_count",
        "subscription_churn_rate",
        "avg_sold_price",
        "avg_base_price",
        "avg_price_delta"
    ];

    private static readonly HashSet<string> AllowedAggregations =
    [
        "count",
        "sum",
        "avg",
        "min",
        "max",
        "distinct_count",
        "p50",
        "p90",
        "p95"
    ];

    private static readonly HashSet<string> AllowedOperators =
    [
        "eq",
        "ne",
        "gt",
        "gte",
        "lt",
        "lte",
        "in",
        "between",
        "contains"
    ];

    private readonly IRepository<ReportDefinition> _reportDefinitionRepository;
    private readonly IRepository<GeneratedReport> _generatedReportRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<Apartment> _apartmentRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Package> _packageRepository;
    private readonly IRepository<LandlordSubscription> _subscriptionRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<ApartmentPriceCalendar> _priceCalendarRepository;
    private readonly IRepository<ApartmentAvailability> _availabilityRepository;
    private readonly IRepository<SmartPricingHistory> _smartPricingRepository;

    public ReportExecutionService(
        IRepository<ReportDefinition> reportDefinitionRepository,
        IRepository<GeneratedReport> generatedReportRepository,
        IBookingRepository bookingRepository,
        IRepository<Apartment> apartmentRepository,
        IRepository<User> userRepository,
        IRepository<Review> reviewRepository,
        IRepository<Package> packageRepository,
        IRepository<LandlordSubscription> subscriptionRepository,
        IRepository<Payment> paymentRepository,
        IRepository<ApartmentPriceCalendar> priceCalendarRepository,
        IRepository<ApartmentAvailability> availabilityRepository,
        IRepository<SmartPricingHistory> smartPricingRepository)
    {
        _reportDefinitionRepository = reportDefinitionRepository;
        _generatedReportRepository = generatedReportRepository;
        _bookingRepository = bookingRepository;
        _apartmentRepository = apartmentRepository;
        _userRepository = userRepository;
        _reviewRepository = reviewRepository;
        _packageRepository = packageRepository;
        _subscriptionRepository = subscriptionRepository;
        _paymentRepository = paymentRepository;
        _priceCalendarRepository = priceCalendarRepository;
        _availabilityRepository = availabilityRepository;
        _smartPricingRepository = smartPricingRepository;
    }

    public async Task<ReportResultDto> RunReportAsync(Guid reportId, ReportRunRequestDto request, Guid requestedByUserId, Guid? landlordId = null)
    {
        request ??= new ReportRunRequestDto();

        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId)
            ?? throw new ArgumentException("Report definition not found.");

        return await BuildReportAsync(definition, reportId, request, requestedByUserId, persistGenerated: true, landlordId);
    }

    public async Task<ReportComparisonResultDto> CompareReportAsync(Guid reportId, ReportComparisonRequestDto request, Guid requestedByUserId, Guid? landlordId = null)
    {
        request ??= new ReportComparisonRequestDto();
        request.RunRequest ??= new ReportRunRequestDto();

        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId)
            ?? throw new ArgumentException("Report definition not found.");

        var currentRequest = request.RunRequest;
    var previousRequest = request.PreviousRunRequest ?? BuildPreviousRequest(currentRequest, request.Mode);

        var currentResult = await BuildReportAsync(definition, reportId, currentRequest, requestedByUserId, persistGenerated: false, landlordId);
        var previousResult = await BuildReportAsync(definition, reportId, previousRequest, requestedByUserId, persistGenerated: false, landlordId);

        var currentMap = currentResult.Rows.ToDictionary(BuildDimensionKeyFromRow, StringComparer.OrdinalIgnoreCase);
        var previousMap = previousResult.Rows.ToDictionary(BuildDimensionKeyFromRow, StringComparer.OrdinalIgnoreCase);
        var keys = currentMap.Keys.Union(previousMap.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        var rows = new List<ReportComparisonRowDto>(keys.Count);
        foreach (var key in keys)
        {
            currentMap.TryGetValue(key, out var currentRow);
            previousMap.TryGetValue(key, out var previousRow);

            var dimensions = currentRow?.Dimensions ?? previousRow?.Dimensions ?? new Dictionary<string, object?>();
            var currentMetrics = currentRow?.Metrics ?? new Dictionary<string, decimal>();
            var previousMetrics = previousRow?.Metrics ?? new Dictionary<string, decimal>();

            var metricNames = currentMetrics.Keys.Union(previousMetrics.Keys, StringComparer.OrdinalIgnoreCase);
            var deltaMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var deltaPercentMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var metricName in metricNames)
            {
                var currentValue = currentMetrics.TryGetValue(metricName, out var cv) ? cv : 0m;
                var previousValue = previousMetrics.TryGetValue(metricName, out var pv) ? pv : 0m;
                var delta = currentValue - previousValue;
                var deltaPercent = previousValue == 0m ? 0m : Math.Round((delta / previousValue) * 100m, 2, MidpointRounding.AwayFromZero);

                deltaMetrics[metricName] = delta;
                deltaPercentMetrics[metricName] = deltaPercent;
            }

            rows.Add(new ReportComparisonRowDto
            {
                Dimensions = new Dictionary<string, object?>(dimensions),
                CurrentMetrics = new Dictionary<string, decimal>(currentMetrics),
                PreviousMetrics = new Dictionary<string, decimal>(previousMetrics),
                DeltaMetrics = deltaMetrics,
                DeltaPercentMetrics = deltaPercentMetrics
            });
        }

        // Calculate total metrics across all rows
        var totalCurrentMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var totalPreviousMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var totalDeltaMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var totalDeltaPercentMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (rows.Count > 0)
        {
            var firstRow = rows[0];
            
            // Sum all current metrics
            foreach (var metricKey in firstRow.CurrentMetrics.Keys)
            {
                totalCurrentMetrics[metricKey] = rows.Sum(r => r.CurrentMetrics.TryGetValue(metricKey, out var v) ? v : 0m);
            }
            
            // Sum all previous metrics
            foreach (var metricKey in firstRow.PreviousMetrics.Keys)
            {
                totalPreviousMetrics[metricKey] = rows.Sum(r => r.PreviousMetrics.TryGetValue(metricKey, out var v) ? v : 0m);
            }
            
            // Calculate total deltas
            foreach (var metricKey in firstRow.DeltaMetrics.Keys)
            {
                var totalCurrent = totalCurrentMetrics.TryGetValue(metricKey, out var tc) ? tc : 0m;
                var totalPrevious = totalPreviousMetrics.TryGetValue(metricKey, out var tp) ? tp : 0m;
                var delta = totalCurrent - totalPrevious;
                var deltaPercent = totalPrevious == 0m ? 0m : Math.Round((delta / totalPrevious) * 100m, 2, MidpointRounding.AwayFromZero);
                
                totalDeltaMetrics[metricKey] = delta;
                totalDeltaPercentMetrics[metricKey] = deltaPercent;
            }
        }

        var result = new ReportComparisonResultDto
        {
            ReportId = reportId,
            Name = definition.Name,
            Mode = string.IsNullOrWhiteSpace(request.Mode) ? "custom" : NormalizeKey(request.Mode),
            CurrentFrom = currentRequest.From,
            CurrentTo = currentRequest.To,
            PreviousFrom = previousRequest.From,
            PreviousTo = previousRequest.To,
            Rows = rows,
            TotalCurrentMetrics = totalCurrentMetrics,
            TotalPreviousMetrics = totalPreviousMetrics,
            TotalDeltaMetrics = totalDeltaMetrics,
            TotalDeltaPercentMetrics = totalDeltaPercentMetrics
        };

        await SaveGeneratedReportAsync(
            reportId,
            requestedByUserId,
            JsonSerializer.Serialize(result),
            $"{{\"rowCount\":{rows.Count},\"mode\":\"{result.Mode}\"}}");

        return result;
    }

    public async Task<ReportResultPageDto> RunReportPageAsync(Guid reportId, ReportRunRequestDto request, int page, int pageSize, Guid requestedByUserId, Guid? landlordId = null)
    {
        request ??= new ReportRunRequestDto();

        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId)
            ?? throw new ArgumentException("Report definition not found.");

        // Prefer DB-grouped paging when the request only uses the supported booking report subset.
        var dimensions = ResolveDimensions(request);
        var metrics = ResolveMetrics(request);
        var filters = request.Filters ?? Array.Empty<ReportFilterRequestDto>();

        if (CanUseDbGroupedBookingQuery(dimensions, metrics, filters))
        {
            var groupedFrom = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
            var groupedTo = request.To ?? Common.Utils.VietnamTime.Now;

            var (rows, groupedTotalCount) = await _bookingRepository.GetPagedGroupedReportRowsAsync(
                groupedFrom,
                groupedTo.AddDays(1),
                dimensions,
                metrics,
                request.SearchTerm,
                page,
                pageSize,
                landlordId);

            return new ReportResultPageDto
            {
                ReportId = reportId,
                Name = definition.Name,
                Rows = rows.ToList(),
                TotalCount = groupedTotalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // reuse BuildReportAsync logic but materialize only the requested page of rows
        var from = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
        var to = request.To ?? Common.Utils.VietnamTime.Now;

        var normalizedFrom = DateOnly.FromDateTime(from.Date);
        var normalizedTo = DateOnly.FromDateTime(to.Date.AddDays(1));

        // Use repository-level paging to iterate bookings without loading all into memory at once.
        var bookingPage = 1;
        var bookingPageSize = 5000; // internal page size for scanning bookings
        var allBookings = new List<Booking>();
        while (true)
        {
            var (pageItems, total) = landlordId.HasValue
                ? await _bookingRepository.GetByLandlordAsync(
                    landlordId.Value,
                    bookingPage,
                    bookingPageSize,
                    sortBy: null,
                    sortOrder: null,
                    search: null,
                    fromDate: normalizedFrom.ToDateTime(TimeOnly.MinValue),
                    toDate: normalizedTo.ToDateTime(TimeOnly.MinValue))
                : await _bookingRepository.FindPagedAsync(b =>
                    b.CreatedAt.HasValue &&
                    b.CreatedAt.Value >= normalizedFrom.ToDateTime(TimeOnly.MinValue) &&
                    b.CreatedAt.Value < normalizedTo.ToDateTime(TimeOnly.MinValue),
                    bookingPage,
                    bookingPageSize,
                    sortBy: null,
                    sortOrder: null);

            if (pageItems == null || !pageItems.Any()) break;
            allBookings.AddRange(pageItems);
            if (allBookings.Count >= total) break;
            bookingPage++;
        }

        var lookupContext = await BuildDimensionLookupContextAsync(filteredBookings: allBookings, dimensions: dimensions);
        var auxiliaryContext = await BuildAuxiliaryDataContextAsync(filteredBookings: allBookings, dimensions: dimensions);

        ValidateFilters(filters, dimensions, metrics);

        var filteredBookings = allBookings
            .Where(b => MatchAllDimensionFilters(b, filters, dimensions, lookupContext))
            .ToList();

        var groupedQuery = filteredBookings
            .GroupBy(b => BuildDimensionKey(b, dimensions, lookupContext))
            .Select(g => BuildRow(g.ToList(), dimensions, metrics, lookupContext, auxiliaryContext, normalizedFrom, normalizedTo))
            .Where(r => MatchAllMetricFilters(r, filters, metrics));

        var ordered = groupedQuery.OrderBy(r => r.Dimensions.TryGetValue(dimensions[0].Alias ?? dimensions[0].Field, out var v) ? v?.ToString() : string.Empty);

        var totalCount = ordered.Count();
        var skip = Math.Max(0, (page - 1) * pageSize);
        var pageRows = ordered.Skip(skip).Take(pageSize).ToList();

        var result = new ReportResultPageDto
        {
            ReportId = reportId,
            Name = definition.Name,
            Rows = pageRows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        // Do not persist paginated requests as generated reports (persisting full sets happens in RunReportAsync)
        return result;
    }

    private async Task<ReportResultDto> BuildReportAsync(
        ReportDefinition definition,
        Guid reportId,
        ReportRunRequestDto request,
        Guid requestedByUserId,
        bool persistGenerated,
        Guid? landlordId = null)
    {
        // Prefer DB-grouped paging for supported booking metrics so we don't pull all bookings into memory.
        var dimensions = ResolveDimensions(request);
        var metrics = ResolveMetrics(request);
        var filters = request.Filters ?? Array.Empty<ReportFilterRequestDto>();

        if (CanUseDbGroupedBookingQuery(dimensions, metrics, filters))
        {
            var groupedFrom = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
            var groupedTo = request.To ?? Common.Utils.VietnamTime.Now;

            var page = 1;
            var pageSize = 5000;
            var groupedRows = new List<ReportResultRowDto>();
            int groupedTotalCount;

            do
            {
                var (rows, count) = await _bookingRepository.GetPagedGroupedReportRowsAsync(
                        groupedFrom,
                        groupedTo.AddDays(1),
                    dimensions,
                    metrics,
                    request.SearchTerm,
                    page,
                    pageSize,
                    landlordId);

                groupedTotalCount = count;
                groupedRows.AddRange(rows);
                page++;
            }
            while (groupedRows.Count < groupedTotalCount);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                groupedRows = groupedRows
                    .Where(r => r.Dimensions.Values.Any(v =>
                        string.Equals(v?.ToString(), request.SearchTerm, StringComparison.OrdinalIgnoreCase)
                        || (v?.ToString()?.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false)))
                    .ToList();
            }

            var groupedPrimaryDimensionKey = dimensions[0].Alias ?? dimensions[0].Field;
            groupedRows = groupedRows
                .OrderBy(r => r.Dimensions.TryGetValue(groupedPrimaryDimensionKey, out var value) ? value?.ToString() : string.Empty)
                .ToList();

            var groupedTotalMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (groupedRows.Count > 0)
            {
                var firstRow = groupedRows[0];
                foreach (var metricKey in firstRow.Metrics.Keys)
            {
                    groupedTotalMetrics[metricKey] = groupedRows.Sum(r => r.Metrics.TryGetValue(metricKey, out var v) ? v : 0m);
            }
            }

            var groupedResult = new ReportResultDto
            {
                ReportId = reportId,
                Name = definition.Name,
                Rows = groupedRows,
                TotalMetrics = groupedTotalMetrics
            };

            if (persistGenerated)
            {
                await SaveGeneratedReportAsync(
                    reportId,
                    requestedByUserId,
                    JsonSerializer.Serialize(groupedResult),
                    $"{{\"rowCount\":{groupedRows.Count}}}");
            }

            return groupedResult;
        }

        // For initial implementation, support booking-based analytics with configurable dimensions/metrics.
            var from = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
            var to = request.To ?? Common.Utils.VietnamTime.Now;

        var normalizedFrom = DateOnly.FromDateTime(from.Date);
        var normalizedTo = DateOnly.FromDateTime(to.Date.AddDays(1));

        var bookings = await LoadBookingsAsync(normalizedFrom, normalizedTo, landlordId);

        var lookupContext = await BuildDimensionLookupContextAsync(filteredBookings: bookings, dimensions: dimensions);
        var auxiliaryContext = await BuildAuxiliaryDataContextAsync(filteredBookings: bookings, dimensions: dimensions);

        ValidateFilters(filters, dimensions, metrics);

        var filteredBookings = bookings
            .Where(b => MatchAllDimensionFilters(b, filters, dimensions, lookupContext))
            .ToList();

        var grouped = filteredBookings
            .GroupBy(b => BuildDimensionKey(b, dimensions, lookupContext))
            .Select(g => BuildRow(g.ToList(), dimensions, metrics, lookupContext, auxiliaryContext, normalizedFrom, normalizedTo))
            .Where(r => MatchAllMetricFilters(r, filters, metrics))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            grouped = grouped
                .Where(r => r.Dimensions.Values.Any(v =>
                    string.Equals(v?.ToString(), request.SearchTerm, StringComparison.OrdinalIgnoreCase)
                    || (v?.ToString()?.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false)))
                .ToList();
        }

        var primaryDimensionKey = dimensions[0].Alias ?? dimensions[0].Field;
        grouped = grouped
            .OrderBy(r => r.Dimensions.TryGetValue(primaryDimensionKey, out var value) ? value?.ToString() : string.Empty)
            .ToList();

        // Calculate total metrics across all rows
        var totalMetrics = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (grouped.Count > 0)
        {
            var firstRow = grouped[0];
            foreach (var metricKey in firstRow.Metrics.Keys)
            {
                totalMetrics[metricKey] = grouped.Sum(r => r.Metrics.TryGetValue(metricKey, out var v) ? v : 0m);
            }
        }

        var result = new ReportResultDto
        {
            ReportId = reportId,
            Name = definition.Name,
            Rows = grouped,
            TotalMetrics = totalMetrics
        };

        if (persistGenerated)
        {
            await SaveGeneratedReportAsync(
                reportId,
                requestedByUserId,
                JsonSerializer.Serialize(result),
                $"{{\"rowCount\":{grouped.Count}}}");
        }

        return result;
    }

    private static bool CanUseDbGroupedBookingQuery(
        IReadOnlyList<ReportDimensionRequestDto> dimensions,
        IReadOnlyList<ReportMetricRequestDto> metrics,
        IReadOnlyList<ReportFilterRequestDto> filters)
    {
        if (filters.Count > 0)
        {
            return false;
        }

        foreach (var dimension in dimensions)
        {
            var field = NormalizeKey(dimension.Field);
            if (field is not ("date" or "status" or "payment_mode" or "apartment_id" or "apartment_name" or "tenant_id" or "tenant_name" or "nights"))
            {
                return false;
            }
        }

            foreach (var metric in metrics)
            {
                var field = NormalizeKey(metric.Field);
                if (field is not (
                    "booking_count" or "total_revenue" or "avg_booking_value" or "min_booking_value" or "max_booking_value" or
                    "paid_booking_count" or "unique_tenant_count" or "unique_apartment_count" or "unique_paid_tenant_count" or
                    "avg_length_of_stay" or "package_revenue" or "package_count" or "adr" or "avg_sold_price" or
                    "occupancy_percent" or
                    "review_avg_rating" or "review_count" or "avg_base_price" or "avg_price_delta"))
                {
                    return false;
                }
            }

        return true;
    }

    private async Task SaveGeneratedReportAsync(
        Guid reportId,
        Guid requestedByUserId,
        string resultJson,
        string resultSummaryJson)
    {
        var generated = new GeneratedReport
        {
            GeneratedReportId = Guid.NewGuid(),
            ReportId = reportId,
            RequestedBy = requestedByUserId,
            RequestedAt = Common.Utils.VietnamTime.Now,
            Status = "completed",
            ResultSummaryJson = resultSummaryJson,
            ResultJson = resultJson,
            RetentionUntil = Common.Utils.VietnamTime.Now.AddDays(30)
        };

        await _generatedReportRepository.AddAsync(generated);
        await _generatedReportRepository.SaveChangesAsync();
    }

    private static ReportRunRequestDto BuildPreviousRequest(ReportRunRequestDto current, string? mode)
    {
        var normalizedMode = NormalizeKey(mode);
        var currentFrom = current.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
        var currentTo = current.To ?? Common.Utils.VietnamTime.Now;

        var previous = new ReportRunRequestDto
        {
            SearchTerm = current.SearchTerm,
            Dimensions = current.Dimensions,
            Metrics = current.Metrics,
            Filters = current.Filters
        };

        if (normalizedMode == "wow")
        {
            previous.From = currentFrom.AddDays(-7);
            previous.To = currentTo.AddDays(-7);
            return previous;
        }

        if (normalizedMode == "mom")
        {
            previous.From = currentFrom.AddMonths(-1);
            previous.To = currentTo.AddMonths(-1);
            return previous;
        }

        if (normalizedMode == "yoy")
        {
            previous.From = currentFrom.AddYears(-1);
            previous.To = currentTo.AddYears(-1);
            return previous;
        }

        var duration = currentTo - currentFrom;
        previous.To = currentFrom;
        previous.From = currentFrom - duration;
        return previous;
    }

    private static string BuildDimensionKeyFromRow(ReportResultRowDto row)
    {
        return string.Join("|", row.Dimensions
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(k => $"{NormalizeKey(k.Key)}:{k.Value}"));
    }

    public static ReportSchemaDto GetDefaultSchema()
    {
        return new ReportSchemaDto
        {
            Dimensions = AllowedDimensionFields.OrderBy(x => x).ToList(),
            MetricFields = AllowedMetricFields.OrderBy(x => x).ToList(),
            Aggregations = AllowedAggregations.OrderBy(x => x).ToList(),
            Operators = AllowedOperators.OrderBy(x => x).ToList()
        };
    }

    private static IReadOnlyList<ReportDimensionRequestDto> ResolveDimensions(ReportRunRequestDto request)
    {
        var dimensions = request.Dimensions?.Any() == true
            ? request.Dimensions
            : [new ReportDimensionRequestDto { Field = "date", Alias = "date" }];

        var result = new List<ReportDimensionRequestDto>();
        foreach (var dim in dimensions)
        {
            var field = NormalizeKey(dim.Field);
            if (!AllowedDimensionFields.Contains(field))
            {
                throw new ArgumentException($"Unsupported dimension field '{dim.Field}'.");
            }

            result.Add(new ReportDimensionRequestDto
            {
                Field = field,
                Alias = string.IsNullOrWhiteSpace(dim.Alias) ? field : NormalizeKey(dim.Alias)
            });
        }

        return result;
    }

    private static IReadOnlyList<ReportMetricRequestDto> ResolveMetrics(ReportRunRequestDto request)
    {
        var metrics = request.Metrics?.Any() == true
            ? request.Metrics
            : [new ReportMetricRequestDto { Field = "booking_count", Aggregation = "count", Alias = "booking_count" }];

        var result = new List<ReportMetricRequestDto>();
        foreach (var metric in metrics)
        {
            var field = NormalizeKey(metric.Field);
            var aggregation = string.IsNullOrWhiteSpace(metric.Aggregation) ? "count" : NormalizeKey(metric.Aggregation);

            if (!AllowedMetricFields.Contains(field))
            {
                throw new ArgumentException($"Unsupported metric field '{metric.Field}'.");
            }

            if (!AllowedAggregations.Contains(aggregation))
            {
                throw new ArgumentException($"Unsupported aggregation '{metric.Aggregation}'.");
            }

            if ((field == "booking_count"
                || field == "paid_booking_count"
                || field == "unique_tenant_count"
                || field == "unique_apartment_count"
                || field == "unique_paid_tenant_count")
                && aggregation is not ("count" or "distinct_count"))
            {
                throw new ArgumentException($"Aggregation '{aggregation}' is not compatible with metric '{field}'.");
            }

            result.Add(new ReportMetricRequestDto
            {
                Field = field,
                Aggregation = aggregation,
                Alias = string.IsNullOrWhiteSpace(metric.Alias)
                    ? BuildMetricAlias(field, aggregation)
                    : NormalizeKey(metric.Alias)
            });
        }

        return result;
    }

    private static void ValidateFilters(
        IReadOnlyList<ReportFilterRequestDto> filters,
        IReadOnlyList<ReportDimensionRequestDto> dimensions,
        IReadOnlyList<ReportMetricRequestDto> metrics)
    {
        var availableDimensionAliases = dimensions.Select(d => d.Alias ?? d.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableMetricAliases = metrics.Select(m => m.Alias ?? BuildMetricAlias(m.Field, m.Aggregation)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            var target = string.IsNullOrWhiteSpace(filter.Target) ? "dimension" : NormalizeKey(filter.Target);
            var op = NormalizeKey(filter.Operator);
            var field = NormalizeKey(filter.Field);

            if (!AllowedOperators.Contains(op))
            {
                throw new ArgumentException($"Unsupported operator '{filter.Operator}'.");
            }

            var exists = target == "metric"
                ? availableMetricAliases.Contains(field)
                : availableDimensionAliases.Contains(field);

            if (!exists)
            {
                throw new ArgumentException($"Filter field '{filter.Field}' not found in selected {target}s.");
            }

            if (op is "in" or "between")
            {
                if (filter.Values == null || !filter.Values.Any())
                {
                    throw new ArgumentException($"Operator '{filter.Operator}' requires a non-empty values array.");
                }
            }
            else if (filter.Value == null && (filter.Values == null || !filter.Values.Any()))
            {
                throw new ArgumentException($"Filter '{filter.Field}' requires value or values.");
            }
        }
    }

    private static bool MatchAllDimensionFilters(
        Booking booking,
        IReadOnlyList<ReportFilterRequestDto> filters,
        IReadOnlyList<ReportDimensionRequestDto> dimensions,
        DimensionLookupContext lookupContext)
    {
        var dimensionMap = dimensions.ToDictionary(
            d => NormalizeKey(d.Alias ?? d.Field),
            d => GetDimensionValue(booking, d.Field, lookupContext),
            StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            var target = string.IsNullOrWhiteSpace(filter.Target) ? "dimension" : NormalizeKey(filter.Target);
            if (target == "metric")
            {
                continue;
            }

            var key = NormalizeKey(filter.Field);
            if (!dimensionMap.TryGetValue(key, out var value))
            {
                return false;
            }

            if (!Evaluate(value, filter))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchAllMetricFilters(
        ReportResultRowDto row,
        IReadOnlyList<ReportFilterRequestDto> filters,
        IReadOnlyList<ReportMetricRequestDto> metrics)
    {
        var metricAliases = metrics.Select(m => m.Alias ?? BuildMetricAlias(m.Field, m.Aggregation)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            var target = string.IsNullOrWhiteSpace(filter.Target) ? "dimension" : NormalizeKey(filter.Target);
            if (target != "metric")
            {
                continue;
            }

            var key = NormalizeKey(filter.Field);
            if (!metricAliases.Contains(key))
            {
                return false;
            }

            if (!row.Metrics.TryGetValue(key, out var metricValue))
            {
                return false;
            }

            if (!Evaluate(metricValue, filter))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildDimensionKey(Booking booking, IReadOnlyList<ReportDimensionRequestDto> dimensions, DimensionLookupContext lookupContext)
    {
        return string.Join("|", dimensions.Select(d => GetDimensionValue(booking, d.Field, lookupContext)?.ToString() ?? "null"));
    }

    private ReportResultRowDto BuildRow(
        IReadOnlyList<Booking> bookingGroup,
        IReadOnlyList<ReportDimensionRequestDto> dimensions,
        IReadOnlyList<ReportMetricRequestDto> metrics,
        DimensionLookupContext lookupContext,
        AuxiliaryDataContext auxiliaryContext,
        DateOnly periodFrom,
        DateOnly periodTo)
    {
        var first = bookingGroup[0];
        var row = new ReportResultRowDto();

        foreach (var dim in dimensions)
        {
            row.Dimensions[dim.Alias ?? dim.Field] = GetDimensionValue(first, dim.Field, lookupContext);
        }

        foreach (var metric in metrics)
        {
            row.Metrics[metric.Alias ?? BuildMetricAlias(metric.Field, metric.Aggregation)] =
                CalculateMetric(bookingGroup, metric.Field, metric.Aggregation, auxiliaryContext, periodFrom, periodTo);
        }

        return row;
    }

    private static object? GetDimensionValue(Booking booking, string dimensionField, DimensionLookupContext lookupContext)
    {
        return NormalizeKey(dimensionField) switch
        {
            "date" => booking.CreatedAt?.Date,
            "status" => booking.Status,
            "payment_mode" => booking.PaymentMode,
            "apartment_id" => booking.ApartmentId,
            "apartment_name" => lookupContext.GetApartmentName(booking.ApartmentId),
            "tenant_id" => booking.TenantId,
            "tenant_name" => lookupContext.GetTenantName(booking.TenantId),
            "nights" => booking.Nights,
            _ => throw new ArgumentException($"Unsupported dimension field '{dimensionField}'.")
        };
    }

    private async Task<DimensionLookupContext> BuildDimensionLookupContextAsync(
        IEnumerable<Booking> filteredBookings,
        IReadOnlyList<ReportDimensionRequestDto> dimensions)
    {
        var needsApartmentName = dimensions.Any(d => NormalizeKey(d.Field) == "apartment_name");
        var needsTenantName = dimensions.Any(d => NormalizeKey(d.Field) == "tenant_name");

        var apartmentNames = new Dictionary<Guid, string>();
        var tenantNames = new Dictionary<Guid, string>();

        if (needsApartmentName)
        {
            var apartmentIds = filteredBookings.Select(b => b.ApartmentId).Distinct().ToList();
            if (apartmentIds.Count > 0)
            {
                var apartments = await _apartmentRepository.FindAsync(a => apartmentIds.Contains(a.ApartmentId));
                apartmentNames = apartments
                    .GroupBy(a => a.ApartmentId)
                    .ToDictionary(g => g.Key, g => g.First().Title ?? string.Empty);
            }
        }

        if (needsTenantName)
        {
            var tenantIds = filteredBookings.Select(b => b.TenantId).Distinct().ToList();
            if (tenantIds.Count > 0)
            {
                var users = await _userRepository.FindAsync(u => tenantIds.Contains(u.UserId));
                tenantNames = users
                    .GroupBy(u => u.UserId)
                    .ToDictionary(
                        g => g.Key,
                        g => string.IsNullOrWhiteSpace(g.First().FullName)
                            ? (g.First().Email ?? g.Key.ToString())
                            : g.First().FullName!);
            }
        }

        return new DimensionLookupContext(apartmentNames, tenantNames);
    }

    private async Task<AuxiliaryDataContext> BuildAuxiliaryDataContextAsync(
        IEnumerable<Booking> filteredBookings,
        IReadOnlyList<ReportDimensionRequestDto> dimensions)
    {
        var apartmentIds = filteredBookings.Select(b => b.ApartmentId).Distinct().ToList();

        var apartments = apartmentIds.Count > 0
            ? (await _apartmentRepository.FindAsync(a => apartmentIds.Contains(a.ApartmentId))).ToList()
            : new List<Apartment>();

        var landlordIds = apartments.Select(a => a.LandlordId).Distinct().ToList();

        var reviews = apartmentIds.Count > 0
            ? (await _reviewRepository.FindAsync(r => r.ApartmentId != null && apartmentIds.Contains(r.ApartmentId.Value))).ToList()
            : new List<Review>();

        var packages = apartmentIds.Count > 0
            ? (await _packageRepository.FindAsync(p => apartmentIds.Contains(p.ApartmentId))).ToList()
            : new List<Package>();

        var priceCalendars = apartmentIds.Count > 0
            ? (await _priceCalendarRepository.FindAsync(pc => apartmentIds.Contains(pc.ApartmentId))).ToList()
            : new List<ApartmentPriceCalendar>();

        var availabilities = apartmentIds.Count > 0
            ? (await _availabilityRepository.FindAsync(av => apartmentIds.Contains(av.ApartmentId))).ToList()
            : new List<ApartmentAvailability>();

        var pricingHistories = apartmentIds.Count > 0
            ? (await _smartPricingRepository.FindAsync(sp => apartmentIds.Contains(sp.ApartmentId))).ToList()
            : new List<SmartPricingHistory>();

        var payments = landlordIds.Count > 0
            ? (await _paymentRepository.FindAsync(p => p.LandlordId.HasValue && landlordIds.Contains(p.LandlordId.Value))).ToList()
            : new List<Payment>();

        var subscriptions = landlordIds.Count > 0
            ? (await _subscriptionRepository.FindAsync(s => landlordIds.Contains(s.LandlordId))).ToList()
            : new List<LandlordSubscription>();

        var reviewsByApartment = reviews.GroupBy(r => r.ApartmentId ?? Guid.Empty).ToDictionary(g => g.Key, g => g.ToList());
        var packagesByApartment = packages.GroupBy(p => p.ApartmentId).ToDictionary(g => g.Key, g => g.ToList());
        var priceCalendarsByApartment = priceCalendars.GroupBy(pc => pc.ApartmentId).ToDictionary(g => g.Key, g => g.ToList());
        var availabilitiesByApartment = availabilities.GroupBy(a => a.ApartmentId).ToDictionary(g => g.Key, g => g.ToList());
        var pricingHistoriesByApartment = pricingHistories.GroupBy(s => s.ApartmentId).ToDictionary(g => g.Key, g => g.ToList());
        var subscriptionsByLandlord = subscriptions.GroupBy(s => s.LandlordId).ToDictionary(g => g.Key, g => g.ToList());
        var paymentsByLandlord = payments.GroupBy(p => p.LandlordId ?? Guid.Empty).ToDictionary(g => g.Key, g => g.ToList());
        var apartmentsById = apartments.GroupBy(a => a.ApartmentId).ToDictionary(g => g.Key, g => g.First());
        return new AuxiliaryDataContext(
            reviewsByApartment,
            packagesByApartment,
            priceCalendarsByApartment,
            availabilitiesByApartment,
            pricingHistoriesByApartment,
            subscriptionsByLandlord,
            paymentsByLandlord,
            apartmentsById);
    }

    private async Task<List<Booking>> LoadBookingsAsync(
        DateOnly normalizedFrom,
        DateOnly normalizedTo,
        Guid? landlordId)
    {
        if (landlordId.HasValue)
        {
            var bookingPage = 1;
            var bookingPageSize = 5000;
            var bookings = new List<Booking>();

            while (true)
            {
                var (pageItems, total) = await _bookingRepository.GetByLandlordAsync(
                    landlordId.Value,
                    bookingPage,
                    bookingPageSize,
                    sortBy: null,
                    sortOrder: null,
                    search: null,
                    fromDate: normalizedFrom.ToDateTime(TimeOnly.MinValue),
                    toDate: normalizedTo.ToDateTime(TimeOnly.MinValue));

                if (pageItems == null || !pageItems.Any())
                {
                    break;
                }

                bookings.AddRange(pageItems);
                if (bookings.Count >= total)
                {
                    break;
                }

                bookingPage++;
            }

            return bookings;
        }

        var allBookings = await _bookingRepository.FindAsync(b =>
            b.CreatedAt.HasValue &&
            b.CreatedAt.Value >= normalizedFrom.ToDateTime(TimeOnly.MinValue) &&
            b.CreatedAt.Value < normalizedTo.ToDateTime(TimeOnly.MinValue));

        return allBookings.ToList();
    }

    private sealed class AuxiliaryDataContext
    {
        public AuxiliaryDataContext(
            IReadOnlyDictionary<Guid, List<Review>> reviewsByApartment,
            IReadOnlyDictionary<Guid, List<Package>> packagesByApartment,
            IReadOnlyDictionary<Guid, List<ApartmentPriceCalendar>> priceCalendarsByApartment,
            IReadOnlyDictionary<Guid, List<ApartmentAvailability>> availabilitiesByApartment,
            IReadOnlyDictionary<Guid, List<SmartPricingHistory>> pricingHistoriesByApartment,
            IReadOnlyDictionary<Guid, List<LandlordSubscription>> subscriptionsByLandlord,
            IReadOnlyDictionary<Guid, List<Payment>> paymentsByLandlord,
            IReadOnlyDictionary<Guid, Apartment> apartmentsById)
        {
            ReviewsByApartment = reviewsByApartment;
            PackagesByApartment = packagesByApartment;
            PriceCalendarsByApartment = priceCalendarsByApartment;
            AvailabilitiesByApartment = availabilitiesByApartment;
            PricingHistoriesByApartment = pricingHistoriesByApartment;
            SubscriptionsByLandlord = subscriptionsByLandlord;
            PaymentsByLandlord = paymentsByLandlord;
            ApartmentsById = apartmentsById;
        }

        public IReadOnlyDictionary<Guid, List<Review>> ReviewsByApartment { get; }
        public IReadOnlyDictionary<Guid, List<Package>> PackagesByApartment { get; }
        public IReadOnlyDictionary<Guid, List<ApartmentPriceCalendar>> PriceCalendarsByApartment { get; }
        public IReadOnlyDictionary<Guid, List<ApartmentAvailability>> AvailabilitiesByApartment { get; }
        public IReadOnlyDictionary<Guid, List<SmartPricingHistory>> PricingHistoriesByApartment { get; }
        public IReadOnlyDictionary<Guid, List<LandlordSubscription>> SubscriptionsByLandlord { get; }
        public IReadOnlyDictionary<Guid, List<Payment>> PaymentsByLandlord { get; }
        public IReadOnlyDictionary<Guid, Apartment> ApartmentsById { get; }
    }

    private sealed class DimensionLookupContext
    {
        private readonly IReadOnlyDictionary<Guid, string> _apartmentNames;
        private readonly IReadOnlyDictionary<Guid, string> _tenantNames;

        public DimensionLookupContext(
            IReadOnlyDictionary<Guid, string> apartmentNames,
            IReadOnlyDictionary<Guid, string> tenantNames)
        {
            _apartmentNames = apartmentNames;
            _tenantNames = tenantNames;
        }

        public string GetApartmentName(Guid apartmentId)
        {
            return _apartmentNames.TryGetValue(apartmentId, out var value)
                ? value
                : apartmentId.ToString();
        }

        public string GetTenantName(Guid tenantId)
        {
            return _tenantNames.TryGetValue(tenantId, out var value)
                ? value
                : tenantId.ToString();
        }
    }

    private decimal CalculateMetric(IReadOnlyList<Booking> bookings, string metricField, string aggregation, AuxiliaryDataContext auxiliaryContext, DateOnly periodFrom, DateOnly periodTo)
    {
        var normalizedMetric = NormalizeKey(metricField);
        var normalizedAggregation = NormalizeKey(aggregation);

        // helper values
        var apartmentIds = bookings.Select(b => b.ApartmentId).Distinct().ToList();
        var firstApartmentId = apartmentIds.Count > 0 ? apartmentIds[0] : Guid.Empty;
        var totalNights = bookings.Sum(b => b.Nights);
        var totalRevenue = bookings.Sum(b => b.TotalPrice);

        DateTime periodFromDt = periodFrom.ToDateTime(TimeOnly.MinValue);
        DateTime periodToDt = periodTo.ToDateTime(TimeOnly.MinValue);

        switch (normalizedMetric)
        {
            case "booking_count":
                return bookings.Count;
            case "total_revenue":
                return Aggregate(bookings.Select(b => b.TotalPrice), normalizedAggregation);
            case "avg_booking_value":
                return Aggregate(bookings.Select(b => b.TotalPrice), "avg");
            case "min_booking_value":
                return Aggregate(bookings.Select(b => b.TotalPrice), "min");
            case "max_booking_value":
                return Aggregate(bookings.Select(b => b.TotalPrice), "max");
            case "paid_booking_count":
                return bookings.Count(b => string.Equals(b.Status, "paid", StringComparison.OrdinalIgnoreCase)
                                               || string.Equals(b.Status, "completed", StringComparison.OrdinalIgnoreCase));
            case "unique_tenant_count":
                return bookings.Select(b => b.TenantId).Distinct().Count();
            case "unique_apartment_count":
                return bookings.Select(b => b.ApartmentId).Distinct().Count();
            case "unique_paid_tenant_count":
                return bookings
                    .Where(b => string.Equals(b.Status, "paid", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(b.Status, "completed", StringComparison.OrdinalIgnoreCase))
                    .Select(b => b.TenantId)
                    .Distinct()
                    .Count();
            case "occupancy_percent":
            {
                var bookedNights = totalNights;
                var totalAvailableRoomNights = 0;
                var totalDays = (int)(periodToDt - periodFromDt).TotalDays;
                if (totalDays <= 0) return 0m;

                foreach (var aptId in apartmentIds)
                {
                    var blockedDays = 0;
                    if (auxiliaryContext.AvailabilitiesByApartment.TryGetValue(aptId, out var avails))
                    {
                        var blockedDates = new HashSet<DateOnly>();
                        foreach (var av in avails)
                        {
                            var overlapStart = av.StartDate > periodFrom ? av.StartDate : periodFrom;
                            var overlapEnd = av.EndDate < periodTo ? av.EndDate : periodTo;

                            if (overlapEnd < overlapStart)
                            {
                                continue;
                            }

                            var cursor = overlapStart;
                            while (cursor <= overlapEnd)
                            {
                                blockedDates.Add(cursor);
                                cursor = cursor.AddDays(1);
                            }
                        }

                        blockedDays = blockedDates.Count;
                    }

                    var availableDays = Math.Max(0, totalDays - blockedDays);
                    totalAvailableRoomNights += availableDays;
                }

                return totalAvailableRoomNights == 0
                    ? 0m
                    : Math.Min(100m, Math.Round((decimal)bookedNights / totalAvailableRoomNights * 100m, 2));
            }
            case "adr":
            {
                var nights = totalNights;
                return nights == 0 ? 0m : Math.Round(totalRevenue / nights, 2, MidpointRounding.AwayFromZero);
            }
            case "avg_length_of_stay":
                return bookings.Count == 0 ? 0m : Math.Round((decimal)bookings.Average(b => b.Nights), 2);
            case "review_avg_rating":
            {
                var allReviews = new List<Review>();
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ReviewsByApartment.TryGetValue(aptId, out var revs) && revs.Count > 0)
                    {
                        allReviews.AddRange(revs.Where(r => r.CreatedAt.HasValue && r.CreatedAt.Value >= periodFromDt && r.CreatedAt.Value < periodToDt));
                    }
                }
                if (allReviews.Count == 0) return 0m;
                var avg = allReviews.Average(r => r.Rating ?? 0);
                return Math.Round((decimal)avg, 2);
            }
            case "review_count":
            {
                var cnt = 0;
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ReviewsByApartment.TryGetValue(aptId, out var rr))
                    {
                        cnt += rr.Count(r => r.CreatedAt.HasValue && r.CreatedAt.Value >= periodFromDt && r.CreatedAt.Value < periodToDt);
                    }
                }
                return cnt;
            }
            case "package_revenue":
                return bookings.Sum(b => b.PackagePrice ?? 0m);
            case "package_count":
                return bookings.Count(b => b.PackageId != null);
            case "subscription_active_count":
            {
                var landlordSet = new HashSet<Guid>();
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ApartmentsById.TryGetValue(aptId, out var apt)) landlordSet.Add(apt.LandlordId);
                }
                var total = 0;
                foreach (var landlordId in landlordSet)
                {
                    if (auxiliaryContext.SubscriptionsByLandlord.TryGetValue(landlordId, out var subs)) total += subs.Count;
                }
                return total;
            }
            case "subscription_revenue":
            {
                var landlordSet = new HashSet<Guid>();
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ApartmentsById.TryGetValue(aptId, out var apt)) landlordSet.Add(apt.LandlordId);
                }

                decimal total = 0m;
                foreach (var landlordId in landlordSet)
                {
                    if (!auxiliaryContext.PaymentsByLandlord.TryGetValue(landlordId, out var landlordPayments))
                    {
                        continue;
                    }

                    total += landlordPayments.Count == 0
                        ? 0m
                        : landlordPayments
                            .Where(p =>
                                string.Equals(p.RelatedEntityType, "host_subscription", StringComparison.OrdinalIgnoreCase) &&
                                p.PaidAt.HasValue &&
                                p.PaidAt.Value >= periodFromDt &&
                                p.PaidAt.Value < periodToDt &&
                                string.Equals(p.Status, "success", StringComparison.OrdinalIgnoreCase))
                            .Sum(p => p.Amount);
                }

                return total;
            }
            case "subscription_churn_count":
            {
                var landlordSet = new HashSet<Guid>();
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ApartmentsById.TryGetValue(aptId, out var apt)) landlordSet.Add(apt.LandlordId);
                }

                var churnCount = 0;
                foreach (var landlordId in landlordSet)
                {
                    if (!auxiliaryContext.SubscriptionsByLandlord.TryGetValue(landlordId, out var subs))
                    {
                        continue;
                    }

                    churnCount += subs.Count(s =>
                        (string.Equals(s.Status, "expired", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase)) &&
                        s.EndDate >= periodFrom && s.EndDate < periodTo);
                }

                return churnCount;
            }
            case "subscription_churn_rate":
            {
                var landlordSet = new HashSet<Guid>();
                foreach (var aptId in apartmentIds)
                {
                    if (auxiliaryContext.ApartmentsById.TryGetValue(aptId, out var apt)) landlordSet.Add(apt.LandlordId);
                }

                var activeBase = 0;
                var churnCount = 0;
                foreach (var landlordId in landlordSet)
                {
                    if (!auxiliaryContext.SubscriptionsByLandlord.TryGetValue(landlordId, out var subs))
                    {
                        continue;
                    }

                    activeBase += subs.Count(s =>
                        string.Equals(s.Status, "active", StringComparison.OrdinalIgnoreCase) &&
                        s.StartDate <= periodFrom &&
                        (!s.EndDate.HasValue || s.EndDate.Value >= periodFrom));

                    churnCount += subs.Count(s =>
                        (string.Equals(s.Status, "expired", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase)) &&
                        s.EndDate >= periodFrom && s.EndDate < periodTo);
                }

                return activeBase == 0 ? 0m : Math.Round((decimal)churnCount / activeBase * 100m, 2);
            }
            case "avg_sold_price":
            {
                var nights = totalNights;
                return nights == 0 ? 0m : Math.Round(totalRevenue / nights, 2, MidpointRounding.AwayFromZero);
            }
            case "avg_base_price":
            {
                // Compute weighted average base price across apartments using booking nights as weight
                var aptNights = bookings.GroupBy(b => b.ApartmentId).ToDictionary(g => g.Key, g => g.Sum(b => b.Nights));
                var weightedPrices = new List<(decimal price, int nights)>();

                foreach (var aptId in apartmentIds)
                {
                    decimal? aptBase = null;
                    // smart pricing history
                    if (auxiliaryContext.PricingHistoriesByApartment.TryGetValue(aptId, out var phs) && phs.Count > 0)
                    {
                        var entries = phs.Where(p => p.Date.ToDateTime(TimeOnly.MinValue) >= periodFromDt && p.Date.ToDateTime(TimeOnly.MinValue) < periodToDt).ToList();
                        if (entries.Count > 0) aptBase = Math.Round(entries.Average(p => p.BasePrice), 2);
                    }

                    // fallback to price calendar fixed price (use average across overlapping days)
                    if (!aptBase.HasValue && auxiliaryContext.PriceCalendarsByApartment.TryGetValue(aptId, out var pcs) && pcs.Count > 0)
                    {
                        var prices = new List<decimal>();
                        foreach (var pc in pcs)
                        {
                            var pcStart = pc.StartDate.ToDateTime(TimeOnly.MinValue);
                            var pcEndExclusive = pc.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
                            var overlapStart = pcStart > periodFromDt ? pcStart : periodFromDt;
                            var overlapEnd = pcEndExclusive < periodToDt ? pcEndExclusive : periodToDt;
                            var overlapDays = (int)Math.Max(0, (overlapEnd - overlapStart).TotalDays);
                            if (overlapDays > 0 && pc.FixedPricePerNight.HasValue)
                            {
                                for (int i = 0; i < overlapDays; i++) prices.Add(pc.FixedPricePerNight.Value);
                            }
                        }
                        if (prices.Count > 0) aptBase = Math.Round(prices.Average(), 2);
                    }

                    // final fallback to apartment base price
                    if (!aptBase.HasValue && auxiliaryContext.ApartmentsById.TryGetValue(aptId, out var apartment))
                    {
                        aptBase = Math.Round(apartment.BasePricePerNight, 2);
                    }

                    if (aptBase.HasValue)
                    {
                        var nights = aptNights.TryGetValue(aptId, out var n) ? n : 0;
                        weightedPrices.Add((aptBase.Value, nights));
                    }
                }

                if (weightedPrices.Count == 0) return 0m;
                var totalNightsWeight = weightedPrices.Sum(w => w.nights);
                if (totalNightsWeight > 0)
                {
                    var weightedSum = weightedPrices.Sum(w => w.price * w.nights);
                    return Math.Round(weightedSum / totalNightsWeight, 2);
                }

                // if no nights weighting applicable, return simple average
                return Math.Round(weightedPrices.Average(w => w.price), 2);
            }
            case "avg_price_delta":
            {
                var avgSold = (decimal)CalculateMetric(bookings, "avg_sold_price", "avg", auxiliaryContext, periodFrom, periodTo);
                var avgBase = (decimal)CalculateMetric(bookings, "avg_base_price", "avg", auxiliaryContext, periodFrom, periodTo);
                return Math.Round(avgSold - avgBase, 2);
            }
            default:
                throw new ArgumentException($"Unsupported metric field '{metricField}'.");
        }
    }

    private static decimal Aggregate(IEnumerable<decimal> values, string aggregation)
    {
        var materialized = values.ToList();
        if (materialized.Count == 0)
        {
            return 0m;
        }

        return NormalizeKey(aggregation) switch
        {
            "count" => materialized.Count,
            "sum" => materialized.Sum(),
            "avg" => Math.Round(materialized.Average(), 2, MidpointRounding.AwayFromZero),
            "min" => materialized.Min(),
            "max" => materialized.Max(),
            "distinct_count" => materialized.Distinct().Count(),
            "p50" => Percentile(materialized, 50m),
            "p90" => Percentile(materialized, 90m),
            "p95" => Percentile(materialized, 95m),
            _ => throw new ArgumentException($"Unsupported aggregation '{aggregation}'.")
        };
    }

    private static decimal Percentile(IReadOnlyList<decimal> values, decimal percentile)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;

        if (n == 1)
        {
            return sorted[0];
        }

        var rank = (percentile / 100m) * (n - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var weight = rank - lowerIndex;
        var lower = sorted[lowerIndex];
        var upper = sorted[upperIndex];
        var interpolated = lower + ((upper - lower) * weight);
        return Math.Round(interpolated, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildMetricAlias(string field, string aggregation)
    {
        var normalizedField = NormalizeKey(field);
        var normalizedAggregation = NormalizeKey(aggregation);
        return normalizedAggregation == "count" && normalizedField == "booking_count"
            ? normalizedField
            : $"{normalizedAggregation}_{normalizedField}";
    }

    private static bool Evaluate(object? value, ReportFilterRequestDto filter)
    {
        var op = NormalizeKey(filter.Operator);

        return op switch
        {
            "contains" => Contains(value, filter.Value),
            "in" => InSet(value, filter.Values),
            "between" => Between(value, filter.Values),
            _ => Compare(value, filter.Value, op)
        };
    }

    private static bool Contains(object? value, string? expected)
    {
        if (value == null || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return value.ToString()?.Contains(expected, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool InSet(object? value, IReadOnlyList<string>? set)
    {
        if (value == null || set == null || set.Count == 0)
        {
            return false;
        }

        return set.Any(item => string.Equals(item, value.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool Between(object? value, IReadOnlyList<string>? range)
    {
        if (value == null || range == null || range.Count < 2)
        {
            return false;
        }

        if (!TryConvertToDecimal(value, out var number)
            || !decimal.TryParse(range[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var lower)
            || !decimal.TryParse(range[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var upper))
        {
            return false;
        }

        return number >= lower && number <= upper;
    }

    private static bool Compare(object? value, string? expected, string op)
    {
        if (value == null || expected == null)
        {
            return false;
        }

        if (TryConvertToDecimal(value, out var left)
            && decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var right))
        {
            return op switch
            {
                "eq" => left == right,
                "ne" => left != right,
                "gt" => left > right,
                "gte" => left >= right,
                "lt" => left < right,
                "lte" => left <= right,
                _ => false
            };
        }

        var compare = string.Compare(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "eq" => compare == 0,
            "ne" => compare != 0,
            "gt" => compare > 0,
            "gte" => compare >= 0,
            "lt" => compare < 0,
            "lte" => compare <= 0,
            _ => false
        };
    }

    private static bool TryConvertToDecimal(object value, out decimal output)
    {
        output = 0m;
        return value switch
        {
            decimal d => (output = d) >= decimal.MinValue,
            int i => (output = i) >= decimal.MinValue,
            long l => (output = l) >= decimal.MinValue,
            double db => (output = Convert.ToDecimal(db)) >= decimal.MinValue,
            float f => (output = Convert.ToDecimal(f)) >= decimal.MinValue,
            _ => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out output)
        };
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}

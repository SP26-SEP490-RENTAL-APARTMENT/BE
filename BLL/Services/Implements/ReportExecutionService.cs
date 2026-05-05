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
        "tenant_id",
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
        "unique_paid_tenant_count"
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
        request ??= new ReportRunRequestDto();

        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId)
            ?? throw new ArgumentException("Report definition not found.");

        return await BuildReportAsync(definition, reportId, request, requestedByUserId, persistGenerated: true);
    }

    public async Task<ReportComparisonResultDto> CompareReportAsync(Guid reportId, ReportComparisonRequestDto request, Guid requestedByUserId)
    {
        request ??= new ReportComparisonRequestDto();
        request.RunRequest ??= new ReportRunRequestDto();

        var definition = await _reportDefinitionRepository.GetByIdAsync(reportId)
            ?? throw new ArgumentException("Report definition not found.");

        var currentRequest = request.RunRequest;
    var previousRequest = request.PreviousRunRequest ?? BuildPreviousRequest(currentRequest, request.Mode);

        var currentResult = await BuildReportAsync(definition, reportId, currentRequest, requestedByUserId, persistGenerated: false);
        var previousResult = await BuildReportAsync(definition, reportId, previousRequest, requestedByUserId, persistGenerated: false);

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

    private async Task<ReportResultDto> BuildReportAsync(
        ReportDefinition definition,
        Guid reportId,
        ReportRunRequestDto request,
        Guid requestedByUserId,
        bool persistGenerated)
    {
        // For initial implementation, support booking-based analytics with configurable dimensions/metrics.
        var from = request.From ?? Common.Utils.VietnamTime.Now.AddDays(-30);
        var to = request.To ?? Common.Utils.VietnamTime.Now;

        var normalizedFrom = DateOnly.FromDateTime(from.Date);
        var normalizedTo = DateOnly.FromDateTime(to.Date.AddDays(1));

        var bookings = await _bookingRepository.FindAsync(b =>
            b.CreatedAt.HasValue &&
            b.CreatedAt.Value >= normalizedFrom.ToDateTime(TimeOnly.MinValue) &&
            b.CreatedAt.Value < normalizedTo.ToDateTime(TimeOnly.MinValue));

        var dimensions = ResolveDimensions(request);
        var metrics = ResolveMetrics(request);
        var filters = request.Filters ?? Array.Empty<ReportFilterRequestDto>();

        ValidateFilters(filters, dimensions, metrics);

        var filteredBookings = bookings
            .Where(b => MatchAllDimensionFilters(b, filters, dimensions))
            .ToList();

        var grouped = filteredBookings
            .GroupBy(b => BuildDimensionKey(b, dimensions))
            .Select(g => BuildRow(g.ToList(), dimensions, metrics))
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
        IReadOnlyList<ReportDimensionRequestDto> dimensions)
    {
        var dimensionMap = dimensions.ToDictionary(
            d => NormalizeKey(d.Alias ?? d.Field),
            d => GetDimensionValue(booking, d.Field),
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

    private static string BuildDimensionKey(Booking booking, IReadOnlyList<ReportDimensionRequestDto> dimensions)
    {
        return string.Join("|", dimensions.Select(d => GetDimensionValue(booking, d.Field)?.ToString() ?? "null"));
    }

    private static ReportResultRowDto BuildRow(
        IReadOnlyList<Booking> bookingGroup,
        IReadOnlyList<ReportDimensionRequestDto> dimensions,
        IReadOnlyList<ReportMetricRequestDto> metrics)
    {
        var first = bookingGroup[0];
        var row = new ReportResultRowDto();

        foreach (var dim in dimensions)
        {
            row.Dimensions[dim.Alias ?? dim.Field] = GetDimensionValue(first, dim.Field);
        }

        foreach (var metric in metrics)
        {
            row.Metrics[metric.Alias ?? BuildMetricAlias(metric.Field, metric.Aggregation)] =
                CalculateMetric(bookingGroup, metric.Field, metric.Aggregation);
        }

        return row;
    }

    private static object? GetDimensionValue(Booking booking, string dimensionField)
    {
        return NormalizeKey(dimensionField) switch
        {
            "date" => booking.CreatedAt?.Date,
            "status" => booking.Status,
            "payment_mode" => booking.PaymentMode,
            "apartment_id" => booking.ApartmentId,
            "tenant_id" => booking.TenantId,
            "nights" => booking.Nights,
            _ => throw new ArgumentException($"Unsupported dimension field '{dimensionField}'.")
        };
    }

    private static decimal CalculateMetric(IReadOnlyList<Booking> bookings, string metricField, string aggregation)
    {
        var normalizedMetric = NormalizeKey(metricField);
        var normalizedAggregation = NormalizeKey(aggregation);

        return normalizedMetric switch
        {
            "booking_count" => bookings.Count,
            "total_revenue" => Aggregate(bookings.Select(b => b.TotalPrice), normalizedAggregation),
            "avg_booking_value" => Aggregate(bookings.Select(b => b.TotalPrice), "avg"),
            "min_booking_value" => Aggregate(bookings.Select(b => b.TotalPrice), "min"),
            "max_booking_value" => Aggregate(bookings.Select(b => b.TotalPrice), "max"),
            "paid_booking_count" => bookings.Count(b => string.Equals(b.Status, "paid", StringComparison.OrdinalIgnoreCase)
                                                   || string.Equals(b.Status, "completed", StringComparison.OrdinalIgnoreCase)),
            "unique_tenant_count" => bookings.Select(b => b.TenantId).Distinct().Count(),
            "unique_apartment_count" => bookings.Select(b => b.ApartmentId).Distinct().Count(),
            "unique_paid_tenant_count" => bookings
                .Where(b => string.Equals(b.Status, "paid", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(b.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .Select(b => b.TenantId)
                .Distinct()
                .Count(),
            _ => throw new ArgumentException($"Unsupported metric field '{metricField}'.")
        };
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

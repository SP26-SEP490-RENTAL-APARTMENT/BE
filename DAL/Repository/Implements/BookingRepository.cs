using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.Implements
{
    public class BookingRepository : Repository<Booking>, IBookingRepository
    {
        public BookingRepository(AppDbContext context) : base(context)
        {
        }

        private static IQueryable<Booking> ApplyBookingSearch(IQueryable<Booking> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            var lowered = search.ToLower();
            return query.Where(b =>
                (b.Status != null && b.Status.ToLower().Contains(lowered)) ||
                (b.Tenant != null &&
                 b.Tenant.TenantNavigation != null &&
                 b.Tenant.TenantNavigation.FullName != null &&
                 b.Tenant.TenantNavigation.FullName.ToLower().Contains(lowered)));
        }

        public override async Task<Booking?> GetByIdAsync(Guid id)
        {
            return await _context.Bookings
                .Include(b => b.Tenant)
                .ThenInclude(t => t.TenantNavigation)
                .Include(b => b.BookingCheckTime)
                .Include(b => b.Apartment)
                    .ThenInclude(a => a.ApartmentMedia)
                .FirstOrDefaultAsync(b => b.BookingId == id);
        }

        public override async Task<(IEnumerable<Booking> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query = _context.Bookings
                .Include(b => b.Tenant)
                .ThenInclude(t => t.TenantNavigation)
                .Include(b => b.BookingCheckTime)
                .Include(b => b.Apartment)
                    .ThenInclude(a => a.ApartmentMedia)
                .AsQueryable();

            query = ApplyFilters(query, filters, allowedColumns);
            query = ApplyBookingSearch(query, search);
            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<(IEnumerable<Booking> Items, int TotalCount)> GetByLandlordAsync(
            Guid landlordId,
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var query =
                from b in _context.Bookings
                    .Include(b => b.Tenant)
                    .ThenInclude(t => t.TenantNavigation)
                    .Include(b => b.BookingCheckTime)
                    .Include(b => b.Apartment)
                        .ThenInclude(a => a.ApartmentMedia)
                    .AsQueryable()
                join a in _context.Apartments on b.ApartmentId equals a.ApartmentId
                where a.LandlordId == landlordId
                select b;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = ApplyBookingSearch(query, search);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt <= toDate.Value);
            }

            query = ApplySorting(query, sortBy, sortOrder);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<ReportResultRowDto> Items, int TotalCount)> GetPagedGroupedReportRowsAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions,
            IReadOnlyList<ReportMetricRequestDto> metrics,
            string? searchTerm,
            int page,
            int pageSize,
            Guid? landlordId = null)
        {
            var query = _context.Bookings
                .AsNoTracking()
                .Include(b => b.Apartment)
                .Include(b => b.Tenant)
                    .ThenInclude(t => t.TenantNavigation)
                .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= fromInclusive && b.CreatedAt.Value < toExclusive);

            if (landlordId.HasValue)
            {
                query = query.Where(b => b.Apartment.LandlordId == landlordId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowered = searchTerm.Trim().ToLower();
                query = query.Where(b =>
                    (b.Status != null && b.Status.ToLower().Contains(lowered)) ||
                    (b.PaymentMode != null && b.PaymentMode.ToLower().Contains(lowered)) ||
                    (b.Tenant != null && b.Tenant.TenantNavigation != null && b.Tenant.TenantNavigation.FullName != null && b.Tenant.TenantNavigation.FullName.ToLower().Contains(lowered)) ||
                    (b.Apartment != null && b.Apartment.Title != null && b.Apartment.Title.ToLower().Contains(lowered)));
            }

            var useDate = dimensions.Any(d => string.Equals(d.Field, "date", StringComparison.OrdinalIgnoreCase));
            var useWeek = dimensions.Any(d => string.Equals(d.Field, "week", StringComparison.OrdinalIgnoreCase));
            var useMonth = dimensions.Any(d => string.Equals(d.Field, "month", StringComparison.OrdinalIgnoreCase));
            var useQuarter = dimensions.Any(d => string.Equals(d.Field, "quarter", StringComparison.OrdinalIgnoreCase));
            var useYear = dimensions.Any(d => string.Equals(d.Field, "year", StringComparison.OrdinalIgnoreCase));
            var useStatus = dimensions.Any(d => string.Equals(d.Field, "status", StringComparison.OrdinalIgnoreCase));
            var usePaymentMode = dimensions.Any(d => string.Equals(d.Field, "payment_mode", StringComparison.OrdinalIgnoreCase));
            var useApartmentId = dimensions.Any(d => string.Equals(d.Field, "apartment_id", StringComparison.OrdinalIgnoreCase));
            var useApartmentName = dimensions.Any(d => string.Equals(d.Field, "apartment_name", StringComparison.OrdinalIgnoreCase));
            var useTenantId = dimensions.Any(d => string.Equals(d.Field, "tenant_id", StringComparison.OrdinalIgnoreCase));
            var useTenantName = dimensions.Any(d => string.Equals(d.Field, "tenant_name", StringComparison.OrdinalIgnoreCase));
            var useNights = dimensions.Any(d => string.Equals(d.Field, "nights", StringComparison.OrdinalIgnoreCase));
            var useCity = dimensions.Any(d => string.Equals(d.Field, "city", StringComparison.OrdinalIgnoreCase));
            var useGuestNationality = dimensions.Any(d => string.Equals(d.Field, "guest_nationality", StringComparison.OrdinalIgnoreCase));

            var groupedQuery = query
                .Select(b => new
                {
                    Booking = b,
                    Key = new BookingReportGroupKeyParts
                    {
                        Date = useDate && b.CreatedAt.HasValue ? b.CreatedAt.Value.Date : null,
                        Week = useWeek && b.CreatedAt.HasValue ? b.CreatedAt.Value.AddDays(-((int)b.CreatedAt.Value.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7).Date : null,
                        MonthYear = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        MonthMonth = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Month : null,
                        QuarterYear = useQuarter && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        QuarterMonth = useQuarter && b.CreatedAt.HasValue ? ((b.CreatedAt.Value.Month - 1) / 3) * 3 + 1 : null,
                        YearYear = useYear && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        Status = useStatus ? b.Status : null,
                        PaymentMode = usePaymentMode ? b.PaymentMode : null,
                        ApartmentId = useApartmentId ? b.ApartmentId : null,
                        ApartmentName = useApartmentName ? b.Apartment.Title : null,
                        TenantId = useTenantId ? b.TenantId : null,
                        TenantName = useTenantName ? b.Tenant.TenantNavigation.FullName : null,
                        Nights = useNights ? b.Nights : null,
                        City = useCity ? b.Apartment.City : null,
                        GuestNationality = useGuestNationality ? b.Tenant.TenantNavigation.Nationality : null
                    }
                })
                .GroupBy(x => x.Key)
                .Select(g => new
                {
                    Key = g.Key,
                    BookingCount = g.Count(),
                    TotalRevenue = g.Sum(x => x.Booking.TotalPrice),
                    AvgBookingValue = g.Average(x => x.Booking.TotalPrice),
                    MinBookingValue = g.Min(x => x.Booking.TotalPrice),
                    MaxBookingValue = g.Max(x => x.Booking.TotalPrice),
                    PaidBookingCount = g.Count(x => x.Booking.Status != null && (x.Booking.Status.ToLower() == "paid" || x.Booking.Status.ToLower() == "completed")),
                    ConfirmedBookingCount = g.Count(x => x.Booking.Status != null && x.Booking.Status.ToLower() == "confirmed"),
                    CompletedBookingCount = g.Count(x => x.Booking.Status != null && x.Booking.Status.ToLower() == "completed"),
                    CancelledBookingCount = g.Count(x => x.Booking.Status != null && x.Booking.Status.ToLower() == "cancelled"),
                    UniqueTenantCount = g.Select(x => x.Booking.TenantId).Distinct().Count(),
                    UniqueApartmentCount = g.Select(x => x.Booking.ApartmentId).Distinct().Count(),
                    UniquePaidTenantCount = g.Where(x => x.Booking.Status != null && (x.Booking.Status.ToLower() == "paid" || x.Booking.Status.ToLower() == "completed")).Select(x => x.Booking.TenantId).Distinct().Count(),
                    AvgLengthOfStay = (decimal)g.Average(x => x.Booking.Nights),
                    PackageRevenue = g.Sum(x => x.Booking.PackagePrice ?? 0m),
                    PackageCount = g.Count(x => x.Booking.PackageId != null),
                    TotalNights = g.Sum(x => x.Booking.Nights)
                });
            // We'll enrich review aggregates after retrieving the page using a separate query across Reviews.

            var totalCount = await groupedQuery.CountAsync();
            var orderedGrouped = groupedQuery.OrderBy(x =>
                x.Key.Date ?? DateTime.MinValue)
                .ThenBy(x => x.Key.Status)
                .ThenBy(x => x.Key.PaymentMode)
                .ThenBy(x => x.Key.ApartmentId)
                .ThenBy(x => x.Key.TenantId)
                .ThenBy(x => x.Key.Nights);

            var groupedPage = await orderedGrouped
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var pageItems = groupedPage.Select(item => new BookingReportAggregateRow
            {
                Key = BuildBookingReportGroupKey(item.Key),
                BookingCount = item.BookingCount,
                TotalRevenue = item.TotalRevenue,
                AvgBookingValue = item.AvgBookingValue,
                MinBookingValue = item.MinBookingValue,
                MaxBookingValue = item.MaxBookingValue,
                PaidBookingCount = item.PaidBookingCount,
                ConfirmedBookingCount = item.ConfirmedBookingCount,
                CompletedBookingCount = item.CompletedBookingCount,
                CancelledBookingCount = item.CancelledBookingCount,
                UniqueTenantCount = item.UniqueTenantCount,
                UniqueApartmentCount = item.UniqueApartmentCount,
                UniquePaidTenantCount = item.UniquePaidTenantCount,
                AvgLengthOfStay = item.AvgLengthOfStay,
                PackageRevenue = item.PackageRevenue,
                PackageCount = item.PackageCount,
                TotalNights = item.TotalNights
            }).ToList();
            // Enrich review aggregates for apartments referenced in the page
            var apartmentIds = pageItems
                .Select(pi => pi.Key.ApartmentId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (apartmentIds.Count > 0)
            {
                var reviewsAgg = await _context.Reviews
                    .Where(r => r.ApartmentId != null && apartmentIds.Contains(r.ApartmentId.Value) && r.CreatedAt.HasValue && r.CreatedAt.Value >= fromInclusive && r.CreatedAt.Value < toExclusive)
                    .GroupBy(r => r.ApartmentId.Value)
                    .Select(g => new { ApartmentId = g.Key, Count = g.Count(), Avg = g.Average(r => r.Rating ?? 0) })
                    .ToListAsync();

                var reviewMap = reviewsAgg.ToDictionary(a => a.ApartmentId, a => (Count: a.Count, Avg: (decimal)a.Avg));

                foreach (var item in pageItems)
                {
                    if (item.Key.ApartmentId.HasValue && reviewMap.TryGetValue(item.Key.ApartmentId.Value, out var agg))
                    {
                        item.ReviewCount = agg.Count;
                        item.AvgReviewRating = Math.Round(agg.Avg, 2);
                    }
                    else
                    {
                        item.ReviewCount = 0;
                        item.AvgReviewRating = 0m;
                    }
                }
            }

            if (metrics.Any(m => string.Equals(m.Field, "avg_base_price", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Field, "avg_price_delta", StringComparison.OrdinalIgnoreCase)))
            {
                await PopulateBasePriceMetricsAsync(pageItems, fromInclusive, toExclusive, dimensions, metrics);
            }

            if (metrics.Any(m => string.Equals(m.Field, "occupancy_percent", StringComparison.OrdinalIgnoreCase)))
            {
                await PopulateOccupancyMetricsAsync(pageItems, fromInclusive, toExclusive, dimensions);
            }

            var rows = pageItems.Select(item => BuildReportRow(item, dimensions, metrics)).ToList();
            return (rows, totalCount);
        }

        private static BookingReportGroupKey BuildBookingReportGroupKey(BookingReportGroupKeyParts parts)
        {
            return new BookingReportGroupKey
            {
                Date = parts.Date,
                Week = parts.Week,
                Month = parts.MonthYear.HasValue && parts.MonthMonth.HasValue
                    ? new DateTime(parts.MonthYear.Value, parts.MonthMonth.Value, 1)
                    : null,
                Quarter = parts.QuarterYear.HasValue && parts.QuarterMonth.HasValue
                    ? new DateTime(parts.QuarterYear.Value, parts.QuarterMonth.Value, 1)
                    : null,
                Year = parts.YearYear.HasValue
                    ? new DateTime(parts.YearYear.Value, 1, 1)
                    : null,
                Status = parts.Status,
                PaymentMode = parts.PaymentMode,
                ApartmentId = parts.ApartmentId,
                ApartmentName = parts.ApartmentName,
                TenantId = parts.TenantId,
                TenantName = parts.TenantName,
                Nights = parts.Nights,
                City = parts.City,
                GuestNationality = parts.GuestNationality
            };
        }

        private static ReportResultRowDto BuildReportRow(
            BookingReportAggregateRow aggregateRow,
            IReadOnlyList<ReportDimensionRequestDto> dimensions,
            IReadOnlyList<ReportMetricRequestDto> metrics)
        {
            var row = new ReportResultRowDto();
            foreach (var dimension in dimensions)
            {
                row.Dimensions[dimension.Alias ?? dimension.Field] = GetDimensionValue(aggregateRow.Key, dimension.Field);
            }

            foreach (var metric in metrics)
            {
                var agg = metric.Aggregation?.ToLowerInvariant() ?? "count";
                var field = metric.Field?.ToLowerInvariant() ?? string.Empty;
                var alias = metric.Alias ?? (agg == "count" && field == "booking_count" ? "booking_count" : $"{agg}_{field}");
                row.Metrics[alias] = GetMetricValue(aggregateRow, metric.Field, metric.Aggregation);
            }

            return row;
        }

        private static object? GetDimensionValue(BookingReportGroupKey key, string dimensionField)
        {
            return dimensionField.ToLowerInvariant() switch
            {
                "week" => key.Week,
                "month" => key.Month,
                "quarter" => key.Quarter,
                "year" => key.Year,
                "date" => key.Date,
                "status" => key.Status,
                "payment_mode" => key.PaymentMode,
                "apartment_id" => key.ApartmentId,
                "apartment_name" => key.ApartmentName,
                "tenant_id" => key.TenantId,
                "tenant_name" => key.TenantName,
                "nights" => key.Nights,
                "city" => key.City,
                "guest_nationality" => key.GuestNationality,
                _ => null
            };
        }

        private static decimal GetMetricValue(BookingReportAggregateRow row, string metricField, string aggregation)
        {
            var field = metricField.ToLowerInvariant();
            var agg = aggregation.ToLowerInvariant();

            return field switch
            {
                "booking_count" => row.BookingCount,
                "total_revenue" => agg switch
                {
                    "avg" => row.BookingCount == 0 ? 0m : Math.Round(row.TotalRevenue / row.BookingCount, 2),
                    "min" => row.MinBookingValue,
                    "max" => row.MaxBookingValue,
                    _ => row.TotalRevenue
                },
                "avg_booking_value" => row.AvgBookingValue,
                "min_booking_value" => row.MinBookingValue,
                "max_booking_value" => row.MaxBookingValue,
                "paid_booking_count" => row.PaidBookingCount,
                "unique_tenant_count" => row.UniqueTenantCount,
                "unique_apartment_count" => row.UniqueApartmentCount,
                "unique_paid_tenant_count" => row.UniquePaidTenantCount,
                "avg_length_of_stay" => row.AvgLengthOfStay,
                "package_revenue" => row.PackageRevenue,
                "package_count" => row.PackageCount,
                "adr" => row.TotalNights == 0 ? 0m : Math.Round(row.TotalRevenue / row.TotalNights, 2, MidpointRounding.AwayFromZero),
                "avg_sold_price" => row.TotalNights == 0 ? 0m : Math.Round(row.TotalRevenue / row.TotalNights, 2, MidpointRounding.AwayFromZero),
                "review_avg_rating" => row.AvgReviewRating,
                "review_count" => row.ReviewCount,
                "avg_base_price" => row.AvgBasePrice,
                "avg_price_delta" => row.AvgPriceDelta,
                "occupancy_percent" => row.OccupancyPercent,
                "confirmed_booking_count" => row.ConfirmedBookingCount,
                "completed_booking_count" => row.CompletedBookingCount,
                "cancelled_booking_count" => row.CancelledBookingCount,
                _ => 0m
            };
        }

        private async Task PopulateBasePriceMetricsAsync(
            IList<BookingReportAggregateRow> pageItems,
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions,
            IReadOnlyList<ReportMetricRequestDto> metrics)
        {
            if (pageItems.Count == 0)
            {
                return;
            }

            var needsBasePrice = metrics.Any(m => string.Equals(m.Field, "avg_base_price", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Field, "avg_price_delta", StringComparison.OrdinalIgnoreCase));
            if (!needsBasePrice)
            {
                return;
            }

            var groupApartmentRows = await QueryGroupApartmentNightRowsAsync(fromInclusive, toExclusive, dimensions);
            if (groupApartmentRows.Count == 0)
            {
                foreach (var item in pageItems)
                {
                    item.AvgBasePrice = 0m;
                    item.AvgPriceDelta = item.TotalNights == 0 ? 0m : Math.Round((item.TotalRevenue / item.TotalNights) - item.AvgBasePrice, 2, MidpointRounding.AwayFromZero);
                }

                return;
            }

            var apartmentIds = groupApartmentRows
                .Select(r => r.ApartmentId)
                .Distinct()
                .ToList();

            var effectiveBasePriceByApartment = await QueryEffectiveBasePriceByApartmentAsync(apartmentIds, fromInclusive, toExclusive);

            var groupedWeights = groupApartmentRows
                .GroupBy(r => BuildGroupApartmentKey(r.Key, r.ApartmentId))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in pageItems)
            {
                var totalNights = 0;
                decimal weightedBasePriceSum = 0m;

                var groupKey = BuildGroupKey(item.Key);
                foreach (var weight in groupedWeights.Where(kvp => kvp.Key.StartsWith(groupKey, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var apartmentRow in weight.Value)
                    {
                        totalNights += apartmentRow.Nights;
                        var basePrice = effectiveBasePriceByApartment.TryGetValue(apartmentRow.ApartmentId, out var price)
                            ? price
                            : 0m;
                        weightedBasePriceSum += basePrice * apartmentRow.Nights;
                    }
                }

                item.AvgBasePrice = totalNights == 0 ? 0m : Math.Round(weightedBasePriceSum / totalNights, 2, MidpointRounding.AwayFromZero);
                item.AvgPriceDelta = item.TotalNights == 0
                    ? 0m
                    : Math.Round((item.TotalRevenue / item.TotalNights) - item.AvgBasePrice, 2, MidpointRounding.AwayFromZero);
            }
        }

        private async Task<List<GroupApartmentNightRow>> QueryGroupApartmentNightRowsAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions)
        {
            var useDate = dimensions.Any(d => string.Equals(d.Field, "date", StringComparison.OrdinalIgnoreCase));
            var useWeek = dimensions.Any(d => string.Equals(d.Field, "week", StringComparison.OrdinalIgnoreCase));
            var useMonth = dimensions.Any(d => string.Equals(d.Field, "month", StringComparison.OrdinalIgnoreCase));
            var useQuarter = dimensions.Any(d => string.Equals(d.Field, "quarter", StringComparison.OrdinalIgnoreCase));
            var useYear = dimensions.Any(d => string.Equals(d.Field, "year", StringComparison.OrdinalIgnoreCase));
            var useStatus = dimensions.Any(d => string.Equals(d.Field, "status", StringComparison.OrdinalIgnoreCase));
            var usePaymentMode = dimensions.Any(d => string.Equals(d.Field, "payment_mode", StringComparison.OrdinalIgnoreCase));
            var useApartmentId = dimensions.Any(d => string.Equals(d.Field, "apartment_id", StringComparison.OrdinalIgnoreCase));
            var useApartmentName = dimensions.Any(d => string.Equals(d.Field, "apartment_name", StringComparison.OrdinalIgnoreCase));
            var useTenantId = dimensions.Any(d => string.Equals(d.Field, "tenant_id", StringComparison.OrdinalIgnoreCase));
            var useTenantName = dimensions.Any(d => string.Equals(d.Field, "tenant_name", StringComparison.OrdinalIgnoreCase));
            var useNights = dimensions.Any(d => string.Equals(d.Field, "nights", StringComparison.OrdinalIgnoreCase));
            var useCity = dimensions.Any(d => string.Equals(d.Field, "city", StringComparison.OrdinalIgnoreCase));
            var useGuestNationality = dimensions.Any(d => string.Equals(d.Field, "guest_nationality", StringComparison.OrdinalIgnoreCase));

            var rows = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= fromInclusive && b.CreatedAt.Value < toExclusive)
                .Select(b => new
                {
                    Key = new BookingReportGroupKeyParts
                    {
                        Date = useDate && b.CreatedAt.HasValue ? b.CreatedAt.Value.Date : null,
                        Week = useWeek && b.CreatedAt.HasValue ? b.CreatedAt.Value.AddDays(-((int)b.CreatedAt.Value.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7).Date : null,
                        MonthYear = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        MonthMonth = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Month : null,
                        QuarterYear = useQuarter && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        QuarterMonth = useQuarter && b.CreatedAt.HasValue ? ((b.CreatedAt.Value.Month - 1) / 3) * 3 + 1 : null,
                        YearYear = useYear && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        Status = useStatus ? b.Status : null,
                        PaymentMode = usePaymentMode ? b.PaymentMode : null,
                        ApartmentId = useApartmentId ? b.ApartmentId : null,
                        ApartmentName = useApartmentName ? b.Apartment.Title : null,
                        TenantId = useTenantId ? b.TenantId : null,
                        TenantName = useTenantName ? b.Tenant.TenantNavigation.FullName : null,
                        Nights = useNights ? b.Nights : null,
                        City = useCity ? b.Apartment.City : null,
                        GuestNationality = useGuestNationality ? b.Tenant.TenantNavigation.Nationality : null
                    },
                    ApartmentId = b.ApartmentId,
                    Nights = b.Nights
                })
                .GroupBy(x => new
                {
                    x.Key.Date,
                    x.Key.Week,
                    x.Key.MonthYear,
                    x.Key.MonthMonth,
                    x.Key.QuarterYear,
                    x.Key.QuarterMonth,
                    x.Key.YearYear,
                    x.Key.Status,
                    x.Key.PaymentMode,
                    DimensionApartmentId = x.Key.ApartmentId,
                    x.Key.ApartmentName,
                    x.Key.TenantId,
                    x.Key.TenantName,
                    x.Key.Nights,
                    x.Key.City,
                    x.Key.GuestNationality,
                    BookingApartmentId = x.ApartmentId
                })
                .Select(g => new
                {
                    Key = g.Key,
                    ApartmentId = g.Key.BookingApartmentId,
                    Nights = g.Sum(x => x.Nights)
                })
                .ToListAsync();

            return rows
                .Select(row => new GroupApartmentNightRow
                {
                    Key = BuildBookingReportGroupKey(new BookingReportGroupKeyParts
                    {
                        Date = row.Key.Date,
                        Week = row.Key.Week,
                        MonthYear = row.Key.MonthYear,
                        MonthMonth = row.Key.MonthMonth,
                        QuarterYear = row.Key.QuarterYear,
                        QuarterMonth = row.Key.QuarterMonth,
                        YearYear = row.Key.YearYear,
                        Status = row.Key.Status,
                        PaymentMode = row.Key.PaymentMode,
                        ApartmentId = row.Key.DimensionApartmentId,
                        ApartmentName = row.Key.ApartmentName,
                        TenantId = row.Key.TenantId,
                        TenantName = row.Key.TenantName,
                        Nights = row.Key.Nights,
                        City = row.Key.City,
                        GuestNationality = row.Key.GuestNationality
                    }),
                    ApartmentId = row.ApartmentId,
                    Nights = row.Nights
                })
                .ToList();
        }

        private async Task<Dictionary<Guid, decimal>> QueryEffectiveBasePriceByApartmentAsync(
            IReadOnlyList<Guid> apartmentIds,
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            if (apartmentIds.Count == 0)
            {
                return new Dictionary<Guid, decimal>();
            }

            var periodStart = DateOnly.FromDateTime(fromInclusive.Date);
            var periodEndExclusive = DateOnly.FromDateTime(toExclusive.Date);

            var smartPricing = await _context.SmartPricingHistories
                .AsNoTracking()
                .Where(sp => apartmentIds.Contains(sp.ApartmentId) && sp.Date >= periodStart && sp.Date < periodEndExclusive)
                .GroupBy(sp => sp.ApartmentId)
                .Select(g => new
                {
                    ApartmentId = g.Key,
                    Count = g.Count(),
                    AvgBasePrice = g.Average(x => x.BasePrice)
                })
                .ToListAsync();

            var calendarRows = await _context.ApartmentPriceCalendars
                .AsNoTracking()
                .Where(pc => apartmentIds.Contains(pc.ApartmentId) && pc.StartDate < periodEndExclusive && pc.EndDate >= periodStart)
                .Select(pc => new
                {
                    pc.ApartmentId,
                    pc.StartDate,
                    pc.EndDate,
                    pc.FixedPricePerNight
                })
                .ToListAsync();

            var apartmentBases = await _context.Apartments
                .AsNoTracking()
                .Where(a => apartmentIds.Contains(a.ApartmentId))
                .Select(a => new { a.ApartmentId, a.BasePricePerNight })
                .ToListAsync();

            var smartMap = smartPricing.ToDictionary(x => x.ApartmentId, x => x.AvgBasePrice);
            var calendarMap = new Dictionary<Guid, decimal>();

            foreach (var row in calendarRows)
            {
                if (!row.FixedPricePerNight.HasValue)
                {
                    continue;
                }

                var overlapStart = row.StartDate > periodStart ? row.StartDate : periodStart;
                var overlapEndExclusive = row.EndDate < periodEndExclusive ? row.EndDate.AddDays(1) : periodEndExclusive;
                var overlapDays = (int)Math.Max(0, (overlapEndExclusive.ToDateTime(TimeOnly.MinValue) - overlapStart.ToDateTime(TimeOnly.MinValue)).TotalDays);
                if (overlapDays <= 0)
                {
                    continue;
                }

                if (!calendarMap.TryGetValue(row.ApartmentId, out var currentTotal))
                {
                    calendarMap[row.ApartmentId] = row.FixedPricePerNight.Value * overlapDays;
                    continue;
                }

                calendarMap[row.ApartmentId] = currentTotal + (row.FixedPricePerNight.Value * overlapDays);
            }

            var calendarWeightMap = calendarRows
                .Where(r => r.FixedPricePerNight.HasValue)
                .GroupBy(r => r.ApartmentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(row =>
                    {
                        var overlapStart = row.StartDate > periodStart ? row.StartDate : periodStart;
                        var overlapEndExclusive = row.EndDate < periodEndExclusive ? row.EndDate.AddDays(1) : periodEndExclusive;
                        return (int)Math.Max(0, (overlapEndExclusive.ToDateTime(TimeOnly.MinValue) - overlapStart.ToDateTime(TimeOnly.MinValue)).TotalDays);
                    }));

            var result = new Dictionary<Guid, decimal>();
            foreach (var apt in apartmentBases)
            {
                if (smartMap.TryGetValue(apt.ApartmentId, out var smartBase))
                {
                    result[apt.ApartmentId] = Math.Round(smartBase, 2, MidpointRounding.AwayFromZero);
                    continue;
                }

                var calendarWeight = calendarWeightMap.TryGetValue(apt.ApartmentId, out var weight) ? weight : 0;
                if (calendarWeight > 0 && calendarMap.TryGetValue(apt.ApartmentId, out var calendarWeightedSum))
                {
                    result[apt.ApartmentId] = Math.Round(calendarWeightedSum / calendarWeight, 2, MidpointRounding.AwayFromZero);
                    continue;
                }

                result[apt.ApartmentId] = Math.Round(apt.BasePricePerNight, 2, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        private async Task PopulateOccupancyMetricsAsync(
            IList<BookingReportAggregateRow> pageItems,
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions)
        {
            if (pageItems.Count == 0)
            {
                return;
            }

            var totalDays = (int)(toExclusive.Date - fromInclusive.Date).TotalDays;
            if (totalDays <= 0)
            {
                foreach (var item in pageItems)
                {
                    item.OccupancyPercent = 0m;
                }

                return;
            }

            var apartmentIdsByGroupKey = await QueryApartmentIdsByGroupKeyAsync(fromInclusive, toExclusive, dimensions);
            if (apartmentIdsByGroupKey.Count == 0)
            {
                foreach (var item in pageItems)
                {
                    item.OccupancyPercent = 0m;
                }

                return;
            }

            var allApartmentIds = apartmentIdsByGroupKey.Values
                .SelectMany(ids => ids)
                .Distinct()
                .ToList();

            var blockedDaysByApartment = await QueryBlockedDaysByApartmentAsync(allApartmentIds, fromInclusive, toExclusive);

            foreach (var item in pageItems)
            {
                var groupKey = BuildGroupKey(item.Key);
                if (!apartmentIdsByGroupKey.TryGetValue(groupKey, out var apartmentIds) || apartmentIds.Count == 0)
                {
                    item.OccupancyPercent = 0m;
                    continue;
                }

                var bookedNights = item.TotalNights;
                var availableRoomNights = 0;

                foreach (var apartmentId in apartmentIds.Distinct())
                {
                    var blockedDays = blockedDaysByApartment.TryGetValue(apartmentId, out var blocked)
                        ? blocked
                        : 0;
                    availableRoomNights += Math.Max(0, totalDays - blockedDays);
                }

                item.OccupancyPercent = availableRoomNights == 0
                    ? 0m
                    : Math.Min(100m, Math.Round((decimal)bookedNights / availableRoomNights * 100m, 2, MidpointRounding.AwayFromZero));
            }
        }

        private async Task<Dictionary<string, List<Guid>>> QueryApartmentIdsByGroupKeyAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            IReadOnlyList<ReportDimensionRequestDto> dimensions)
        {
            var useDate = dimensions.Any(d => string.Equals(d.Field, "date", StringComparison.OrdinalIgnoreCase));
            var useWeek = dimensions.Any(d => string.Equals(d.Field, "week", StringComparison.OrdinalIgnoreCase));
            var useMonth = dimensions.Any(d => string.Equals(d.Field, "month", StringComparison.OrdinalIgnoreCase));
            var useQuarter = dimensions.Any(d => string.Equals(d.Field, "quarter", StringComparison.OrdinalIgnoreCase));
            var useYear = dimensions.Any(d => string.Equals(d.Field, "year", StringComparison.OrdinalIgnoreCase));
            var useStatus = dimensions.Any(d => string.Equals(d.Field, "status", StringComparison.OrdinalIgnoreCase));
            var usePaymentMode = dimensions.Any(d => string.Equals(d.Field, "payment_mode", StringComparison.OrdinalIgnoreCase));
            var useApartmentId = dimensions.Any(d => string.Equals(d.Field, "apartment_id", StringComparison.OrdinalIgnoreCase));
            var useApartmentName = dimensions.Any(d => string.Equals(d.Field, "apartment_name", StringComparison.OrdinalIgnoreCase));
            var useTenantId = dimensions.Any(d => string.Equals(d.Field, "tenant_id", StringComparison.OrdinalIgnoreCase));
            var useTenantName = dimensions.Any(d => string.Equals(d.Field, "tenant_name", StringComparison.OrdinalIgnoreCase));
            var useNights = dimensions.Any(d => string.Equals(d.Field, "nights", StringComparison.OrdinalIgnoreCase));
            var useCity = dimensions.Any(d => string.Equals(d.Field, "city", StringComparison.OrdinalIgnoreCase));
            var useGuestNationality = dimensions.Any(d => string.Equals(d.Field, "guest_nationality", StringComparison.OrdinalIgnoreCase));

            var rows = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= fromInclusive && b.CreatedAt.Value < toExclusive)
                .Select(b => new
                {
                    Key = new BookingReportGroupKeyParts
                    {
                        Date = useDate && b.CreatedAt.HasValue ? b.CreatedAt.Value.Date : null,
                        Week = useWeek && b.CreatedAt.HasValue ? b.CreatedAt.Value.AddDays(-((int)b.CreatedAt.Value.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7).Date : null,
                        MonthYear = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        MonthMonth = useMonth && b.CreatedAt.HasValue ? b.CreatedAt.Value.Month : null,
                        QuarterYear = useQuarter && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        QuarterMonth = useQuarter && b.CreatedAt.HasValue ? ((b.CreatedAt.Value.Month - 1) / 3) * 3 + 1 : null,
                        YearYear = useYear && b.CreatedAt.HasValue ? b.CreatedAt.Value.Year : null,
                        Status = useStatus ? b.Status : null,
                        PaymentMode = usePaymentMode ? b.PaymentMode : null,
                        ApartmentId = useApartmentId ? b.ApartmentId : null,
                        ApartmentName = useApartmentName ? b.Apartment.Title : null,
                        TenantId = useTenantId ? b.TenantId : null,
                        TenantName = useTenantName ? b.Tenant.TenantNavigation.FullName : null,
                        Nights = useNights ? b.Nights : null,
                        City = useCity ? b.Apartment.City : null,
                        GuestNationality = useGuestNationality ? b.Tenant.TenantNavigation.Nationality : null
                    },
                    b.ApartmentId
                })
                .GroupBy(x => new
                {
                    x.Key.Date,
                    x.Key.Week,
                    x.Key.MonthYear,
                    x.Key.MonthMonth,
                    x.Key.QuarterYear,
                    x.Key.QuarterMonth,
                    x.Key.YearYear,
                    x.Key.Status,
                    x.Key.PaymentMode,
                    DimensionApartmentId = x.Key.ApartmentId,
                    x.Key.ApartmentName,
                    x.Key.TenantId,
                    x.Key.TenantName,
                    x.Key.Nights,
                    x.Key.City,
                    x.Key.GuestNationality,
                    x.ApartmentId
                })
                .Select(g => new
                {
                    Key = g.Key,
                    g.Key.ApartmentId
                })
                .ToListAsync();

            return rows
                .Select(row => new
                {
                    Key = BuildBookingReportGroupKey(new BookingReportGroupKeyParts
                    {
                        Date = row.Key.Date,
                        Week = row.Key.Week,
                        MonthYear = row.Key.MonthYear,
                        MonthMonth = row.Key.MonthMonth,
                        QuarterYear = row.Key.QuarterYear,
                        QuarterMonth = row.Key.QuarterMonth,
                        YearYear = row.Key.YearYear,
                        Status = row.Key.Status,
                        PaymentMode = row.Key.PaymentMode,
                        ApartmentId = row.Key.DimensionApartmentId,
                        ApartmentName = row.Key.ApartmentName,
                        TenantId = row.Key.TenantId,
                        TenantName = row.Key.TenantName,
                        Nights = row.Key.Nights,
                        City = row.Key.City,
                        GuestNationality = row.Key.GuestNationality
                    }),
                    row.ApartmentId
                })
                .GroupBy(r => BuildGroupKey(r.Key))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.ApartmentId).Distinct().ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<Guid, int>> QueryBlockedDaysByApartmentAsync(
            IReadOnlyList<Guid> apartmentIds,
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            if (apartmentIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var result = new Dictionary<Guid, int>();
            var periodStart = fromInclusive.Date;
            var periodEnd = toExclusive.Date;

            if (!_context.Database.IsRelational())
            {
                var availabilityRows = await _context.ApartmentAvailabilities
                    .AsNoTracking()
                    .Where(aa => apartmentIds.Contains(aa.ApartmentId) && aa.StartDate < DateOnly.FromDateTime(periodEnd) && aa.EndDate >= DateOnly.FromDateTime(periodStart))
                    .Select(aa => new { aa.ApartmentId, aa.StartDate, aa.EndDate })
                    .ToListAsync();

                foreach (var apartmentGroup in availabilityRows.GroupBy(row => row.ApartmentId))
                {
                    var blockedDates = new HashSet<DateOnly>();
                    foreach (var availability in apartmentGroup)
                    {
                        var overlapStart = availability.StartDate > DateOnly.FromDateTime(periodStart) ? availability.StartDate : DateOnly.FromDateTime(periodStart);
                        var overlapEnd = availability.EndDate < DateOnly.FromDateTime(periodEnd.AddDays(-1)) ? availability.EndDate : DateOnly.FromDateTime(periodEnd.AddDays(-1));

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

                    result[apartmentGroup.Key] = blockedDates.Count;
                }

                foreach (var apartmentId in apartmentIds)
                {
                    if (!result.ContainsKey(apartmentId))
                    {
                        result[apartmentId] = 0;
                    }
                }

                return result;
            }

            foreach (var apartmentId in apartmentIds)
            {
                                var blockedDays = await _context.Database
                                        .SqlQuery<int>($@"
WITH RECURSIVE dates AS (
        SELECT DATE({periodStart}) AS d
        UNION ALL
        SELECT DATE_ADD(d, INTERVAL 1 DAY)
        FROM dates
        WHERE d < DATE_SUB(DATE({periodEnd}), INTERVAL 1 DAY)
)
SELECT COUNT(DISTINCT d.d) AS Value
FROM apartment_availability aa
JOIN dates d ON d.d BETWEEN aa.start_date AND aa.end_date
WHERE aa.apartment_id = {apartmentId}
    AND aa.start_date < {periodEnd}
    AND aa.end_date >= {periodStart}
").SingleAsync();

                result[apartmentId] = blockedDays;
            }

            return result;
        }

        private static string BuildGroupKey(BookingReportGroupKey key)
        {
            return string.Join("|", new object?[]
            {
                key.Date,
                key.Week,
                key.Month,
                key.Quarter,
                key.Year,
                key.Status,
                key.PaymentMode,
                key.ApartmentId,
                key.ApartmentName,
                key.TenantId,
                key.TenantName,
                key.Nights,
                key.City,
                key.GuestNationality
            }.Select(value => value?.ToString() ?? "null"));
        }

        private static string BuildGroupApartmentKey(BookingReportGroupKey key, Guid apartmentId)
        {
            return BuildGroupKey(key) + "|" + apartmentId;
        }

        private sealed record BookingReportGroupKey
        {
            public DateTime? Date { get; init; }
            public DateTime? Week { get; init; }
            public DateTime? Month { get; init; }
            public DateTime? Quarter { get; init; }
            public DateTime? Year { get; init; }
            public string? Status { get; init; }
            public string? PaymentMode { get; init; }
            public Guid? ApartmentId { get; init; }
            public string? ApartmentName { get; init; }
            public Guid? TenantId { get; init; }
            public string? TenantName { get; init; }
            public int? Nights { get; init; }
            public string? City { get; init; }
            public string? GuestNationality { get; init; }
        }

        private sealed record BookingReportGroupKeyParts
        {
            public DateTime? Date { get; init; }
            public DateTime? Week { get; init; }
            public int? MonthYear { get; init; }
            public int? MonthMonth { get; init; }
            public int? QuarterYear { get; init; }
            public int? QuarterMonth { get; init; }
            public int? YearYear { get; init; }
            public string? Status { get; init; }
            public string? PaymentMode { get; init; }
            public Guid? ApartmentId { get; init; }
            public string? ApartmentName { get; init; }
            public Guid? TenantId { get; init; }
            public string? TenantName { get; init; }
            public int? Nights { get; init; }
            public string? City { get; init; }
            public string? GuestNationality { get; init; }
        }

        private sealed class BookingReportAggregateRow
        {
            public BookingReportGroupKey Key { get; init; } = new();
            public int BookingCount { get; init; }
            public decimal TotalRevenue { get; init; }
            public decimal AvgBookingValue { get; init; }
            public decimal MinBookingValue { get; init; }
            public decimal MaxBookingValue { get; init; }
            public int PaidBookingCount { get; init; }
            public int ConfirmedBookingCount { get; init; }
            public int CompletedBookingCount { get; init; }
            public int CancelledBookingCount { get; init; }
            public int UniqueTenantCount { get; init; }
            public int UniqueApartmentCount { get; init; }
            public int UniquePaidTenantCount { get; init; }
            public decimal AvgLengthOfStay { get; init; }
            public decimal PackageRevenue { get; init; }
            public int PackageCount { get; init; }
            public int TotalNights { get; init; }
            public int ReviewCount { get; set; }
            public decimal AvgReviewRating { get; set; }
            public decimal AvgBasePrice { get; set; }
            public decimal AvgPriceDelta { get; set; }
            public decimal OccupancyPercent { get; set; }
        }

        private sealed class GroupApartmentNightRow
        {
            public BookingReportGroupKey Key { get; init; } = new();
            public Guid ApartmentId { get; init; }
            public int Nights { get; init; }
        }
    }
}
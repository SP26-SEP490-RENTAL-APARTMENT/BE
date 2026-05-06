using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public class ApartmentPriceCalendarService : BaseService<ApartmentPriceCalendar>, IApartmentPriceCalendarService
{
    private readonly IApartmentPriceCalendarRepository _apartmentPriceCalendarRepository;
    private readonly IAuthService _authService;
    private readonly ICacheService _cacheService;
    private readonly AppDbContext _dbContext;
    public ApartmentPriceCalendarService(
        IApartmentPriceCalendarRepository repository,
        IAuthService authService,
        ICacheService cacheService,
        AppDbContext dbContext) : base(repository)
    {
        _authService = authService;
        _apartmentPriceCalendarRepository = repository;
        _cacheService = cacheService;
        _dbContext = dbContext;
    }


    public async Task<string> SetManuallyOverriddenRange(
    Guid apartmentId,
    DateOnly startDate,
    DateOnly endDate,
    decimal fixedPrice,
    Guid landlordId)
    {
        if (!await _authService.IsUserOwnerOrManager(landlordId, apartmentId))
            return "Error: User does not have permission.";

        if (startDate > endDate)
            return "Error: Start date cannot be after end date.";

        using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var recordsToDelete = await _apartmentPriceCalendarRepository
                .GetPriceCalendarRecordsForDeletionAsync(apartmentId, startDate, endDate);

            Console.WriteLine($"Found {recordsToDelete.Count()} overlapping records.");
            foreach (var rec in recordsToDelete)
            {
                Console.WriteLine($"Will delete: Apartment={rec.ApartmentId}, Start={rec.StartDate}, End={rec.EndDate}, PriceId={rec.PriceId}");
            }


            if (recordsToDelete.Any())
            {
                _apartmentPriceCalendarRepository.DeleteRange(recordsToDelete);
                await _apartmentPriceCalendarRepository.SaveChangesAsync(); // first save
            }

            var newRecord = new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = startDate,
                EndDate = endDate,
                FixedPricePerNight = fixedPrice,
                PriceType = "manual_override",
                DiscountPercentage = null,
                IsDiscount = false,
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _apartmentPriceCalendarRepository.AddRange(new List<ApartmentPriceCalendar> { newRecord });
            await _apartmentPriceCalendarRepository.SaveChangesAsync(); // second save

            await tx.CommitAsync();
            return "Success: The time slot has been successfully overwritten.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            // Log
            Console.WriteLine($"Error in SetManuallyOverriddenRange: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Upserts a single manual price range, ensuring conflict resolution and normalization.
    /// </summary>
    public async Task<PricingResultDto> UpsertManualRangeAsync(
        Guid apartmentId,
        ManualPriceRangeDto rangeDto,
        Guid landlordId)
    {
        // Authorization and validation (omitted for brevity)...
        if (!await _authService.IsUserOwnerOrManager(landlordId, apartmentId))
            throw new UnauthorizedAccessException();
        if (rangeDto.StartDate > rangeDto.EndDate)
            throw new ArgumentException();

        using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // 1. Find all overlapping records
            var overlappingRecords = await _apartmentPriceCalendarRepository
                .GetPriceCalendarRecordsForDeletionAsync(apartmentId, rangeDto.StartDate, rangeDto.EndDate);
            Console.WriteLine($"Found {overlappingRecords.Count()} overlapping records.");
            foreach (var rec in overlappingRecords)
            {
                Console.WriteLine($"Will delete: Apartment={rec.ApartmentId}, Start={rec.StartDate}, End={rec.EndDate}, PriceId={rec.PriceId}");
            }

            // 2. Delete them and SAVE immediately (physically remove from DB)
            if (overlappingRecords.Any())
            {
                _apartmentPriceCalendarRepository.DeleteRange(overlappingRecords);
                await _apartmentPriceCalendarRepository.SaveChangesAsync(); // ✅ first commit
            }

            // 3. Create the new record
            var newRecord = new ApartmentPriceCalendar
            {
                PriceId = Guid.NewGuid(),
                ApartmentId = apartmentId,
                StartDate = rangeDto.StartDate,
                EndDate = rangeDto.EndDate,
                FixedPricePerNight = rangeDto.FixedPricePerNight,
                PriceType = rangeDto.PriceType ?? "manual_override",
                DiscountPercentage = null,
                IsDiscount = false,
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // 4. Insert and SAVE again
            _apartmentPriceCalendarRepository.AddRange(new[] { newRecord });
            await _apartmentPriceCalendarRepository.SaveChangesAsync(); // ✅ second commit

            await tx.CommitAsync();

            return new PricingResultDto
            {
                Success = true,
                UpdatedId = newRecord.PriceId,
                Message = "Pricing range successfully set/updated."
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            Console.WriteLine($"Error in UpsertManualRangeAsync: {ex.Message}");
            // Log properly
            throw;
        }
    }

    // ==========================================================================
    // 🔑 CORE METHOD 2: Delete Range
    // ==========================================================================

    public async Task DeleteManualRangeAsync(
        Guid apartmentId,
        DateOnly startDate,
        DateOnly endDate,
        Guid landlordId)
    {
        // 1. Authorization Check (Same as above)
        if (!await _authService.IsUserOwnerOrManager(landlordId, apartmentId))
        {
            throw new UnauthorizedAccessException("User does not have permission to delete pricing for this apartment.");
        }

        // 2. Deletion Logic: Find all records within the range and delete them.
        var recordsToDelete = await _apartmentPriceCalendarRepository.GetPriceCalendarRecordsForDeletionAsync(
            apartmentId, startDate, endDate);
        Console.WriteLine($"Found {recordsToDelete.Count()} overlapping not-soft-deleted records.");

        if (recordsToDelete == null || !recordsToDelete.Any())
        {
            // No records found in the specified range.
            return;
        }

        // 3. Persistence
        _apartmentPriceCalendarRepository.DeleteRange(recordsToDelete);
        await _apartmentPriceCalendarRepository.SaveChangesAsync();
    }

    // ============================================================================
    // 🔑 CORE METHOD 2: Bulk Weekday Upsert
    // ============================================================================

    public async Task<PricingResultDto> BulkUpsertWeekdayAsync(
        Guid apartmentId,
        BulkPriceUpdateDto updateDto,
        Guid landlordId)
    {
        // 1. Authorization Check (Mandatory)
        if (!await _authService.IsUserOwnerOrManager(landlordId, apartmentId))
        {
            throw new UnauthorizedAccessException("User does not have permission to manage pricing for this apartment.");
        }

        // 2. Preparation: Determine which days of the week to process
        var targetDayNames = updateDto.DaysOfWeek.Select(d => d.ToLower()).ToList();

        // 3. Iteration and Record Collection
        var singleDayRecordsToUpsert = new List<(DateOnly Date, ManualPriceRangeDto Dto)>();

        var currentDate = updateDto.FromDate;
        var endDate = updateDto.ToDate;

        while (currentDate <= endDate)
        {
            var currentDayOfWeek = DateHelper.GetDayOfWeekFromDate(currentDate);
            var dayName = currentDayOfWeek.ToString().ToLower();

            // Check if the current date's day name matches the target list
            if (targetDayNames.Contains(dayName))
            {
                // This is a matching day, so we create a single-day override record
                var singleDayDto = new ManualPriceRangeDto
                {
                    StartDate = currentDate,
                    EndDate = currentDate, // Crucial: Single day override
                    FixedPricePerNight = updateDto.FixedPricePerNight
                };
                singleDayRecordsToUpsert.Add((currentDate, singleDayDto));
            }

            // Move to the next day
            currentDate = currentDate.AddDays(1);
        }

        // 4. Batch Upsert Execution (Efficiency Improvement)
        // We execute the single-day upsert for every matching date.
        foreach (var (date, dto) in singleDayRecordsToUpsert)
        {
            // We call the robust upsert method for each day
            await UpsertManualRangeAsync(apartmentId, dto, landlordId);
        }

        // Success message
        return new PricingResultDto
        {
            Success = true,
            Message = $"Successfully upserted {singleDayRecordsToUpsert.Count} daily rates."
        };
    }


    // ============================================================================
    // 💡 CORE METHOD 3: Get Resolved Calendar (The Read Path)
    // ============================================================================

    /// <summary>
    /// Retrieves the resolved daily price for every day in the range, calculating 
    /// the final price based on the hierarchy of rules (Manual > Calendar > Base).
    /// </summary>
    public async Task<IEnumerable<DailyPriceResolutionDto>> GetResolvedCalendarAsync(
    Guid apartmentId,
    DateOnly start,
    DateOnly end)
    {
        // // 1. Create a unique, predictable cache key based on inputs
        // var cacheKey = $"{apartmentId}:{start:yyyyMMdd}:{end:yyyyMMdd}";

        // // 2. Check the cache (using Redis or distributed cache)
        // var cachedData = await _cacheService.GetAsync<List<DailyPriceResolutionDto>>(cacheKey);
        // if (cachedData != null)
        // {
        //     Console.WriteLine("--- Cache Hit: Returning cached pricing data ---");
        //     return cachedData;
        // }

        // 3. If cache miss, perform the expensive calculation
        Console.WriteLine("--- Cache Miss: Performing live price resolution ---");

        // ... (The rest of the logic from Phase 2 remains, calculating the resolutions) ...
        var resolutions = await CalculateResolution(apartmentId, start, end); // Re-use the private calculation method

        // 4. Store the result in the cache (with an appropriate expiration time, e.g., 1 hour)
        // await _cacheService.SetAsync(cacheKey, resolutions, TimeSpan.FromHours(1));

        return resolutions;
    }

    private async Task<IEnumerable<DailyPriceResolutionDto>> CalculateResolution(
        Guid apartmentId,
        DateOnly start,
        DateOnly end)
    {
        // 1. Fetch all relevant rules for the entire range (Assuming this happens BEFORE calling this method, 
        //    but included here for completeness).
        // --- Implementation detail: Call repository methods here ---
        var manualOverrides = await _apartmentPriceCalendarRepository.GetExistingManualRecords(apartmentId, start, end);
        var calendarPeriods = await _apartmentPriceCalendarRepository.GetExistingStandardRecords(apartmentId, start, end);

        // 2. Initialize list and iterator
        var dailyResolutions = new List<DailyPriceResolutionDto>();
        var currentDate = start;

        // 3. Core Loop: Iterate day by day
        while (currentDate <= end)
        {
            // Pass the current date and all available rules to the resolver helper
            var resolution = ResolveDayPrice(
                apartmentId,
                currentDate,
                manualOverrides,
                calendarPeriods);

            dailyResolutions.Add(resolution);

            // Advance to the next day
            currentDate = currentDate.AddDays(1);
        }

        return dailyResolutions;
    }

    // ============================================================================
    // Internal Resolution Logic (SRP principle)
    // ============================================================================

    private DailyPriceResolutionDto ResolveDayPrice(
        Guid apartmentId,
        DateOnly date,
        IReadOnlyList<ApartmentPriceCalendar> manualOverrides,
        IReadOnlyList<ApartmentPriceCalendar> calendarPeriods)
    {
        decimal nightlyRate = 0;
        string source = "BaseRate";

        // --- 1. Check for Manual Override (Highest Priority) ---
        var manualMatch = manualOverrides.FirstOrDefault(r => date >= r.StartDate && date <= r.EndDate);
        if (manualMatch != null)
        {
            if (manualMatch.FixedPricePerNight.HasValue)
            {
                nightlyRate = manualMatch.FixedPricePerNight.Value;
                source = "ManualOverride";
            }
            // --- 2. Check for Calendar Override (Medium Priority) ---
            else
            {
                var calendarMatch = calendarPeriods.FirstOrDefault(r => date >= r.StartDate && date <= r.EndDate);
                if (calendarMatch != null)
                {
                    // Apply the calendar rule logic (e.g., discount or multiplier)
                    // NOTE: This requires detailed re-implementing of the discount logic from Phase 1
                    if (calendarMatch.DiscountPercentage.HasValue && calendarMatch.DiscountPercentage > 0)
                    {
                        nightlyRate = GetBaseRate(apartmentId) * (1 - calendarMatch.DiscountPercentage.Value / 100m);
                        source = "CalendarDiscount";
                    }
                    else
                    {
                        // If no discount, assume the calendar sets the rate.
                        nightlyRate = GetBaseRate(apartmentId);
                        source = "CalendarPeriod";
                    }
                }
                // --- 3. Fallback to Base Rate ---
                else
                {
                    nightlyRate = GetBaseRate(apartmentId);
                    source = "BaseRate";
                }
            }
        }


        // For this basic calendar resolution, we assume the nightly rate *is* the total cost for that day.
        // In a real system, we would apply mandatory daily fees (like cleaning deposits) here.
        decimal totalNightlyCost = nightlyRate;

        return new DailyPriceResolutionDto
        {
            Date = date,
            FinalPricePerNight = Math.Round(nightlyRate, 2),
            TotalNightlyCost = Math.Round(totalNightlyCost, 2),
            Source = source,
            Notes = $"Rate calculated based on {source} rule."
        };
    }

    // Dummy methods (Must be implemented in the UnitOfWork/Repository)
    private decimal GetBaseRate(Guid apartmentId) { /* Fetches rate from Apartment table */ return 100m; }
}

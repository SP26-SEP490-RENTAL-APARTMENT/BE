using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using DAL.Repository.Interfaces;
using DAL.Models;

public class PricingDataOptimizerService
{
    private readonly IApartmentPriceCalendarRepository _repository;

    public PricingDataOptimizerService(
        IApartmentPriceCalendarRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Runs the cleanup process by identifying and merging overlapping records 
    /// within a specific criteria period (e.g., 30 days).
    /// </summary>
    /// <param name="apartmentId">The ID of the apartment to normalize.</param>
    /// <param name="cutoffDate">The date up to which records should be checked.</param>
    /// <returns>The number of redundant records removed.</returns>
    public async Task<int> OptimizeAndCleanupAsync(Guid apartmentId, DateTime cutoffDate)
    {
        // 1. Fetch Records
        // Fetch all records up to the cutoff date that are candidates for cleanup.
        var existingRecords = await _repository.GetPriceCalendarRecordsForDeletionAsync(
            apartmentId, new DateOnly(cutoffDate.Year, cutoffDate.Month, cutoffDate.Day),
            new DateOnly(cutoffDate.Year, cutoffDate.Month, cutoffDate.Day));

        if (existingRecords == null || !existingRecords.Any())
        {
            Console.WriteLine("No records found to process.");
            return 0;
        }

        // 2. Sort Records
        var sortedRecords = existingRecords.OrderBy(r => r.StartDate).ToList();

        // 3. Algorithm Core: Detect and Merge
        var consolidatedRecords = new List<ApartmentPriceCalendar>();

        if (sortedRecords.Any())
        {
            // Start with the first record as the initial consolidated state.
            // We must create a *copy* of the entity to modify it safely.
            var currentConsolidated = CloneToCalendar(sortedRecords[0]);

            for (int i = 1; i < sortedRecords.Count; i++)
            {
                var nextRecord = sortedRecords[i];

                // Check for Overlap: 
                // Overlap occurs if the current consolidated end date is >= the next record's start date.
                if (currentConsolidated.EndDate >= nextRecord.StartDate)
                {
                    // Merge Logic:

                    // A. Update End Date: Take the latest end date.
                    currentConsolidated.EndDate = DateHelper.Max(currentConsolidated.EndDate, nextRecord.EndDate);

                    // B. Update Price: Take the highest price to ensure no gap in coverage.
                    // This assumes price is the key metric for determining continuity.
                    if (nextRecord.FixedPricePerNight.HasValue)
                    {
                        // Logic for merging prices (assuming high price wins)
                        if (currentConsolidated.FixedPricePerNight == null ||
                            nextRecord.FixedPricePerNight > currentConsolidated.FixedPricePerNight)
                        {
                            currentConsolidated.FixedPricePerNight = nextRecord.FixedPricePerNight;
                        }
                    }
                    // Note: In a real system, you might also need to recalculate PriceType and MinNights.
                }
                else
                {
                    // No Overlap: Finalize the current consolidated record and start a new one.
                    consolidatedRecords.Add(currentConsolidated);

                    // Start the new consolidated record
                    currentConsolidated = CloneToCalendar(nextRecord);
                }
            }
            // Don't forget to add the very last consolidated record after the loop ends
            consolidatedRecords.Add(currentConsolidated);
        }

        // 4. Database Persistence (Transaction required!)
        var recordsToKeep = consolidatedRecords;
        var recordsToDelete = existingRecords;

        // Execute the entire operation within a single transaction for atomicity.
        _repository.DeleteRange(recordsToDelete);
        _repository.AddRange(recordsToKeep);
        await _repository.SaveChangesAsync(); // Single atomic save

        return existingRecords.Count() - recordsToKeep.Count();
    }

    /// <summary>
    /// Clones the necessary fields from an existing calendar record to create a state object for merging.
    /// </summary>
    private ApartmentPriceCalendar CloneToCalendar(ApartmentPriceCalendar original)
    {
        // Important: Create a new instance to prevent modification side effects
        return new ApartmentPriceCalendar
        {
            PriceId = original.PriceId, // Keep original ID for tracking
            ApartmentId = original.ApartmentId,
            StartDate = original.StartDate,
            EndDate = original.EndDate,
            FixedPricePerNight = original.FixedPricePerNight,
            PriceType = original.PriceType,
            // Initialize other fields as necessary
            DiscountPercentage = original.DiscountPercentage,
            MinNights = original.MinNights,
            // ... other necessary fields
            CreatedAt = original.CreatedAt,
            UpdatedAt = DateTime.Now,
            Apartment = original.Apartment
        };
    }
}

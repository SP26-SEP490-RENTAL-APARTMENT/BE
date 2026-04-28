// DTO for input when setting a single range
public class ManualPriceRangeDto
{
    // REQUIRED INPUTS
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // The absolute price that overrides everything else
    public decimal FixedPricePerNight { get; set; } 

    // The source of the pricing (Landlord, System, etc.)
    public string? RuleSource { get; set; } 

    // Optional business rules
    public int? MinNights { get; set; }
    public string? PriceType { get; set; } = "manual_override"; 
}

// DTO for the API response
public class PricingResultDto
{
    public bool Success { get; set; }
    public Guid UpdatedId { get; set; }
    public string Message { get; set; }
}

public class DailyPriceResolutionDto
{
    public DateOnly Date { get; set; }
    public decimal FinalPricePerNight { get; set; }
    public decimal TotalNightlyCost { get; set; }
    public string Source { get; set; } // e.g., "BaseRate", "CalendarOverride", "ManualInput"
    public string? Notes { get; set; }
}
public class BulkPriceUpdateDto
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    // Example: ["Mon", "Tue", "Wed"]
    public List<string> DaysOfWeek { get; set; } = new List<string>();
    public decimal FixedPricePerNight { get; set; }
}


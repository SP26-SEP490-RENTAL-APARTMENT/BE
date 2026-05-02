// DTO for input when setting a single range
public class ManualPriceRangeDto
{
    // REQUIRED INPUTS
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // The absolute price that overrides everything else
    public decimal FixedPricePerNight { get; set; } 
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
    public decimal FixedPricePerNight { get; set; }
}


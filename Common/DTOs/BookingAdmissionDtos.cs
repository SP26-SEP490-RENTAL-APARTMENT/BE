namespace Common.DTOs;

public class BookingAdmissionEvaluationDto
{
    public bool Allowed { get; set; }

    public string? BlockReason { get; set; }

    public decimal OutstandingAmount { get; set; }

    public int UnpaidBookingCount { get; set; }

    public DateTime? GraceWindowExpiry { get; set; }

    public bool IsInsideGraceWindow { get; set; }

    public bool SelectedPaymentModeAllowed { get; set; }

    public string? SelectedPaymentMode { get; set; }

    public double? OldestUnpaidBookingAgeHours { get; set; }
}
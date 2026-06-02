namespace Common.DTOs;

public class AppSettingsDto
{
    public BookingCheckTimeSettingsDto BookingCheckTimeSettings { get; set; } = new();
    public BookingAdmissionPolicyDto BookingAdmissionPolicy { get; set; } = new();
    public OccupiedRoomAlternativesDto OccupiedRoomAlternatives { get; set; } = new();
    public BookingSettingsDto Booking { get; set; } = new();
}

public class BookingCheckTimeSettingsDto
{
    public double EarlyCheckInFeePercentOfDaily { get; set; }
    public double LateCheckOutFeePercentPerHour { get; set; }
    public double LateCheckOutFeeCapPercentOfDaily { get; set; }
    public int CorrectionWindowHours { get; set; }
    public int TenantResponseSilenceHours { get; set; }
    public int NoShowGraceHours { get; set; }
    public int MissingCheckOutGraceHours { get; set; }
    public int ClosedWithoutCheckOutHours { get; set; }
    public int AutomationPollIntervalSeconds { get; set; }
    public int FeeSettlementGraceDays { get; set; }
}

public class BookingAdmissionPolicyDto
{
    public int GraceWindowHours { get; set; }
    public int MaxSimultaneousUnpaidConfirmedBookings { get; set; }
    public List<string> AllowedPaymentModesWhenDebtExists { get; set; } = new();
}

public class OccupiedRoomAlternativesDto
{
    public int DefaultRadiusMeters { get; set; }
}

public class BookingSettingsDto
{
    public double OccupiedIncidentPenaltyRate { get; set; }
}

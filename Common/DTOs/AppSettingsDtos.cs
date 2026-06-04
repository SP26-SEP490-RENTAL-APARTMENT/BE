using System.ComponentModel.DataAnnotations;

namespace Common.DTOs;

public class AppSettingsDto
{
    [Required] public BookingCheckTimeSettingsDto BookingCheckTimeSettings { get; set; } = new();
    [Required] public BookingAdmissionPolicyDto BookingAdmissionPolicy { get; set; } = new();
    [Required] public OccupiedRoomAlternativesDto OccupiedRoomAlternatives { get; set; } = new();
    [Required] public BookingSettingsDto Booking { get; set; } = new();
}

public class BookingCheckTimeSettingsDto
{
    [Range(0.0, 1.0)] public double EarlyCheckInFeePercentOfDaily { get; set; }
    [Range(0.0, 1.0)] public double LateCheckOutFeePercentPerHour { get; set; }
    [Range(0.0, 1.0)] public double LateCheckOutFeeCapPercentOfDaily { get; set; }
    [Range(1, 168)] public int CorrectionWindowHours { get; set; }
    [Range(1, 168)] public int TenantResponseSilenceHours { get; set; }
    [Range(1, 48)] public int NoShowGraceHours { get; set; }
    [Range(1, 48)] public int MissingCheckOutGraceHours { get; set; }
    [Range(1, 168)] public int ClosedWithoutCheckOutHours { get; set; }
    [Range(30, 3600)] public int AutomationPollIntervalSeconds { get; set; }
    [Range(0, 30)] public int FeeSettlementGraceDays { get; set; }
}

public class BookingAdmissionPolicyDto
{
    [Range(0, 168)] public int GraceWindowHours { get; set; }
    [Range(1, 20)] public int MaxSimultaneousUnpaidConfirmedBookings { get; set; }
    [Required, MinLength(1)] public List<string> AllowedPaymentModesWhenDebtExists { get; set; } = new();
}

public class OccupiedRoomAlternativesDto
{
    [Range(100, 50000)] public int DefaultRadiusMeters { get; set; }
}

public class BookingSettingsDto
{
    [Range(0.0, 10.0)] public double OccupiedIncidentPenaltyRate { get; set; }
}

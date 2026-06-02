namespace Common.Settings;

public class BookingCheckTimeSettings
{
    public const string SectionName = "BookingCheckTimeSettings";

    public double EarlyCheckInFeePercentOfDaily { get; set; } = 0.5;
    public double LateCheckOutFeePercentPerHour { get; set; } = 0.025;
    public double LateCheckOutFeeCapPercentOfDaily { get; set; } = 0.25;
    public int CorrectionWindowHours { get; set; } = 24;
    public int TenantResponseSilenceHours { get; set; } = 24;
    public int NoShowGraceHours { get; set; } = 4;
    public int MissingCheckOutGraceHours { get; set; } = 6;
    public int ClosedWithoutCheckOutHours { get; set; } = 24;
    public int AutomationPollIntervalSeconds { get; set; } = 300;
    public int FeeSettlementGraceDays { get; set; } = 3;
}
namespace Common.Settings;

public class BookingAdmissionPolicySettings
{
    public const string SectionName = "BookingAdmissionPolicy";

    public int GraceWindowHours { get; set; } = 24;

    public int MaxSimultaneousUnpaidConfirmedBookings { get; set; } = 1;

    public List<string> AllowedPaymentModesWhenDebtExists { get; set; } = new() { "full" };
}
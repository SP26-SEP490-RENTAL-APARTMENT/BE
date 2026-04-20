namespace MoMoApi;

public class MomoOptions
{
    public const string SectionName = "Momo";

    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string IpnUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://test-payment.momo.vn";

    // Disbursement v2 configuration
    public string DisbursementIpnUrl { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public long? OrderGroupId { get; set; }

    public int PaymentStatusQueryAfterSeconds { get; set; } = 30;
    public double PaymentStatusRetryDelayMinutes { get; set; } = 0.5;
}

namespace Common.Settings
{
    public class PayOsOptions
    {
        public const string SectionName = "PayOs";
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? WebhookSecret { get; set; }
        public string? RedirectUrl { get; set; }
        public string? RedirectUrlAndroid { get; set; }
        public string? RedirectUrlIos { get; set; }
    }
}

namespace Common.Settings;

public class FptIdRecognitionOptions
{
    public const string SectionName = "FptIdRecognition";

    public string Endpoint { get; set; } = "https://api.fpt.ai/vision/idr/vnm/";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public double AutoApproveConfidenceThreshold { get; set; } = 0.90;
}

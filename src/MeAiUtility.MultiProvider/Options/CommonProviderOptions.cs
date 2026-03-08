namespace MeAiUtility.MultiProvider.Options;

public sealed class CommonProviderOptions
{
    public float? DefaultTemperature { get; set; } = 0.7f;
    public int? DefaultMaxTokens { get; set; } = 1000;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public bool EnableTelemetry { get; set; } = true;
    public bool CapturePrompts { get; set; }
    public bool LogRequestResponse { get; set; }
    public bool MaskSensitiveData { get; set; } = true;
}

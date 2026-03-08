namespace MeAiUtility.MultiProvider.Options;

public sealed class ProviderOverrideOptions
{
    public string Type { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>APIキー（機密情報）。ログ出力時は <see cref="MeAiUtility.MultiProvider.Telemetry.LoggingExtensions.MaskSensitive"/> でマスクすること。</summary>
    public string? ApiKey { get; set; }

    /// <summary>ベアラートークン（機密情報）。ログ出力時は <see cref="MeAiUtility.MultiProvider.Telemetry.LoggingExtensions.MaskSensitive"/> でマスクすること。</summary>
    public string? BearerToken { get; set; }

    public string? AzureApiVersion { get; set; }
}

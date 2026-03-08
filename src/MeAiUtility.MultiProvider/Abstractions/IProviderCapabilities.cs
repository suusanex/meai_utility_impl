namespace MeAiUtility.MultiProvider.Abstractions;

public enum FeatureName
{
    Streaming,
    ReasoningEffort,
    ModelDiscovery,
    Embeddings,
    ProviderOverride,
    ExtensionParameters,
}

public interface IProviderCapabilities
{
    bool SupportsReasoningEffort { get; }
    bool SupportsStreaming { get; }
    bool SupportsModelDiscovery { get; }
    bool SupportsEmbeddings { get; }
    bool SupportsProviderOverride { get; }
    bool SupportsExtensionParameters { get; }
    bool IsSupported(FeatureName featureName);
}

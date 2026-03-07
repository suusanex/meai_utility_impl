using MeAiUtility.MultiProvider.Abstractions;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Configuration;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, Type> _providers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string providerName, Type implementationType)
    {
        _providers[providerName] = implementationType;
    }

    public Type Resolve(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var type))
        {
            throw new InvalidOperationException($"Provider '{providerName}' is not registered.");
        }

        return type;
    }

    public void ValidateCapabilities(string providerName, IProviderCapabilities capabilities)
    {
        if (!capabilities.IsSupported(FeatureName.Streaming))
        {
            throw new InvalidOperationException($"Provider '{providerName}' must support streaming.");
        }
    }
}

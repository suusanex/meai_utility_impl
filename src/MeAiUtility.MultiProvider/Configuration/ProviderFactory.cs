using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MeAiUtility.MultiProvider.Configuration;

public sealed class ProviderFactory(IServiceProvider serviceProvider, ProviderRegistry registry, IOptions<MultiProviderOptions> options) : IProviderFactory
{
    public IChatClient Create()
    {
        options.Value.Validate();
        if (!registry.TryResolve(options.Value.Provider, out var implementationType))
        {
            implementationType = DiscoverProviderType(options.Value.Provider);
            registry.Register(options.Value.Provider, implementationType);
        }

        var client = serviceProvider.GetService(implementationType) as IChatClient;
        if (client is null)
        {
            throw new InvalidOperationException($"Provider '{options.Value.Provider}' could not be resolved.");
        }

        if (client is IProviderCapabilities capabilities)
        {
            registry.ValidateCapabilities(options.Value.Provider, capabilities);
        }

        return client;
    }

    private static Type DiscoverProviderType(string provider)
    {
        var candidates = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(IChatClient).IsAssignableFrom(t))
            .ToArray();

        var preferredName = provider switch
        {
            "OpenAI" => "OpenAIChatClientAdapter",
            "OpenAICompatible" => "OpenAICompatibleProvider",
            "AzureOpenAI" => "AzureOpenAIChatClientAdapter",
            "GitHubCopilot" => "GitHubCopilotChatClient",
            _ => provider,
        };

        var match = candidates.FirstOrDefault(t => string.Equals(t.Name, preferredName, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(t => t.Name.Contains(provider, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new InvalidOperationException($"Provider '{provider}' is not registered.");
        }

        return match;
    }
}

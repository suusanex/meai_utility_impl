using MeAiUtility.MultiProvider.Abstractions;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MeAiUtility.MultiProvider.Configuration;

public static class ProviderConfigurationExtensions
{
    public static IServiceCollection AddMultiProviderChat(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MultiProviderOptions>()
            .Bind(configuration.GetSection(MultiProviderOptions.SectionName))
            .PostConfigure(options => options.Validate());

        services.TryAddSingleton<ProviderRegistry>();
        services.TryAddSingleton<IProviderFactory, ProviderFactory>();
        services.TryAddSingleton<IChatClient>(sp => sp.GetRequiredService<IProviderFactory>().Create());
        return services;
    }

    public static IServiceCollection AddMultiProviderChat(this IServiceCollection services, Action<MultiProviderOptions> configure)
    {
        services.AddOptions<MultiProviderOptions>()
            .Configure(configure)
            .PostConfigure(options => options.Validate());

        services.TryAddSingleton<ProviderRegistry>();
        services.TryAddSingleton<IProviderFactory, ProviderFactory>();
        services.TryAddSingleton<IChatClient>(sp => sp.GetRequiredService<IProviderFactory>().Create());
        return services;
    }
}

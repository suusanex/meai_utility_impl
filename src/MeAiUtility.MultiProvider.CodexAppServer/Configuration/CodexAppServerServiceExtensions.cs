using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Options;
using MeAiUtility.MultiProvider.CodexAppServer.Stdio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.CodexAppServer.Configuration;

public static class CodexAppServerServiceExtensions
{
    public static IServiceCollection AddCodexAppServer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection("MultiProvider:CodexAppServer").Get<CodexAppServerProviderOptions>()
            ?? new CodexAppServerProviderOptions();

        services.AddSingleton(options);
        services.AddSingleton<ICodexProcessRunner, SystemCodexProcessRunner>();
        services.AddSingleton<ICodexTransportFactory, DefaultCodexTransportFactory>();
        services.AddSingleton<CodexAppServerChatClient>();
        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiCodingAgentFacade.CodexAppServer.Abstractions;
using MultiCodingAgentFacade.CodexAppServer.Options;
using MultiCodingAgentFacade.CodexAppServer.Stdio;
using MultiCodingAgentFacade.CodexAppServer.Threading;

namespace MultiCodingAgentFacade.CodexAppServer.Configuration;

public static class CodexAppServerServiceExtensions
{
    public static IServiceCollection AddCodexAppServerAgentRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection("MultiCodingAgentFacade:CodexAppServer").Get<CodexAppServerOptions>()
            ?? new CodexAppServerOptions();

        services.AddSingleton(options);
        services.AddSingleton<ICodexProcessRunner, SystemCodexProcessRunner>();
        services.AddSingleton<ICodexTransportFactory, DefaultCodexTransportFactory>();
        services.AddSingleton<ICodexThreadStore, FileCodexThreadStore>();
        services.AddSingleton<ICodexThreadRegistry, CodexThreadRegistry>();
        services.AddSingleton<CodexAppServerAgentClient>(serviceProvider =>
            new CodexAppServerAgentClient(
                serviceProvider.GetRequiredService<CodexAppServerOptions>(),
                serviceProvider.GetRequiredService<ICodexTransportFactory>(),
                serviceProvider.GetRequiredService<ICodexThreadStore>(),
                serviceProvider.GetRequiredService<ILogger<CodexAppServerAgentClient>>(),
                serviceProvider.GetRequiredService<ILoggerFactory>()));

        return services;
    }
}

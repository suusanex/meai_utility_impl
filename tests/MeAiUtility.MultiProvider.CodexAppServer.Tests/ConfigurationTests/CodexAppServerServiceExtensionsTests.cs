using MeAiUtility.MultiProvider.CodexAppServer;
using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.ConfigurationTests;

public class CodexAppServerServiceExtensionsTests
{
    [Test]
    public void AddCodexAppServer_RegistersSingletonServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddCodexAppServer(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var firstClient = provider.GetRequiredService<CodexAppServerChatClient>();
        var secondClient = provider.GetRequiredService<CodexAppServerChatClient>();
        var firstFactory = provider.GetRequiredService<ICodexTransportFactory>();
        var secondFactory = provider.GetRequiredService<ICodexTransportFactory>();

        Assert.That(secondClient, Is.SameAs(firstClient));
        Assert.That(secondFactory, Is.SameAs(firstFactory));
    }

    [Test]
    public void AddCodexAppServer_ThrowsOnNullConfiguration()
    {
        var services = new ServiceCollection();
        Assert.That(() => services.AddCodexAppServer(null!), Throws.TypeOf<ArgumentNullException>());
    }

    private static IConfiguration BuildConfiguration()
    {
        var configuration = new ConfigurationManager();
        var values = new Dictionary<string, string?>
        {
            ["MultiProvider:CodexAppServer:CodexCommand"] = "codex",
            ["MultiProvider:CodexAppServer:Transport"] = "stdio",
            ["MultiProvider:CodexAppServer:ApprovalPolicy"] = "never",
            ["MultiProvider:CodexAppServer:SandboxMode"] = "workspace-write",
        };

        foreach (var kv in values)
        {
            configuration[kv.Key] = kv.Value;
        }

        return configuration;
    }
}

using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.GitHubCopilot.Configuration;
using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.Samples;

public static class GitHubCopilotSampleHost
{
    public static IHost CreateHost(string configurationPath)
    {
        var fullPath = ResolveConfigurationPath(configurationPath);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(fullPath, optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();
        ConfigureServices(builder.Services, builder.Configuration);
        return builder.Build();
    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration[$"{MultiProviderOptions.SectionName}:Provider"];
        if (!string.Equals(provider, "GitHubCopilot", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This sample only supports the GitHubCopilot provider.");
        }

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddMultiProviderChat(configuration);
        services.AddGitHubCopilot(configuration);
    }

    private static string ResolveConfigurationPath(string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            throw new ArgumentException("Configuration path must not be empty.", nameof(configurationPath));
        }

        var fullPath = Path.GetFullPath(configurationPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Configuration file was not found.", fullPath);
        }

        return fullPath;
    }
}

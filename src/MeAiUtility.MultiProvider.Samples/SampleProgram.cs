using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.Samples;

internal static class SampleProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Ensure console I/O uses UTF-8 to avoid mojibake on non-ASCII output.
        // 日本語の出力が文字化けしないように、起動時にコンソールの入出力エンコーディングを UTF-8 に設定する。
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        var options = ParseArguments(args);
        if (options.ShowHelp)
        {
            await Console.Out.WriteLineAsync(GetUsage());
            return 0;
        }

        if (!options.ChatMode)
        {
            Console.WriteLine(await ProviderSwitchSample.RunAsync("GitHubCopilot"));
            return 0;
        }

        var configurationPath = options.ConfigurationPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var host = GitHubCopilotSampleHost.CreateHost(configurationPath);
        var chatClient = host.Services.GetRequiredService<IChatClient>();
        await InteractiveChatSample.RunAsync(chatClient, Console.In, Console.Out);
        return 0;
    }

    private static SampleCommandLineOptions ParseArguments(string[] args)
    {
        string? configurationPath = null;
        var chatMode = false;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--chat":
                    chatMode = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--config":
                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --config.");
                    }

                    configurationPath = args[++index];
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        return new SampleCommandLineOptions(chatMode, showHelp, configurationPath);
    }

    private static string GetUsage() =>
        """
        MeAiUtility.MultiProvider.Samples

          --chat           Start interactive chat mode.
          --config <path>  Load configuration from the specified appsettings.json.
          --help           Show this help text.

        Without --chat, the legacy provider switch sample is executed.
        """;

    private sealed record SampleCommandLineOptions(bool ChatMode, bool ShowHelp, string? ConfigurationPath);
}

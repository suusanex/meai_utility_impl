using MeAiUtility.MultiProvider.CodexAppServer.Abstractions;
using MeAiUtility.MultiProvider.CodexAppServer.Stdio;

namespace MeAiUtility.MultiProvider.CodexAppServer.Tests.Stdio;

public class SystemCodexProcessRunnerTests
{
    [Test]
    public async Task StartAsync_ResolvesCmdFromPath_WhenCommandHasNoExtension()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("This test verifies Windows command resolution behavior.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "meai-codex-runner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var commandPath = Path.Combine(tempDirectory, "fakecodex.cmd");
            await File.WriteAllTextAsync(commandPath, "@echo off\r\necho fake-codex\r\n");

            var startInfo = new CodexProcessStartInfo
            {
                Command = "fakecodex",
                Arguments = [],
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PATH"] = tempDirectory,
                },
            };

            var runner = new SystemCodexProcessRunner();
            using var process = await runner.StartAsync(startInfo);
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.That(process.ExitCode, Is.EqualTo(0));
            Assert.That(stdout, Does.Contain("fake-codex"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}

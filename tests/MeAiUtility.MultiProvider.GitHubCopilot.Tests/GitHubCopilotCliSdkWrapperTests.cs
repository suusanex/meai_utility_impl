using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotCliSdkWrapperTests
{
    [Test]
    public async Task SendAsync_PreservesMultilinePrompt_WhenCliPathIsNpmPowerShellShim()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("This test requires Windows PowerShell.");
        }

        var rootDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"fake-copilot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(rootDirectory, "node_modules", "@github", "copilot"));
        var scriptPath = Path.Combine(rootDirectory, "copilot.ps1");
        var loaderPath = Path.Combine(rootDirectory, "node_modules", "@github", "copilot", "npm-loader.js");

        await File.WriteAllTextAsync(
            scriptPath,
            """
# このモック PowerShell shim は npm loader の痕跡を含め、
# wrapper が npm インストール版 Copilot CLI shim として認識できるようにする。
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CopilotArgs
)

$promptIndex = [Array]::IndexOf($CopilotArgs, '-p')
if ($promptIndex -lt 0 -or ($promptIndex + 1) -ge $CopilotArgs.Length) {
    Write-Error 'missing -p'
    exit 1
}

Write-Output $CopilotArgs[$promptIndex + 1]
""");
        await File.WriteAllTextAsync(
            loaderPath,
            """
// wrapper は npm インストール版 Copilot CLI shim と判定する前に
// loader の実在を確認するため、このファイルをテスト用に配置する。
""");

        try
        {
            var sut = new GitHubCopilotCliSdkWrapper(new GitHubCopilotProviderOptions
            {
                CliPath = scriptPath,
                TimeoutSeconds = 30,
            });

            var prompt = """
You design Windows desktop UI automation scenarios.
Return exactly one JSON object.
line-3 "quoted"
""";

            var response = await sut.SendAsync(prompt, new CopilotSessionConfig { ModelId = "gpt-5-mini" });

            Assert.That(response, Does.Contain("You design Windows desktop UI automation scenarios."));
            Assert.That(response, Does.Contain("Return exactly one JSON object."));
            Assert.That(response, Does.Contain("line-3"));
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Test]
    public void SendAsync_Throws_WhenCliReturnsOnlyStandardError()
    {
        var rootDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"fake-copilot-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
        var scriptPath = CreateStandardErrorOnlyCli(rootDirectory);

        try
        {
            var sut = new GitHubCopilotCliSdkWrapper(new GitHubCopilotProviderOptions
            {
                CliPath = scriptPath,
                TimeoutSeconds = 30,
            });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await sut.SendAsync("prompt", new CopilotSessionConfig { ModelId = "gpt-5-mini" }));

            Assert.That(ex!.Message, Does.Contain("returned no output"));
            Assert.That(ex.Message, Does.Contain("synthetic stderr failure"));
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static string CreateStandardErrorOnlyCli(string rootDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(rootDirectory, "copilot.cmd");
            File.WriteAllText(
                scriptPath,
                """
@echo off
echo synthetic stderr failure 1>&2
exit /b 0
""");
            return scriptPath;
        }

        var unixScriptPath = Path.Combine(rootDirectory, "copilot");
        File.WriteAllText(
            unixScriptPath,
            """
#!/bin/sh
echo "synthetic stderr failure" 1>&2
exit 0
""");
        File.SetUnixFileMode(
            unixScriptPath,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
        return unixScriptPath;
    }
}

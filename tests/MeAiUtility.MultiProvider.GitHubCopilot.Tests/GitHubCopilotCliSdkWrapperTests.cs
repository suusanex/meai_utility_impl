using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;

namespace MeAiUtility.MultiProvider.GitHubCopilot.Tests;

public class GitHubCopilotCliSdkWrapperTests
{
    [Test]
    public async Task SendAsync_PreservesMultilinePrompt_WhenCliPathIsNpmPowerShellShim()
    {
        var rootDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"fake-copilot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(rootDirectory, "node_modules", "@github", "copilot"));
        var scriptPath = Path.Combine(rootDirectory, "copilot.ps1");
        var loaderPath = Path.Combine(rootDirectory, "node_modules", "@github", "copilot", "npm-loader.js");

        await File.WriteAllTextAsync(
            scriptPath,
            """
$basedir = Split-Path $MyInvocation.MyCommand.Definition -Parent
& "node.exe" "$basedir/node_modules/@github/copilot/npm-loader.js" $args
exit $LASTEXITCODE
""");
        await File.WriteAllTextAsync(
            loaderPath,
            """
const args = process.argv.slice(2);
const promptIndex = args.indexOf('-p');
if (promptIndex < 0 || promptIndex + 1 >= args.length) {
  console.error('missing -p');
  process.exit(1);
}

console.log(args[promptIndex + 1]);
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
        Directory.CreateDirectory(Path.Combine(rootDirectory, "node_modules", "@github", "copilot"));
        var scriptPath = Path.Combine(rootDirectory, "copilot.ps1");
        var loaderPath = Path.Combine(rootDirectory, "node_modules", "@github", "copilot", "npm-loader.js");

        File.WriteAllText(
            scriptPath,
            """
$basedir = Split-Path $MyInvocation.MyCommand.Definition -Parent
& "node.exe" "$basedir/node_modules/@github/copilot/npm-loader.js" $args
exit $LASTEXITCODE
""");
        File.WriteAllText(
            loaderPath,
            """
console.error('synthetic stderr failure');
process.exit(0);
""");

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
}

using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;
using MeAiUtility.MultiProvider.GitHubCopilot.Options;
using MeAiUtility.MultiProvider.Telemetry;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.GitHubCopilot;

public sealed class CopilotClientHost(ICopilotSdkWrapper sdkWrapper, GitHubCopilotProviderOptions options, ILogger<CopilotClientHost> logger)
{
    public ICopilotSdkWrapper Wrapper => sdkWrapper;

    public async Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("GitHub Copilot model list start. Stage=model list start");
            var models = await sdkWrapper.ListModelsAsync(cancellationToken);
            logger.LogDebug("GitHub Copilot model list completed. Stage=model list completed; Count={Count}", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            logger.LogError("GitHub Copilot model list failed. Stage=model list failed");
            var traceId = Guid.NewGuid().ToString("N");
            logger.LogExceptionWithTrace(ex, traceId);
            throw new CopilotRuntimeException(
                "Failed to list Copilot models.",
                "GitHubCopilot",
                options.CliPath,
                null,
                traceId,
                ex,
                MeAiUtility.MultiProvider.Options.CopilotOperation.ListModels);
        }
    }
}

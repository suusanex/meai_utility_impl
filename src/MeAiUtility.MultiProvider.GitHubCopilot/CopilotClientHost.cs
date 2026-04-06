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
            return await sdkWrapper.ListModelsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
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

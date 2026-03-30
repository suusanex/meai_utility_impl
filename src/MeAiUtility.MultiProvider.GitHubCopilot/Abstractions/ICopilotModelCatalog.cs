namespace MeAiUtility.MultiProvider.GitHubCopilot.Abstractions;

public interface ICopilotModelCatalog
{
    Task<IReadOnlyList<CopilotModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
}

using MultiCodingAgentFacade.GitHubCopilot;
using Xunit;

namespace MultiCodingAgentFacade.GitHubCopilot.Tests;

public sealed class GitHubCopilotRuntimeMarkerTests
{
    [Fact]
    public void RuntimeNameIsGitHubCopilot()
    {
        Assert.Equal("GitHubCopilot", GitHubCopilotRuntimeMarker.RuntimeName);
    }
}

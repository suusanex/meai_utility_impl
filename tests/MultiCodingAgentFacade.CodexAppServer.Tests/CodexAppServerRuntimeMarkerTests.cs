using MultiCodingAgentFacade.CodexAppServer;
using Xunit;

namespace MultiCodingAgentFacade.CodexAppServer.Tests;

public sealed class CodexAppServerRuntimeMarkerTests
{
    [Fact]
    public void RuntimeNameIsCodexAppServer()
    {
        Assert.Equal("CodexAppServer", CodexAppServerRuntimeMarker.RuntimeName);
    }
}

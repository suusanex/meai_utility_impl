using MultiCodingAgentFacade.Core.Exceptions;
using Xunit;

namespace MultiCodingAgentFacade.Core.Tests;

public sealed class RuntimeFacadeExceptionTests
{
    [Fact]
    public void ConstructorStoresRuntimeName()
    {
        var exception = new RuntimeFacadeException("failure", "CodexAppServer", traceId: "trace-1");

        Assert.Equal("CodexAppServer", exception.RuntimeName);
        Assert.Equal("trace-1", exception.TraceId);
    }
}

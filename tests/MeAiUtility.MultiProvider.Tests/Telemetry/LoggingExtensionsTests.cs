using Microsoft.Extensions.Logging;
using Moq;
using MeAiUtility.MultiProvider.Telemetry;

namespace MeAiUtility.MultiProvider.Tests.Telemetry;

public class LoggingExtensionsTests
{
    [Test]
    public void MaskSensitive_ReturnsMaskedValue()
    {
        var masked = LoggingExtensions.MaskSensitive("secret");
        Assert.That(masked, Is.EqualTo("***MASKED***"));
    }

    [Test]
    public void LogHttpError_LogsStatusAndBody()
    {
        var mock = new Mock<ILogger>();
        mock.Object.LogHttpError(500, "body", "trace");
        mock.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("StatusCode=500") && v.ToString()!.Contains("ResponseBody=body")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}

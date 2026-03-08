using MeAiUtility.MultiProvider.Telemetry;
using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.Tests.Telemetry;

public class ChatTelemetryTests
{
    [Test]
    public void Start_SetsTraceAndReasoning()
    {
        var (_, telemetry) = ChatTelemetry.Start("OpenAI", "gpt-4", ReasoningEffortLevel.High);
        Assert.That(telemetry.TraceId, Is.Not.Empty);
        Assert.That(telemetry.ReasoningEffort, Is.EqualTo(ReasoningEffortLevel.High));
    }
}

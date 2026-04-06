using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.Options;

namespace MeAiUtility.MultiProvider.Tests.Exceptions;

public class MultiProviderExceptionTests
{
    [Test]
    public void ProviderException_StoresHttpData()
    {
        var ex = new ProviderException("err", "OpenAI", "trace", 500, "body");
        Assert.That(ex.ProviderName, Is.EqualTo("OpenAI"));
        Assert.That(ex.TraceId, Is.EqualTo("trace"));
        Assert.That(ex.StatusCode, Is.EqualTo(500));
        Assert.That(ex.ResponseBody, Is.EqualTo("body"));
    }

    [Test]
    public void TimeoutException_StoresTimeoutSeconds()
    {
        var ex = new MeAiUtility.MultiProvider.Exceptions.TimeoutException("timeout", "OpenAI", 12, "trace");
        Assert.That(ex.TimeoutSeconds, Is.EqualTo(12));
    }

    [Test]
    [Property("IntegrationPointId", "T-4-04")]
    public void UT_IT_T_4_04__CopilotRuntimeException_OperationCliPathAndExitCodeAllPreserved()
    {
        // Operation / CliPath / ExitCode / InnerException が同時に保持されることを確認する。
        var inner = new InvalidOperationException("inner-fail");
        var ex = new CopilotRuntimeException("runtime", "GitHubCopilot", "copilot", 1, "trace", inner, CopilotOperation.Send);
        Assert.That(ex.CliPath, Is.EqualTo("copilot"));
        Assert.That(ex.ExitCode, Is.EqualTo(1));
        Assert.That(ex.Operation, Is.EqualTo(CopilotOperation.Send));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    [Property("IntegrationPointId", "T-4-05")]
    public void CopilotRuntimeException_AllowsNullOperation()
    {
        // 既存コンストラクタ（Operation パラメータなし）で生成した場合に Operation == null となることを確認する。
        var ex = new CopilotRuntimeException("runtime", "GitHubCopilot", "copilot", 1, "trace");
        Assert.That(ex.Operation, Is.Null);
    }

    [Test]
    [Property("IntegrationPointId", "T-4-03")]
    public void UT_IT_T_4_03__CopilotRuntimeException_ClientInitializationOperationIsSetCorrectly()
    {
        // CopilotRuntimeException に ClientInitialization operation を設定したとき、正しく保持されることを確認する。
        var inner = new InvalidOperationException("CLI not found");
        var ex = new CopilotRuntimeException(
            "クライアント初期化失敗",
            "GitHubCopilot",
            cliPath: null,
            exitCode: null,
            innerException: inner,
            operation: CopilotOperation.ClientInitialization);

        Assert.That(ex.Operation, Is.EqualTo(CopilotOperation.ClientInitialization));
        Assert.That(ex.Message, Is.EqualTo("クライアント初期化失敗"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    [Property("IntegrationPointId", "T-4-06")]
    public void UT_IT_T_4_06__ExistingCatchCodeIsNotAffectedByOperation()
    {
        // Operation プロパティを参照しない既存の catch パターンが、Operation フィールド追加後も壊れないことを確認する。
        var ex = new CopilotRuntimeException(
            "runtime error",
            "GitHubCopilot",
            cliPath: @"C:\Program Files\GitHub CLI\gh.exe",
            exitCode: 1,
            traceId: "trace-abc");

        MultiProviderException? caught = null;
        try
        {
            throw ex;
        }
        catch (MultiProviderException e)
        {
            caught = e;
        }

        // Operation を参照せずに catch できること、かつ既存プロパティに変化がないこと
        Assert.That(caught, Is.Not.Null);
        Assert.That(caught!.Message, Is.EqualTo("runtime error"));
        Assert.That(caught.ProviderName, Is.EqualTo("GitHubCopilot"));
        Assert.That(caught, Is.InstanceOf<CopilotRuntimeException>());
        var copilotEx = (CopilotRuntimeException)caught;
        Assert.That(copilotEx.CliPath, Is.EqualTo(@"C:\Program Files\GitHub CLI\gh.exe"));
        Assert.That(copilotEx.ExitCode, Is.EqualTo(1));
    }
}

# MultiCodingAgentFacade

`MultiCodingAgentFacade` は、GitHub Copilot SDK と Codex App Server を .NET アプリから呼び出すための runtime library です。
2つの runtime を無理に共通 facade へ押し込まず、それぞれの公開 API、権限、sandbox、diagnostics を typed interface として扱います。

このライブラリは、プロバイダー切替基盤ではありません。GitHub Copilot SDK と Codex App Server の対象を隠すのではなく、.NET 側から扱いやすい client、DI 登録、設定、response model、streaming update を提供する層です。

## 対象 runtime

| Runtime | Package / project | 主な入口 |
| --- | --- | --- |
| GitHub Copilot SDK | `MultiCodingAgentFacade.GitHubCopilot` | `GitHubCopilotAgentClient` |
| Codex App Server | `MultiCodingAgentFacade.CodexAppServer` | `CodexAppServerAgentClient` |
| 共通例外・診断・option 型 | `MultiCodingAgentFacade.Core` | `RuntimeFacadeException`, `ReasoningEffortLevel` |

対応 framework は `net8.0` と `net10.0` です。

## 提供しないもの

- OpenAI、Azure OpenAI、OpenAI compatible endpoint の provider switching。
- 複数 provider を1つの chat abstraction へまとめる共通 facade。
- 非対応 runtime parameter を無理に通す escape hatch。
- 常時実 runtime を CI で動かす仕組み。
- 高度な承認 UI。必要なら GitHub Copilot SDK または Codex App Server を直接使う選択肢も残してください。

## Quickstart

source checkout から試す場合は、sample project を実行します。通常実行は dry-run で、production DI 登録と request 作成だけを確認します。

```bash
dotnet run --project src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj --framework net8.0
```

実 runtime に接続する場合だけ、対象 runtime の opt-in 環境変数を有効にして明示的な引数を渡します。
実行結果には、ライブラリが公開する `RequestId`、`TraceId`、status、diagnostics summary、error summary などの診断情報も標準出力へ表示されます。

```bash
# GitHub Copilot SDK
set MCAF_GITHUB_COPILOT_INTEGRATION=1
dotnet run --project src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj --framework net8.0 -- --run-copilot

# Codex App Server
set MCAF_CODEX_APP_SERVER_INTEGRATION=1
dotnet run --project src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj --framework net8.0 -- --run-codex
```

PowerShell では次のように設定します。

```powershell
$env:MCAF_GITHUB_COPILOT_INTEGRATION = "1"
$env:MCAF_CODEX_APP_SERVER_INTEGRATION = "1"
```

### 実 runtime 実行前の認証

サンプルの dry-run は認証情報なしで実行できます。`--run-copilot`、`--stream-copilot`、`--run-codex`、`--stream-codex` で実 runtime に接続する場合は、先に対象 runtime の CLI または SDK が現在のユーザーで認証済みであることを確認してください。

GitHub Copilot SDK は、既定では Copilot CLI / SDK のログイン済みユーザーを使用します。未ログインの場合は次の手順で認証し、CLI が起動できることを確認してから sample project を実行します。

```powershell
copilot login
copilot --version
$env:MCAF_GITHUB_COPILOT_INTEGRATION = "1"
dotnet run --project src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj --framework net8.0 -- --run-copilot
```

アプリケーション側で GitHub token を明示的に渡す場合は、`MultiCodingAgentFacade:GitHubCopilot:GitHubToken` を設定できます。ただし sample project はログイン済みユーザーでの実行確認を主経路にしています。認証 token は README、`appsettings.json`、git 管理対象ファイルへ保存しないでください。

Codex App Server は `codex app-server` プロセスを起動します。このライブラリ自体は Codex の認証情報を保持しないため、先に Codex CLI のログイン状態を確認し、必要ならログインしてから sample project を実行します。

```powershell
codex login status
codex login
codex doctor
$env:MCAF_CODEX_APP_SERVER_INTEGRATION = "1"
dotnet run --project src/MultiCodingAgentFacade.Samples/MultiCodingAgentFacade.Samples.csproj --framework net8.0 -- --run-codex
```

## DI 登録

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiCodingAgentFacade.CodexAppServer.Configuration;
using MultiCodingAgentFacade.GitHubCopilot.Configuration;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging();
services.AddGitHubCopilotAgentRuntime(configuration);
services.AddCodexAppServerAgentRuntime(configuration);

using var provider = services.BuildServiceProvider();
var copilot = provider.GetRequiredService<GitHubCopilotAgentClient>();
var codex = provider.GetRequiredService<CodexAppServerAgentClient>();
```

## appsettings.json

設定 section は runtime ごとに分かれます。

```json
{
  "MultiCodingAgentFacade": {
    "GitHubCopilot": {
      "ModelId": "gpt-5",
      "ReasoningEffort": "Medium",
      "TimeoutSeconds": 120,
      "WorkingDirectory": "D:\\work\\repo",
      "PermissionHandling": "ApproveAll"
    },
    "CodexAppServer": {
      "CodexCommand": "codex",
      "Transport": "stdio",
      "ModelId": "gpt-5",
      "ReasoningEffort": "Medium",
      "WorkingDirectory": "D:\\work\\repo",
      "ApprovalPolicy": "never",
      "SandboxMode": "workspace-write",
      "NetworkAccess": false,
      "TimeoutSeconds": 1800
    }
  }
}
```

## GitHub Copilot SDK

### Non-streaming

```csharp
using MultiCodingAgentFacade.Core.Options;
using MultiCodingAgentFacade.GitHubCopilot;
using MultiCodingAgentFacade.GitHubCopilot.Options;

var response = await copilot.SendTurnAsync(new GitHubCopilotAgentRequest
{
    Prompt = "Summarize the current repository architecture.",
    ModelId = "gpt-5",
    ReasoningEffort = ReasoningEffortLevel.Medium,
    WorkingDirectory = @"D:\work\repo",
    TimeoutSeconds = 120,
    PermissionHandling = GitHubCopilotPermissionHandlingMode.DenyAll,
});

Console.WriteLine(response.Text);
Console.WriteLine($"TraceId={response.TraceId}; RequestId={response.RequestId}; Runtime={response.RuntimeName}");
```

`GitHubCopilotAgentResponse` は `Text`、`ModelId`、`TraceId`、`RequestId`、`RuntimeName`、`ElapsedTime`、`FinishStatus`、`DiagnosticsSummary`、`SdkMetadata` を保持します。

### Streaming

```csharp
await foreach (var update in copilot.StreamTurnAsync(new GitHubCopilotAgentRequest
{
    Prompt = "Stream a short implementation plan.",
    ModelId = "gpt-5",
    Streaming = true,
    WorkingDirectory = @"D:\work\repo",
    PermissionHandling = GitHubCopilotPermissionHandlingMode.DenyAll,
}))
{
    if (update.Kind == GitHubCopilotStreamingUpdateKind.Delta)
    {
        Console.Write(update.TextDelta);
    }
}
```

GitHub Copilot SDK wrapper が streaming を公開していない構成では、streaming 呼び出しは fail-fast します。最終応答を分割する疑似 streaming は行いません。

### Permission handling

GitHub Copilot の permission handling 既定値は `ApproveAll` です。
これは sub-agent 的に使うとき、呼び出し側アプリへ承認イベント UI 実装を必須化しないための default です。

選択肢は次のとおりです。

| 値 | 挙動 |
| --- | --- |
| `ApproveAll` | SDK の承認要求を自動承認する |
| `DenyAll` | SDK の承認要求を拒否する |
| `NoResult` | SDK へ明示結果を返さない |

承認 request の種類ごとに人間へ確認する UI、差分表示、永続的な approval policy などが必要な場合は、このライブラリで薄く包みすぎず、GitHub Copilot SDK を直接使う設計も候補にしてください。

### Model provider parameters

このライブラリに `ProviderOverride` という共通概念はありません。
GitHub Copilot SDK が受け取れる model provider 関連値は `GitHubCopilotModelProviderOptions` / `ModelProvider` として typed に公開します。

| Property | Notes |
| --- | --- |
| `Type` | `openai` または `azure` |
| `BaseUrl` | 絶対 HTTP/HTTPS URL |
| `ApiKey` / `BearerToken` | どちらか一方だけ |
| `AzureApiVersion` | `Type=azure` のとき必須 |

非対応 parameter は非対応です。必要になった場合はこのライブラリを更新するか、GitHub Copilot SDK を直接使ってください。

### Skills / tools / working directory

GitHub Copilot request では `WorkingDirectory`、`AvailableTools`、`ExcludedTools`、`SkillDirectories`、`DisabledSkills` を typed property として指定できます。
`AdvancedOptions` は SDK 追従用の不安定な escape hatch です。主要な値は typed property を優先し、unsupported key は fail-fast します。

## Codex App Server

### Non-streaming

```csharp
using MultiCodingAgentFacade.CodexAppServer;
using MultiCodingAgentFacade.CodexAppServer.Threading;

var response = await codex.ExecuteTurnAsync(new CodexAppServerTurnRequest
{
    Prompt = "Inspect this repository and list risky files.",
    ModelId = "gpt-5",
    ReasoningEffort = CodexReasoningEffort.Medium,
    WorkingDirectory = @"D:\work\repo",
    ApprovalPolicy = "never",
    SandboxMode = "workspace-write",
    NetworkAccess = false,
    AutoApprove = false,
    ThreadReusePolicy = CodexThreadReusePolicy.AlwaysNew,
    CaptureEventsForDiagnostics = true,
});

Console.WriteLine(response.Text);
Console.WriteLine($"ThreadId={response.ThreadId}; TurnId={response.TurnId}; Status={response.Status}");
```

`CodexAppServerTurnResponse` は `Text`、`ThreadId`、`TurnId`、`Status`、`TraceId`、`RequestId`、`DiagnosticsSummary`、`ErrorSummary`、`JsonRpcTurnStartRequestId` を保持します。
Codex App Server が公開した診断や error summary は、このライブラリでも隠さず公開します。利用者が扱いやすい形へ整える層であって、対象 runtime を覆い隠す層ではありません。

### Streaming

```csharp
await foreach (var update in codex.StreamTurnAsync(new CodexAppServerTurnRequest
{
    Prompt = "Stream a concise code review.",
    WorkingDirectory = @"D:\work\repo",
    ApprovalPolicy = "never",
    SandboxMode = "workspace-write",
    NetworkAccess = false,
    CaptureEventsForDiagnostics = true,
}))
{
    if (update.Kind == CodexAppServerStreamingUpdateKind.Delta)
    {
        Console.Write(update.TextDelta);
    }
}
```

`CodexAppServerStreamingUpdate` は delta、status change、completed、error を区別し、thread / turn / request / trace / diagnostics / error summary を保持します。

### Sandbox / approval / working directory / skills

Codex request では `ApprovalPolicy`、`SandboxMode`、`NetworkAccess`、`AutoApprove`、`WorkingDirectory` を指定できます。

| Property | 例 |
| --- | --- |
| `ApprovalPolicy` | `never`, `on-request`, `on-failure`, `untrusted` |
| `SandboxMode` | `read-only`, `workspace-write`, `danger-full-access` |
| `NetworkAccess` | `false` |
| `WorkingDirectory` | repo root または作業対象 directory |

Codex の skill discovery は Codex runtime の配置規約に従います。
`WorkingDirectory` を起点に `AGENTS.md`、`.agents/skills/**/SKILL.md`、`.codex/agents` などを配置し、prompt で `$skill-name` を指定してください。
Codex App Server runtime に、GitHub Copilot と同じ `SkillDirectories` / `DisabledSkills` typed API はありません。

## Diagnostics と例外

すべての runtime 例外は `RuntimeFacadeException` 系の例外で返します。
この repository の実装方針として、例外を捕捉して捨てる場合は `Exception.ToString()` を trace / log に出力します。

主な共通例外:

- `RuntimeInvalidRequestException`
- `RuntimeOperationException`
- `RuntimeTimeoutException`
- `RuntimeFeatureNotSupportedException`
- `RuntimeAuthenticationException`
- `RuntimeRateLimitException`

Copilot response/update は SDK metadata と diagnostics summary を保持します。
Codex response/update は Codex App Server 由来の thread / turn / status / diagnostics / error summary を保持します。

## Integration opt-in

CI や通常開発で real runtime credentials を必須にしないため、integration smoke は opt-in です。

| Runtime | Environment variable |
| --- | --- |
| GitHub Copilot SDK | `MCAF_GITHUB_COPILOT_INTEGRATION=1` |
| Codex App Server | `MCAF_CODEX_APP_SERVER_INTEGRATION=1` |

未設定時は skip reason を出して real runtime smoke を実行しません。
ただし production DI/client binding は credentials なしでも確認します。

## Release zip distribution

GitHub Release の zip 配布を使う場合は、利用する target framework に合う archive を選びます。

| Archive | Target framework |
| --- | --- |
| `MultiCodingAgentFacade-vX.X.X-net8.0.zip` | .NET 8 |
| `MultiCodingAgentFacade-vX.X.X-net10.0.zip` | .NET 10 |

解凍した DLL をアプリの `libs/` などへ置き、必要な runtime project を参照します。

```xml
<ItemGroup>
  <Reference Include="libs\MultiCodingAgentFacade.Core.dll" />
  <Reference Include="libs\MultiCodingAgentFacade.GitHubCopilot.dll" />
  <Reference Include="libs\MultiCodingAgentFacade.CodexAppServer.dll" />
</ItemGroup>
```

GitHub Copilot SDK runtime を使うアプリでは、SDK と Microsoft.Extensions.* の依存 package も利用側で解決してください。

```xml
<ItemGroup>
  <PackageReference Include="GitHub.Copilot.SDK" Version="0.2.1-preview.1" />
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.2" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.2" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.2" />
</ItemGroup>
```

現時点の release workflow 修正は別 slice の担当です。README は最終配布形として、`MultiCodingAgentFacade` 名の zip と runtime DLL を前提にしています。

## Migration note

この節は履歴と移行判断のための限定的な記録です。通常利用手順ではありません。

旧構成では `MeAiUtility.MultiProvider`、`AddMultiProviderChat`、`IChatClient`、OpenAI / Azure OpenAI provider switching、`ProviderOverride`、`ProviderFactory`、`ProviderRegistry` などを中心に案内していました。
`MultiCodingAgentFacade` では、その active guidance を廃止します。

移行時の考え方:

- GitHub Copilot SDK を使うコードは `GitHubCopilotAgentClient` と `GitHubCopilotAgentRequest` へ移します。
- Codex App Server を使うコードは `CodexAppServerAgentClient` と `CodexAppServerTurnRequest` へ移します。
- provider switching を前提にした設定は runtime ごとの typed options に分けます。
- unsupported parameter は黙って通さず、このライブラリを更新するか underlying SDK/server を直接利用します。

旧名を残してよい場所は、この migration note、planning artifacts、過去仕様の履歴説明に限ります。
final old-name audit の PASS 判定は SL-002 と親 cross-slice verification の担当です。

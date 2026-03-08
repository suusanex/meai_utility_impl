# Research: MEAI マルチプロバイダー抽象化ライブラリ

**Branch**: `002-meai-multi-provider` | **Date**: 2026-02-08  
**Phase**: 0 (Research & Technology Decisions)

## Overview

本ドキュメントは、MEAI マルチプロバイダー抽象化ライブラリの実装に必要な技術調査結果をまとめたものである。Plan.md で特定されたNEEDS CLARIFICATION項目と技術選択に関する意思決定を記録する。

---

## Research Task 1: Microsoft.Extensions.AI 実装状況

### 調査結果サマリー

**IChatClient インターフェースの提供元**:
- **Microsoft.Extensions.AI.Abstractions** (v10.2.0) がコアインターフェースを提供
- **Microsoft.Extensions.AI** (v10.2.0) がユーティリティ（テレメトリ、キャッシング、パイプライン構築）を提供

**既存プロバイダー実装**:
- ✅ **Microsoft.Extensions.AI.OpenAI** (v10.2.0-preview.1): OpenAI API および OpenAI互換エンドポイント対応
- ✅ **Microsoft.Extensions.AI.AzureAIInference** (v10.0.0-preview.1): Azure AI Inference + Azure OpenAI 対応
- ✅ **GitHub Copilot SDK**: 公式SDK（.NET / TypeScript / Python / Go）が存在。ただし Microsoft.Extensions.AI の `IChatClient` 実装は提供されないため、MEAI境界のアダプタは自作が必要

### IChatClient 主要メソッド

```csharp
public interface IChatClient : IDisposable
{
    Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    object? GetService(Type serviceType, object? serviceKey = null);
}
```

### ChatOptions 設計パターン

**主要プロパティ**:
- `Temperature`, `MaxOutputTokens`, `TopP`, `TopK`: 生成パラメータ
- `StopSequences`: 停止条件
- `Tools`, `ToolMode`: Function Calling
- `ResponseFormat`: Text/Json/JsonSchema
- **RawRepresentationFactory**: プロバイダー固有オプション生成コールバック
- **AdditionalProperties**: 追加プロパティ辞書

**特徴**:
- 全プロパティはNullable（未設定時はプロバイダーデフォルトを使用）
- `RawRepresentationFactory` でプロバイダー固有設定を動的生成
- `Clone()` メソッドでシャローコピー作成

### 既存実装で対応済み機能

- ✅ ストリーミングレスポンス (`IAsyncEnumerable<ChatResponseUpdate>`)
- ✅ キャンセルトークン対応
- ✅ ツール呼び出し（Function Calling）
- ✅ テレメトリ（OpenTelemetry統合）
- ✅ キャッシング（分散キャッシュ統合）
- ✅ ロギング（ILogger統合）
- ✅ DI統合（`AddChatClient` 拡張メソッド）
- ✅ パイプライン構築（`ChatClientBuilder`）
- ✅ 構造化出力（`GetResponseAsync<T>`）
- ✅ 埋め込み生成（OpenAI実装のみ）

### Decision 1: 既存実装の再利用方針

**決定事項**: 
- OpenAI および Azure OpenAI は既存のMicrosoft.Extensions.AI実装パッケージを**ラッパー**として使用
- GitHub Copilot は**公式 SDK をバックエンド**として採用し、MEAI に合わせるアダプタ層のみ自作する
- OpenAI互換エンドポイントはOpenAI実装の**ベースURL差し替え**で対応

**理由**:
- 既存実装は十分に成熟しており、車輪の再発明を避ける
- プロバイダー固有の最適化（認証、リトライ等）が既に実装済み
- バグ修正やセキュリティアップデートの恩恵を受けられる

---

## Research Task 2: GitHub Copilot SDK 調査

### 調査結果サマリー

**公式SDKの提供状況**:
- GitHub Copilot SDK は **.NET / TypeScript / Python / Go** の公式SDKを提供
- ルート README で `.NET` 向け `GitHub.Copilot.SDK` パッケージが案内されている
- SDK は **Copilot CLI server と JSON-RPC で通信**し、CLIプロセスの起動も既存CLIサーバー接続もサポートする

**.NET SDK の主要設定面**:
- `CopilotClientOptions`: `CliPath`, `CliArgs`, `CliUrl`, `UseStdio`, `LogLevel`, `AutoStart`, `AutoRestart`, `Cwd`, `Environment`, `GitHubToken`, `UseLoggedInUser`
- `SessionConfig`: `Model`, `ReasoningEffort`, `SystemMessage`, `AvailableTools`, `ExcludedTools`, `Provider` (BYOK), `Streaming`, `WorkingDirectory`, `ConfigDir`, `InfiniteSessions`, `Tools`, `OnUserInputRequest`, `Hooks`
- 認証は **GitHub signed-in user / explicit GitHub token / environment variables / BYOK** をサポート

**モデル能力取得**:
- SDK は `ListModelsAsync()` を公開し、利用可能モデル一覧を取得できる
- `ModelInfo.Capabilities.Supports.ReasoningEffort` で reasoning effort 対応可否を判定できる
- `SupportedReasoningEfforts` / `DefaultReasoningEffort` によりモデルごとの有効値を取得できる
- reasoning effort の有効値は **`low` / `medium` / `high` / `xhigh`**

### 設計含意

1. **GitHub Copilot SDK はオプション機能ではなく、共通I/F設計の基準に置くべき**
   - モデル選択と reasoning effort は first-class な共通オプションが必要
   - `SystemMessage`, `Streaming`, `WorkingDirectory`, `AvailableTools`, `ExcludedTools` も共通化候補
2. **Copilot固有の高度機能を捨てずに通す逃がし口が必要**
   - BYOK provider override, infinite sessions, custom tools/hook 等は provider-specific advanced options で保持
   - 「未対応だから黙って捨てる」設計は Copilot SDK を主用途にした時点で破綻する
3. **reasoning effort は実行前検証が必要**
   - プロバイダー選択後にモデル能力を確認し、未対応モデルには送信前に明確なエラーを返す

### Decision 2: GitHub Copilot SDK 統合方針

**決定事項**:
- GitHub Copilot SDK を**主用途かつ設計アンカー**として扱う
- 共通I/Fは少なくとも **model / reasoning effort / system message / tool allow-deny / streaming / working directory / auth selection / BYOK provider override** を表現可能にする
- Copilot固有で他プロバイダーに自然写像できない要素（hooks, ask_user, custom agents 等）は、**provider-specific advanced options** として保持し、スコープ外扱いにしない
- モデル能力取得 (`ListModelsAsync`) を利用し、**reasoning effort の妥当性を実行前に検証**する

**理由**:
- ユーザーの最頻出ストーリーが「Copilot SDK ベースで始め、必要に応じて他手段へ移行する」ため
- reasoning effort を含む主要セッション設定を表現できないと、抽象化層を導入しても呼び出し側で Copilot SDK へ直結したコードが残るため
- 公式SDKが提供するモデル能力情報を利用すれば、未対応組み合わせを事前に失敗させられるため

**将来拡張の余地**:
- hooks / ask_user / MCP server / custom agents は、共通化できる最小単位が見えた時点で段階的に昇格させる
- 初期段階では provider-specific advanced options と capability matrix で露出する

---
## Research Task 3: OpenAI互換実装の互換性

### 代表的な実装

| 実装 | OpenAI互換エンドポイント | 制約事項 |
|------|------------------------|---------|
| **LM Studio** | `/v1/chat/completions`, `/v1/embeddings` | ローカルモデル識別子、認証不要 |
| **Ollama** | `/v1/chat/completions` | ストリーミング対応、Function Calling は実装依存 |
| **Foundry Local** | 完全互換（調査要） | ベースURL変更のみで動作 |

### 差異が生じやすい領域

1. **モデル名**:
   - OpenAI: `gpt-4o-mini`, `gpt-4`
   - LM Studio: ローカルモデルのパス/ID
   - Ollama: `llama2`, `mistral`

2. **機能サポート**:
   - Function Calling / Tools: 実装により対応状況が異なる
   - Vision (画像入力): 一部実装でのみサポート
   - Structured Output: 精度が実装により異なる

3. **トークン使用量**:
   - `usage` オブジェクトの精度が実装により異なる（一部は概算値）

### Decision 3: OpenAI互換対応方針

**決定事項**:
- **ベースURL差し替え**で基本的な互換性を確保
- **モデル名マッピング**機能を提供（"gpt-4" → "local-model"等）
- **未対応パラメータ**: 検証エラーとして失敗させる（黙って無視しない）
- **動作確認済みエンドポイント**をドキュメントで明示

**実装方針**:
```csharp
public class OpenAICompatibleProviderOptions
{
    public string BaseUrl { get; set; }  // "http://localhost:8080"
    public string? ApiKey { get; set; }  // nullでも可（ローカル実行時）
    public Dictionary<string, string> ModelMapping { get; set; }  // "gpt-4" -> "local-model"
    public bool StrictCompatibilityMode { get; set; }  // true = 互換性差異を即エラー
}
```

---

## Research Task 4: OpenTelemetry 統合パターン

### Activity ベースのトレーシング

**基本実装パターン**:
```csharp
private static readonly ActivitySource s_activitySource = 
    new ActivitySource("MeAiUtility.MultiProvider", "1.0.0");

using (Activity? activity = s_activitySource.StartActivity("ChatRequest"))
{
    activity?.SetTag("gen_ai.provider.name", "openai");
    activity?.SetTag("gen_ai.request.model", "gpt-4");
    
    await SendRequestAsync();
    
    activity?.SetTag("gen_ai.usage.input_tokens", 150);
    activity?.SetTag("gen_ai.usage.output_tokens", 200);
    activity?.SetStatus(ActivityStatusCode.Ok);
}
```

### GenAI Semantic Conventions

**必須タグ**:
- `gen_ai.operation.name`: "chat"
- `gen_ai.provider.name`: "openai", "azure.ai.openai", etc.
- `gen_ai.request.model`: モデル名

**推奨タグ**:
- `gen_ai.request.temperature`, `gen_ai.request.max_tokens`
- `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`
- `gen_ai.response.finish_reasons`
- `server.address`: エンドポイントURL

**センシティブデータ（Opt-Inのみ）**:
- `gen_ai.input.messages`, `gen_ai.output.messages`
- デフォルトは記録しない（PII漏洩防止）

### ILogger との統合

```csharp
public class ChatService
{
    private static readonly ActivitySource s_source = new("MeAiUtility.MultiProvider", "1.0.0");
    private readonly ILogger<ChatService> _logger;

    public async Task<ChatResponse> GetResponseAsync(...)
    {
        using var activity = s_source.StartActivity("GetChatResponse");
        
        try
        {
            _logger.LogInformation("Chat request started for provider {Provider}", providerName);
            // ActivityのTraceIdは自動的にログに含まれる
            await CallProviderAsync();
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### Decision 4: テレメトリ実装方針

**決定事項**:
- `System.Diagnostics.Activity` でトレースを実装
- GenAI Semantic Conventions に準拠したタグ設計
- **プロンプト・レスポンス内容はデフォルトで記録しない**（`EnablePromptCapture` オプションで有効化）
- `ILogger` との統合で TraceId を自動的にログに含める

**構成例**:
```json
{
  "MultiProvider": {
    "Common": {
      "EnableTelemetry": true,
      "CapturePrompts": false  // デフォルトfalse（PII保護）
    }
  }
}
```

---

## Research Task 5: .NET DI拡張メソッド ベストプラクティス

### 命名規約

```csharp
// AddXxx: サービス登録
public static IServiceCollection AddMultiProviderChat(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddOptions<MultiProviderOptions>()
        .Bind(configuration.GetSection("MultiProvider"))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    
    services.AddSingleton<IProviderFactory, ProviderFactory>();
    services.AddSingleton<IChatClient>(sp => 
        sp.GetRequiredService<IProviderFactory>().Create());
    
    return services;
}

// ConfigureXxx: 既存設定の変更
public static IServiceCollection ConfigureMultiProviderChat(
    this IServiceCollection services,
    Action<MultiProviderOptions> configure)
{
    services.Configure(configure);
    return services;
}
```

### オプション検証パターン

**Source Generator 活用（.NET 10+）**:
```csharp
public class MultiProviderOptions
{
    [Required]
    public string Provider { get; set; }  // "OpenAI" | "AzureOpenAI" | ...
    
    public OpenAIProviderOptions? OpenAI { get; set; }
    public AzureOpenAIProviderOptions? AzureOpenAI { get; set; }
}

[OptionsValidator]
public partial class ValidateMultiProviderOptions : IValidateOptions<MultiProviderOptions>
{
    // Source Generatorが実装を自動生成
}

// 登録
services.AddSingleton<IValidateOptions<MultiProviderOptions>, ValidateMultiProviderOptions>();
```

**カスタム検証**:
```csharp
services.AddOptions<MultiProviderOptions>()
    .Bind(configuration.GetSection("MultiProvider"))
    .ValidateDataAnnotations()
    .Validate(opt => 
    {
        return opt.Provider switch
        {
            "OpenAI" => opt.OpenAI != null,
            "AzureOpenAI" => opt.AzureOpenAI != null,
            _ => false
        };
    }, "Provider-specific options must be configured")
    .ValidateOnStart();
```

### Keyed Services (.NET 10+)

複数プロバイダーの登録:
```csharp
services.AddKeyedSingleton<IChatClient, OpenAIChatClient>("OpenAI");
services.AddKeyedSingleton<IChatClient, AzureOpenAIChatClient>("AzureOpenAI");
services.AddKeyedSingleton<IChatClient, GitHubCopilotChatClient>("GitHubCopilot");

// 解決
public class ChatService(
    [FromKeyedServices("OpenAI")] IChatClient openAIClient,
    [FromKeyedServices("AzureOpenAI")] IChatClient azureClient)
{
    // ...
}
```

### Decision 5: DI設計方針

**決定事項**:
- `AddMultiProviderChat` 拡張メソッドで統一的に登録
- Options Pattern + Source Generator によるオプション検証
- **Keyed Services** で複数プロバイダーを名前付き登録（将来拡張用）
- `ValidateOnStart()` で起動時に設定エラーを検出

**推奨登録パターン**:
```csharp
builder.Services.AddMultiProviderChat(builder.Configuration);

// または、プログラマティック設定
builder.Services.AddMultiProviderChat(options =>
{
    options.Provider = "OpenAI";
    options.OpenAI = new OpenAIProviderOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        ModelName = "gpt-4"
    };
});
```

---

## Technology Decisions Summary

| 項目 | 決定内容 | 理由 |
|------|---------|------|
| **OpenAI/Azure OpenAI** | 既存のMicrosoft.Extensions.AI実装を再利用 | 成熟した実装、バグ修正の恩恵 |
| **GitHub Copilot** | 公式 Copilot SDK を主対象にし、主要 SessionConfig を共通実行オプションへ昇格 | 最頻出ストーリーに合わせ、移行時の情報欠落を防ぐ |
| **OpenAI互換** | ベースURL差し替え + モデル名マッピング | 柔軟性とシンプルさのバランス |
| **テレメトリ** | Activity + GenAI Semantic Conventions | 業界標準、OpenTelemetry標準準拠 |
| **DI設計** | Options Pattern + Source Generator + Keyed Services | 型安全性、検証の自動化 |
| **機密情報保護** | プロンプト/レスポンスはデフォルト非記録 | PII/秘密情報漏洩防止 |
| **エラー処理** | 即失敗（リトライなし）、詳細ログ | Constitution 準拠、デバッグ容易性 |

---

## Resolved NEEDS CLARIFICATION Items

以下の項目が調査により解決された：

1. ✅ **既存MEAI実装の有無**: OpenAI/Azure OpenAI は既存実装あり、Copilot は公式SDK + MEAIアダプタで対応
2. ✅ **IChatClient仕様**: メソッドシグネチャ、オプションパターン、ストリーミング対応を確認
3. ✅ **OpenAI互換の差異**: モデル名、一部機能サポート、トークン使用量に差異あり
4. ✅ **テレメトリ実装パターン**: Activity + GenAI Semantic Conventions
5. ✅ **DI拡張メソッドパターン**: AddXxx命名、Options Pattern、Source Generator

---

## Next Steps

Phase 1（Data Model & Contracts）へ進行：
- `data-model.md`: MultiProviderOptions、ConversationExecutionOptions、ExtensionParameters 等の詳細定義
- `contracts/`: JSON Schema 生成
- `quickstart.md`: 5分で動作する最小構成ガイド
- Agent Context 更新: 新規技術スタックを Copilot 指示に追加

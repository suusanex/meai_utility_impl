# Data Model: MEAI マルチプロバイダー抽象化ライブラリ

**Branch**: `002-meai-multi-provider` | **Date**: 2026-02-08  
**Phase**: 1 (Design - Data Model)

## Overview

本ドキュメントは、MEAI マルチプロバイダー抽象化ライブラリで使用する主要なデータモデル（エンティティ、オプション、構成）を定義する。実装詳細ではなく、各エンティティの**責務**、**プロパティ**、**関係性**、**制約**を記述する。

---

## Core Entities

### MultiProviderOptions

**責務**: プロバイダー選択のルート設定。どのLLMプロバイダーを使用するかを決定し、各プロバイダー固有の設定を保持する。

**ライフサイクル**: アプリケーション起動時に構成から読み込み、DI登録時に検証。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Provider` | string | ✅ Yes | なし | 使用するプロバイダー名。"OpenAI", "AzureOpenAI", "OpenAICompatible", "GitHubCopilot" のいずれか |
| `OpenAI` | OpenAIProviderOptions? | ❌ No | null | OpenAI プロバイダー固有設定。Provider="OpenAI" の場合は必須 |
| `AzureOpenAI` | AzureOpenAIProviderOptions? | ❌ No | null | Azure OpenAI プロバイダー固有設定。Provider="AzureOpenAI" の場合は必須 |
| `OpenAICompatible` | OpenAICompatibleProviderOptions? | ❌ No | null | OpenAI互換エンドポイント固有設定。Provider="OpenAICompatible" の場合は必須 |
| `GitHubCopilot` | GitHubCopilotProviderOptions? | ❌ No | null | GitHub Copilot SDK 固有設定。Provider="GitHubCopilot" の場合は必須 |
| `Common` | CommonProviderOptions | ❌ No | デフォルト値 | 全プロバイダー共通の設定 |

**検証ルール**:
- `Provider` は上記4つの値のいずれかでなければならない
- `Provider` に対応するプロバイダー固有設定（例: Provider="OpenAI" なら `OpenAI` プロパティ）が非nullでなければならない
- 検証は起動時（`ValidateOnStart()`）に実行

**関係性**:
- `ProviderFactory` が `MultiProviderOptions` を参照して、適切な `IChatClient` 実装を生成

---

### CommonProviderOptions

**責務**: 全プロバイダーに共通する設定値を保持する。デフォルト値の提供とテレメトリ/ロギング制御を管理。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `DefaultTemperature` | float? | ❌ No | 0.7 | デフォルトの温度パラメータ（0.0-2.0） |
| `DefaultMaxTokens` | int? | ❌ No | 1000 | デフォルトの最大トークン数 |
| `DefaultTimeout` | TimeSpan | ❌ No | 60秒 | デフォルトのHTTPタイムアウト |
| `EnableTelemetry` | bool | ❌ No | true | OpenTelemetry トレーシングを有効化 |
| `CapturePrompts` | bool | ❌ No | false | プロンプト/レスポンス内容をテレメトリに記録するか（PII保護のためデフォルトfalse） |
| `LogRequestResponse` | bool | ❌ No | false | リクエスト/レスポンスの詳細をログに出力するか（デバッグ用） |
| `MaskSensitiveData` | bool | ❌ No | true | APIキー、エンドポイントURL等をログでマスキングするか |

---

### ConversationExecutionOptions

**責務**: GitHub Copilot SDK の `SessionConfig` を基準に、各プロバイダーへ可能な範囲で正規化される共通実行オプションを保持する。生成パラメータ（temperature, max tokens 等）は `ChatOptions` に残し、本エンティティではセッション/実行コンテキストに寄せた設定を扱う。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `ModelId` | string? | ❌ No | null | 使用するモデルID。Copilot では `SessionConfig.Model`、他プロバイダーでは相当するモデル指定へ変換 |
| `ReasoningEffort` | ReasoningEffortLevel? | ❌ No | null | reasoning effort。`Low` / `Medium` / `High` / `XHigh` |
| `SystemMessageMode` | SystemMessageMode? | ❌ No | null | system message の適用方式。`Append` または `Replace` |
| `AllowedTools` | IReadOnlyList<string>? | ❌ No | null | 許可するツール名の allow list。null の場合はプロバイダーデフォルト |
| `ExcludedTools` | IReadOnlyList<string>? | ❌ No | null | 無効化するツール名の deny list |
| `ClientName` | string? | ❌ No | null | 呼び出し元アプリ識別子。Copilot SDK の User-Agent 等に利用 |
| `WorkingDirectory` | string? | ❌ No | null | セッション実行ディレクトリ。Copilot SDK 等で利用 |
| `Streaming` | bool? | ❌ No | null | ストリーミング強制指定。null は既定動作 |
| `ProviderOverride` | ProviderOverrideOptions? | ❌ No | null | Copilot SDK の BYOK provider override や、将来の他プロバイダー向け上書きを表す |

**検証ルール**:
- `ReasoningEffort` が指定された場合、選択モデル/プロバイダーが対応していなければ送信前に失敗させる
- `AllowedTools` と `ExcludedTools` を同時指定する場合、`AllowedTools` を優先するか、優先順位を固定した上で明示的に検証する
- `ProviderOverride` は選択プロバイダーが対応していない場合に `NotSupportedException` を返す

---

### OpenAIProviderOptions

**責務**: OpenAI API および OpenAI互換エンドポイント接続に必要な設定を保持。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `ApiKey` | string | ✅ Yes | なし | OpenAI APIキー。環境変数 `${OPENAI_API_KEY}` 形式で参照推奨 |
| `OrganizationId` | string? | ❌ No | null | OpenAI 組織ID（複数組織に所属する場合のみ指定） |
| `BaseUrl` | string? | ❌ No | null | カスタムベースURL。nullの場合は `https://api.openai.com/v1` |
| `ModelName` | string | ❌ No | "gpt-4" | 使用するモデル名 |
| `TimeoutSeconds` | int | ❌ No | 60 | HTTPリクエストタイムアウト（秒） |

**制約**:
- `ApiKey` は非空文字列
- `BaseUrl` はnullまたは有効なHTTP(S) URL
- `TimeoutSeconds` は正の整数

---

### AzureOpenAIProviderOptions

**責務**: Azure OpenAI Service 接続に必要な設定を保持。Azure固有の認証方式とデプロイメント名を管理。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Endpoint` | string | ✅ Yes | なし | Azure OpenAI エンドポイントURL（例: `https://myresource.openai.azure.com`） |
| `DeploymentName` | string | ✅ Yes | なし | デプロイメント名（Azure OpenAIで作成したデプロイメント） |
| `ApiVersion` | string | ❌ No | "2024-02-15-preview" | Azure OpenAI APIバージョン |
| `Authentication` | AzureAuthenticationOptions | ✅ Yes | なし | 認証方式の設定 |
| `TimeoutSeconds` | int | ❌ No | 60 | HTTPリクエストタイムアウト（秒） |

**制約**:
- `Endpoint` は有効なHTTPS URL（`.openai.azure.com` ドメイン推奨）
- `DeploymentName` は非空文字列
- `Authentication` は非null

---

### AzureAuthenticationOptions

**責務**: Azure OpenAI の認証方式を定義。APIキー認証またはEntra ID（旧Azure AD）認証を選択。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Type` | AuthenticationType | ✅ Yes | なし | 認証方式。`ApiKey` または `EntraId` |
| `ApiKey` | string? | 条件付き | null | APIキー（Type=ApiKey の場合は必須） |

**AuthenticationType 列挙型**:
```text
- ApiKey: APIキー認証（api-key ヘッダー）
- EntraId: Entra ID認証（DefaultAzureCredential使用。環境変数、マネージドID、Azure CLI等から自動取得）
```

**検証ルール**:
- `Type == ApiKey` の場合、`ApiKey` は非nullかつ非空文字列
- `Type == EntraId` の場合、`ApiKey` は null でなければならず、指定されていたら検証エラー

---

### OpenAICompatibleProviderOptions

**責務**: OpenAI API互換エンドポイント（Foundry Local、Ollama等）への接続設定を保持。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `BaseUrl` | string | ✅ Yes | なし | OpenAI互換エンドポイントのベースURL（例: `http://localhost:8080`） |
| `ApiKey` | string? | ❌ No | null | APIキー（ローカル実行時は不要な場合が多い） |
| `ModelName` | string | ✅ Yes | なし | 使用するモデル名（互換実装固有の識別子） |
| `ModelMapping` | Dictionary<string, string>? | ❌ No | null | OpenAIモデル名をローカルモデル名にマッピング（例: "gpt-4" → "llama2"） |
| `StrictCompatibilityMode` | bool | ❌ No | true | 既定の fail-fast に加えて、true 時は追加の互換性検証（任意フィールドや補助的制約）も有効化する |
| `TimeoutSeconds` | int | ❌ No | 60 | HTTPリクエストタイムアウト（秒） |

**ModelMapping の例**:
```json
{
  "gpt-4": "llama2",
  "gpt-4o-mini": "mistral"
}
```

---

### GitHubCopilotProviderOptions

**責務**: GitHub Copilot SDK のクライアント初期化設定と、Copilot を既定プロバイダーとして使う場合のセッション既定値を保持する。

**クライアント初期化プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `CliPath` | string? | ❌ No | null | Copilot CLI 実行ファイルパス。null の場合は SDK/環境既定値 |
| `CliArgs` | IReadOnlyList<string>? | ❌ No | null | CLI 起動時に追加する引数 |
| `CliUrl` | string? | ❌ No | null | 既存 CLI server へ接続する場合の URL |
| `UseStdio` | bool | ❌ No | true | CLI server と stdio で接続するか |
| `LogLevel` | string | ❌ No | "info" | SDK/CLI のログレベル |
| `AutoStart` | bool | ❌ No | true | 必要時に CLI server を自動起動するか |
| `AutoRestart` | bool | ❌ No | true | CLI server 異常終了時に自動再接続/再起動するか |
| `GitHubToken` | string? | ❌ No | null | 明示的に渡す GitHub トークン |
| `UseLoggedInUser` | bool? | ❌ No | null | CLI ログイン済みユーザー認証を使用するか |
| `EnvironmentVariables` | Dictionary<string, string>? | ❌ No | null | CLI process に渡す環境変数 |
| `TimeoutSeconds` | int | ❌ No | 120 | ライブラリ側で適用する既定タイムアウト |

**セッション既定値プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `ModelId` | string? | ❌ No | null | Copilot 既定モデル |
| `ReasoningEffort` | ReasoningEffortLevel? | ❌ No | null | Copilot 既定 reasoning effort |
| `SystemMessageMode` | SystemMessageMode? | ❌ No | null | system message の適用方式 |
| `AvailableTools` | IReadOnlyList<string>? | ❌ No | null | 既定 allow list |
| `ExcludedTools` | IReadOnlyList<string>? | ❌ No | null | 既定 deny list |
| `ClientName` | string? | ❌ No | null | 呼び出し元クライアント名 |
| `WorkingDirectory` | string? | ❌ No | null | 既定 working directory |
| `Streaming` | bool? | ❌ No | null | 既定 streaming 設定 |
| `ConfigDir` | string? | ❌ No | null | Copilot session state/config を保存するディレクトリ |
| `InfiniteSessions` | InfiniteSessionOptions? | ❌ No | null | コンテキスト圧縮と永続セッションの既定値 |
| `ProviderOverride` | ProviderOverrideOptions? | ❌ No | null | Copilot SDK の BYOK provider override 既定値 |

**制約**:
- `GitHubToken` を省略しても、`UseLoggedInUser` または環境変数ベース認証が成立しなければ実行時エラー
- `ReasoningEffort` は有効値（Low/Medium/High/XHigh）かつ、選択モデルがサポートする場合のみ有効
- `CliUrl` 指定時は、ローカル起動前提の設定と競合しないよう検証する

---

### ReasoningEffortLevel

**責務**: reasoning effort の共通列挙値。GitHub Copilot SDK の `low / medium / high / xhigh` を基準にする。

```text
- Low
- Medium
- High
- XHigh
```

---

### SystemMessageMode

**責務**: system message の適用方法を表す共通列挙値。

```text
- Append: プロバイダー既定 system message の後ろに追記
- Replace: プロバイダー既定 system message を置き換え
```

---

### InfiniteSessionOptions

**責務**: Copilot SDK の infinite sessions 設定を保持する。

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Enabled` | bool? | ❌ No | null | infinite sessions を有効化するか |
| `BackgroundCompactionThreshold` | double? | ❌ No | null | 背景圧縮を始めるコンテキスト使用率 |
| `BufferExhaustionThreshold` | double? | ❌ No | null | ブロッキング圧縮へ切り替える閾値 |

---

### ProviderOverrideOptions

**責務**: Copilot SDK の BYOK provider 設定を表現する。共通I/Fから provider override を失わずに伝えるための型。

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Type` | string | ✅ Yes | なし | プロバイダー種別（例: `openai`, `azure`, `anthropic`） |
| `BaseUrl` | string | ✅ Yes | なし | 接続先 API のベースURL |
| `ApiKey` | string? | ❌ No | null | API key 認証で使うキー |
| `BearerToken` | string? | ❌ No | null | Bearer token 認証で使うトークン |
| `AzureApiVersion` | string? | ❌ No | null | Azure 系 provider で使う API version |

---

## Request/Response Entities

### ChatRequest

**責務**: チャット実行のリクエストを表現。MEAI の `IChatClient.GetResponseAsync` に渡される情報をカプセル化する。

**プロパティ**:

| プロパティ名 | 型 | 必須 | デフォルト値 | 説明 |
|------------|---|------|------------|------|
| `Messages` | IEnumerable<ChatMessage> | ✅ Yes | なし | チャットメッセージ履歴（system, user, assistant） |
| `Options` | ChatOptions? | ❌ No | null | 生成系オプション（temperature, max tokens, stop sequences, tools 等のMEAI標準） |
| `ExecutionOptions` | ConversationExecutionOptions? | ❌ No | null | `ChatOptions.AdditionalProperties["meai.execution"]` から正規化した共通セッション設定 |
| `ExtensionParameters` | ExtensionParameters? | ❌ No | null | ベンダー固有の拡張パラメータ |
| `CancellationToken` | CancellationToken | ❌ No | default | キャンセルトークン |

**注**: `ChatMessage` と `ChatOptions` は Microsoft.Extensions.AI.Abstractions で定義された型をそのまま使用する。公開I/Fでは `ConversationExecutionOptions` を `ChatOptions.AdditionalProperties["meai.execution"]` に載せ、内部で `ChatRequest.ExecutionOptions` へ正規化する。

---
### ChatResponse

**責務**: チャット実行のレスポンスを表現。MEAI の `IChatClient.GetResponseAsync` の戻り値。

**プロパティ**（MEAI標準を使用）:

| プロパティ名 | 型 | 説明 |
|------------|---|------|
| `Message` | ChatMessage | 生成されたアシスタントメッセージ |
| `Choices` | IReadOnlyList<ChatChoice> | 複数の候補（通常は1つ） |
| `Usage` | ChatUsage? | トークン使用量（input/output/total） |
| `FinishReason` | FinishReason? | 終了理由（stop, length, tool_calls等） |
| `ModelId` | string? | 実際に使用されたモデルID |
| `AdditionalProperties` | AdditionalPropertiesDictionary? | プロバイダー固有の追加情報 |

---

### ExtensionParameters

**責務**: ベンダー固有の拡張パラメータを型安全に格納する辞書。共通化できない差分を失わずに伝える。

**構造**:
```text
内部的には Dictionary<string, object?> を保持
キー命名規約: "{プロバイダー名}.{パラメータ名}"
  例: "azure.data_sources"
      "openai.top_logprobs"
      "copilot.mcp_servers"
      "copilot.custom_agents"
```

**メソッド**:

| メソッド | 説明 |
|---------|------|
| `Set(string key, object? value)` | 拡張パラメータを設定 |
| `Get<T>(string key)` | 拡張パラメータを取得（型変換付き） |
| `TryGet<T>(string key, out T? value)` | 安全な取得（存在しない場合はfalse） |
| `Has(string key)` | キーの存在チェック |
| `GetAllForProvider(string providerName)` | 特定プロバイダー用のパラメータのみを取得 |

**使用例**:
```csharp
var extensionParams = new ExtensionParameters();
extensionParams.Set("azure.data_sources", new[] 
{
    new { type = "azure_search", parameters = new { endpoint = "...", key = "..." } }
});
extensionParams.Set("copilot.mcp_servers", new
{
    issueTracker = new { type = "http", url = "https://example.invalid/mcp" }
});
```

**検証**:
- キーは `{プロバイダー名}.{パラメータ名}` 形式でなければならない
- プロバイダー名は `openai`, `azure`, `copilot` のいずれか
- 値の型が期待と一致しない場合や、選択中プロバイダーで解釈できない場合は、警告で継続せず送信前に例外を返す

---
## Telemetry & Logging Entities

### ChatTelemetry

**責務**: リクエスト・レスポンスのテレメトリコンテキストを保持。OpenTelemetry Activity との統合点。

**プロパティ**:

| プロパティ名 | 型 | 説明 |
|------------|---|------|
| `TraceId` | string | 分散トレースID（ActivityのTraceIdから取得） |
| `SpanId` | string | 現在のスパンID |
| `ParentSpanId` | string? | 親スパンID（階層的トレース） |
| `ProviderName` | string | 使用したプロバイダー名 |
| `ModelName` | string | 使用したモデル名 |
| `StartTimestamp` | DateTimeOffset | リクエスト開始時刻 |
| `EndTimestamp` | DateTimeOffset? | リクエスト終了時刻 |
| `DurationMs` | double? | 実行時間（ミリ秒） |
| `InputTokens` | int? | 入力トークン数 |
| `OutputTokens` | int? | 出力トークン数 |
| `Temperature` | float? | 使用した温度パラメータ |
| `ReasoningEffort` | string? | 使用した reasoning effort |
| `MaxTokens` | int? | 指定した最大トークン数 |
| `FinishReason` | string? | 終了理由 |
| `StatusCode` | ActivityStatusCode | Ok/Error |
| `ErrorMessage` | string? | エラーメッセージ（StatusCode==Error の場合） |

**ライフサイクル**:
1. リクエスト開始時に `ChatTelemetry` インスタンスを生成
2. `Activity` を開始し、TraceId/SpanId を設定
3. リクエスト終了時に終了時刻、トークン使用量等を設定
4. Activity にタグとして記録（GenAI Semantic Conventions 準拠）

---

## Configuration Entities

### ProviderCapabilities

**責務**: 各プロバイダーの機能サポート状況を表現。ランタイムで未対応機能を事前チェック可能にする。

**プロパティ**:

| プロパティ名 | 型 | 説明 |
|------------|---|------|
| `ProviderName` | string | プロバイダー名 |
| `SupportsStreaming` | bool | ストリーミングレスポンス対応 |
| `SupportsTools` | bool | Function Calling / tool execution 対応 |
| `SupportsVision` | bool | 画像入力対応 |
| `SupportsEmbeddings` | bool | 埋め込み生成対応 |
| `SupportsExtensionParameters` | bool | 拡張パラメータ対応 |
| `SupportsReasoningEffort` | bool | reasoning effort 指定対応 |
| `SupportsModelDiscovery` | bool | モデル一覧/能力取得対応 |
| `SupportsProviderOverride` | bool | BYOK provider override 対応 |
| `SupportsPersistentSessions` | bool | 永続セッション/コンテキスト圧縮対応 |
| `MaxTemperature` | float | サポートする最大温度値 |
| `MaxTokensLimit` | int? | 最大トークン数制限（nullの場合は制限なし） |

**プロバイダー別サポート状況**:

| 機能 | OpenAI | Azure OpenAI | OpenAI互換 | GitHub Copilot |
|------|--------|--------------|-----------|---------------|
| Streaming | ✅ | ✅ | ✅ | ✅ |
| Tools | ✅ | ✅ | ⚠️ | ✅ |
| Vision | ✅ | ✅ | ⚠️ | ⚠️ |
| Embeddings | ✅ | ✅ | ⚠️ | ❌ |
| ExtensionParameters | ✅ | ✅ | ⚠️ | ✅ |
| ReasoningEffort | ⚠️ | ⚠️ | ⚠️ | ✅ |
| ModelDiscovery | ⚠️ | ⚠️ | ❌ | ✅ |
| ProviderOverride | ❌ | ❌ | ❌ | ✅ |
| PersistentSessions | ❌ | ❌ | ❌ | ✅ |

**凡例**: ✅ = 完全対応、⚠️ = 実装/モデル依存、❌ = 非対応

---
## Exception Entities

### MultiProviderException (基底クラス)

**責務**: ライブラリ固有例外の基底クラス。全例外が共通して持つべきプロパティを定義。

**プロパティ**:

| プロパティ名 | 型 | 説明 |
|------------|---|------|
| `ProviderName` | string | エラー発生時のプロバイダー名 |
| `TraceId` | string? | トレースID（テレメトリとの相関） |
| `Timestamp` | DateTimeOffset | エラー発生時刻 |
| `StatusCode` | int? | HTTP系エラー時のステータスコード |
| `ResponseBody` | string? | HTTP系エラー時のエラーレスポンス本文 |

**派生例外**:

| 例外型 | 用途 | HTTPステータス |
|-------|------|--------------|
| `AuthenticationException` | 認証失敗 | 401/403 |
| `RateLimitException` | レート制限 | 429 |
| `InvalidRequestException` | 無効なリクエスト | 400 |
| `ProviderException` | プロバイダーエラー | 500-599 |
| `TimeoutException` | タイムアウト | N/A |
| `NotSupportedException` | 未対応機能 | N/A |
| `CopilotRuntimeException` | Copilot起動失敗 | N/A |

---

## Entity Relationships

```text
┌─────────────────────────┐
│ MultiProviderOptions    │
├─────────────────────────┤
│ - Provider: string      │
│ - OpenAI: Options?      │───┐
│ - AzureOpenAI: Options? │───┼─┐
│ - OpenAICompatible: ?   │───┼─┼─┐
│ - GitHubCopilot: ?      │───┼─┼─┼─┐
│ - Common: Options       │   │ │ │ │
└─────────────────────────┘   │ │ │ │
                              ▼ ▼ ▼ ▼
              ┌────────────────────────────────┐
              │ Provider-Specific Options      │
              ├────────────────────────────────┤
              │ - ApiKey, Endpoint, etc.       │
              └────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │ ProviderFactory  │
                    ├──────────────────┤
                    │ Create()         │
                    └──────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │ IChatClient      │
                    ├──────────────────┤
                    │ GetResponseAsync │
                    └──────────────────┘
                              │
                ┌─────────────┴─────────────┐
                ▼                           ▼
        ┌───────────────┐         ┌───────────────────┐
        │ ChatRequest   │         │ ChatResponse      │
        ├───────────────┤         ├───────────────────┤
        │ - Messages    │         │ - Message         │
        │ - Options     │         │ - Usage           │
        │ - Extensions  │         │ - FinishReason    │
        └───────────────┘         └───────────────────┘
                │                           │
                ▼                           ▼
    ┌──────────────────────┐      ┌──────────────────┐
    │ ExtensionParameters  │      │ ChatTelemetry    │
    ├──────────────────────┤      ├──────────────────┤
    │ - GetAllForProvider  │      │ - TraceId        │
    └──────────────────────┘      │ - InputTokens    │
                                  │ - DurationMs     │
                                  └──────────────────┘
```

---

## Validation Summary

### 起動時検証（ValidateOnStart）

- `MultiProviderOptions.Provider` が有効な値か
- `Provider` に対応するプロバイダー固有設定が非nullか
- APIキー、エンドポイントURL等の必須項目が非空か
- URL形式が正しいか（https://, http://localhost のみ許可）

### 実行時検証

- `ExtensionParameters` のキー命名規約違反チェック（違反時は例外）
- 拡張パラメータの型不一致チェック（違反時は例外）
- reasoning effort のモデル能力チェック（未対応時は例外）
- 未対応機能呼び出し時の `ProviderCapabilities` チェック（例外スロー）
- `ChatOptions.StopSequences` のプロバイダー別正規化と互換性検証（解釈不能時は例外）
- `CancellationToken` が送信前チェックと上流呼び出しへ伝播されることの検証

---

## Next Steps

Phase 1 の残り作業:
- `contracts/`: JSON Schema ファイル生成
- `quickstart.md`: 最小構成で動作させる手順書
- Agent Context 更新: 新規技術を .github/copilot-instructions.md に追加

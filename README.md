# MeAiUtility.MultiProvider

複数の LLM プロバイダー（OpenAI / Azure OpenAI / OpenAI 互換 / GitHub Copilot）を統一した API で切り替えて利用するためのクラスライブラリです。
[Microsoft.Extensions.AI (MEAI)](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) の `IChatClient` インターフェースを実装しており、DI コンテナへの登録と `appsettings.json` による設定のみで動作します。
プロバイダーの切り替えはアプリケーションコードを変更せず設定ファイルの書き換えだけで行えます。

---

## 目次

1. [パッケージ構成](#パッケージ構成)
2. [クイックスタート](#クイックスタート)
3. [appsettings.json の設定](#appsettingsjson-の設定)
4. [DI 登録メソッド](#di-登録メソッド)
5. [プロバイダー別オプション](#プロバイダー別オプション)
6. [ChatOptions の拡張](#chatoptions-の拡張)
7. [独自インターフェースと型](#独自インターフェースと型)
8. [例外クラス](#例外クラス)
9. [使用例](#使用例)

---

## パッケージ構成

| パッケージ | 内容 |
|---|---|
| `MeAiUtility.MultiProvider` | コア。`IChatClient` DI 登録、共通オプション |
| `MeAiUtility.MultiProvider.OpenAI` | OpenAI / OpenAI 互換プロバイダー |
| `MeAiUtility.MultiProvider.AzureOpenAI` | Azure OpenAI プロバイダー |
| `MeAiUtility.MultiProvider.GitHubCopilot` | GitHub Copilot CLI SDK プロバイダー |

対象フレームワーク：`net8.0` / `net10.0`

---

## クイックスタート

### 1. ライブラリのダウンロードとプロジェクトへの参照追加

[GitHub Releases](https://github.com/suusanex/meai_utility_impl/releases) から最新バージョンのアーカイブをダウンロードします。

#### 1-1. DLL ファイルの配置

使用するターゲットフレームワークに合わせてアーカイブを選択し、解凍します。

| アーカイブ名 | 対応フレームワーク |
|---|---|
| `MeAiUtility-vX.X.X-net8.0.zip` | .NET 8 |
| `MeAiUtility-vX.X.X-net10.0.zip` | .NET 10 |

解凍したフォルダ内の DLL をプロジェクトの任意のフォルダ（例：`libs/`）に配置します。

#### 1-2. プロジェクトへの参照追加

`.csproj` にコア DLL と使用するプロバイダーの DLL を追加します。

```xml
<ItemGroup>
  <!-- コア（必須） -->
  <Reference Include="libs\MeAiUtility.MultiProvider.dll" />

  <!-- 使用するプロバイダーを 1 つ選択 -->
  <Reference Include="libs\MeAiUtility.MultiProvider.OpenAI.dll" />
  <!-- または -->
  <Reference Include="libs\MeAiUtility.MultiProvider.AzureOpenAI.dll" />
  <!-- または -->
  <Reference Include="libs\MeAiUtility.MultiProvider.GitHubCopilot.dll" />
</ItemGroup>
```

#### 1-3. NuGet 依存パッケージの追加

本ライブラリが依存する NuGet パッケージを別途インストールします。

```bash
# 共通（必須）
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Options.ConfigurationExtensions
dotnet add package Microsoft.Extensions.Configuration.Json
```

### 2. appsettings.json の設定

```json
{
  "MultiProvider": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "YOUR_API_KEY",
      "ModelName": "gpt-4"
    }
  }
}
```

### 3. DI への登録

```csharp
// OpenAI を使う場合
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddOpenAIProvider(builder.Configuration);

// Azure OpenAI を使う場合
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddAzureOpenAIProvider(builder.Configuration);

// GitHub Copilot を使う場合
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddGitHubCopilotProvider(builder.Configuration);
```

### 4. IChatClient の利用

`IChatClient` の詳細については [Microsoft.Extensions.AI 公式ドキュメント](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) を参照してください。

```csharp
// コンストラクタインジェクション
public class MyService(IChatClient chatClient)
{
    public async Task<string> AskAsync(string question)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, question),
        ]);
        return response.Message.Text;
    }
}
```

---

## appsettings.json の設定

最上位セクション名は `"MultiProvider"` 固定です。

```json
{
  "MultiProvider": {
    "Provider": "<プロバイダー名>",
    "Common": { ... },
    "<プロバイダー名>": { ... }
  }
}
```

### `Provider` フィールド（必須）

使用するプロバイダーを文字列で指定します。

| 値 | 対応パッケージ |
|---|---|
| `"OpenAI"` | `MeAiUtility.MultiProvider.OpenAI` |
| `"AzureOpenAI"` | `MeAiUtility.MultiProvider.AzureOpenAI` |
| `"OpenAICompatible"` | `MeAiUtility.MultiProvider.OpenAI` |
| `"GitHubCopilot"` | `MeAiUtility.MultiProvider.GitHubCopilot` |

### `Common` セクション（任意）

全プロバイダー共通の設定です（`CommonProviderOptions` クラスに対応）。

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `DefaultTemperature` | `float?` | `0.7` | デフォルトの温度パラメータ |
| `DefaultMaxTokens` | `int?` | `1000` | デフォルトの最大トークン数 |
| `DefaultTimeout` | `TimeSpan` | `00:01:00` | デフォルトのタイムアウト |
| `EnableTelemetry` | `bool` | `true` | テレメトリの有効化 |
| `CapturePrompts` | `bool` | `false` | ログへのプロンプト記録 |
| `LogRequestResponse` | `bool` | `false` | リクエスト・レスポンスのログ出力 |
| `MaskSensitiveData` | `bool` | `true` | ログ内の機密データをマスク |

---

## DI 登録メソッド

### `AddMultiProviderChat`

```csharp
// appsettings.json から設定を読み込む場合
IServiceCollection AddMultiProviderChat(this IServiceCollection services, IConfiguration configuration)

// コードで設定する場合
IServiceCollection AddMultiProviderChat(this IServiceCollection services, Action<MultiProviderOptions> configure)
```

`IChatClient` をシングルトンとして DI コンテナに登録します。`IProviderFactory` も同時に登録されます。

**名前空間：** `MeAiUtility.MultiProvider.Configuration`

---

### `AddOpenAIProvider`

```csharp
IServiceCollection AddOpenAIProvider(this IServiceCollection services, IConfiguration configuration)
```

OpenAI プロバイダーおよび OpenAI 互換プロバイダーを DI に登録します。`appsettings.json` の `MultiProvider:OpenAI` または `MultiProvider:OpenAICompatible` セクションを読み込みます。

**名前空間：** `MeAiUtility.MultiProvider.OpenAI.Configuration`

---

### `AddAzureOpenAIProvider`

```csharp
IServiceCollection AddAzureOpenAIProvider(this IServiceCollection services, IConfiguration configuration)
```

Azure OpenAI プロバイダーを DI に登録します。`appsettings.json` の `MultiProvider:AzureOpenAI` セクションを読み込みます。

**名前空間：** `MeAiUtility.MultiProvider.AzureOpenAI.Configuration`

---

### `AddGitHubCopilotProvider`

```csharp
IServiceCollection AddGitHubCopilotProvider(this IServiceCollection services, IConfiguration configuration)
```

GitHub Copilot プロバイダーを DI に登録します。`appsettings.json` の `MultiProvider:GitHubCopilot` セクションを読み込みます。

このメソッドは `ICopilotSdkWrapper` のデフォルト実装（スタブ）を登録します。**実際に Copilot CLI を呼び出すには、`ICopilotSdkWrapper` を独自実装に差し替えるか、`AddGitHubCopilotCliSdkWrapper()` を追加で呼び出してください。**

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Configuration`

---

### `AddGitHubCopilotCliSdkWrapper`

```csharp
IServiceCollection AddGitHubCopilotCliSdkWrapper(this IServiceCollection services)
```

`ICopilotSdkWrapper` を、実際の GitHub Copilot CLI を起動する `GitHubCopilotCliSdkWrapper` で上書き登録します。`AddGitHubCopilotProvider()` の後で呼び出してください。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Configuration`

---

## プロバイダー別オプション

### OpenAI プロバイダー（`OpenAIProviderOptions`）

`appsettings.json` セクション：`MultiProvider:OpenAI`

| プロパティ | 型 | デフォルト | 必須 | 説明 |
|---|---|---|---|---|
| `ApiKey` | `string` | — | ✅ | OpenAI API キー |
| `OrganizationId` | `string?` | `null` | | 組織 ID |
| `BaseUrl` | `string?` | `null` | | カスタム API エンドポイント |
| `ModelName` | `string` | `"gpt-4"` | | 使用するモデル名 |
| `TimeoutSeconds` | `int` | `60` | | タイムアウト秒数 |

---

### OpenAI 互換プロバイダー（`OpenAICompatibleProviderOptions`）

`appsettings.json` セクション：`MultiProvider:OpenAICompatible`

| プロパティ | 型 | デフォルト | 必須 | 説明 |
|---|---|---|---|---|
| `BaseUrl` | `string` | — | ✅ | API エンドポイントの URL |
| `ApiKey` | `string?` | `null` | | API キー |
| `ModelName` | `string` | — | ✅ | 使用するモデル名 |
| `ModelMapping` | `Dictionary<string,string>?` | `null` | | モデル名のマッピング |
| `StrictCompatibilityMode` | `bool` | `true` | | 厳密互換モード |
| `TimeoutSeconds` | `int` | `60` | | タイムアウト秒数 |

---

### Azure OpenAI プロバイダー（`AzureOpenAIProviderOptions`）

`appsettings.json` セクション：`MultiProvider:AzureOpenAI`

| プロパティ | 型 | デフォルト | 必須 | 説明 |
|---|---|---|---|---|
| `Endpoint` | `string` | — | ✅ | Azure OpenAI エンドポイント URL |
| `DeploymentName` | `string` | — | ✅ | デプロイメント名 |
| `ApiVersion` | `string` | `"2024-02-15-preview"` | | API バージョン |
| `Authentication` | `AzureAuthenticationOptions` | — | ✅ | 認証設定（後述） |
| `TimeoutSeconds` | `int` | `60` | | タイムアウト秒数 |

#### `AzureAuthenticationOptions`

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Type` | `AuthenticationType` | `ApiKey` | 認証方式（`ApiKey` / `EntraId`） |
| `ApiKey` | `string?` | `null` | `Type = ApiKey` の場合に必須 |

---

### GitHub Copilot プロバイダー（`GitHubCopilotProviderOptions`）

`appsettings.json` セクション：`MultiProvider:GitHubCopilot`

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `CliPath` | `string?` | `null` | Copilot CLI の実行ファイルパス（省略時はシステムの `copilot` を使用） |
| `CliArgs` | `IReadOnlyList<string>?` | `null` | CLI に追加で渡す引数 |
| `CliUrl` | `string?` | `null` | 既存 CLI サーバーの URL（`host:port` 形式）。指定時は CLI プロセスを起動しない |
| `UseStdio` | `bool` | `true` | stdio トランスポートを使用 |
| `LogLevel` | `string` | `"info"` | CLI のログレベル（`none` / `error` / `warning` / `info` / `debug` / `all`） |
| `AutoStart` | `bool` | `true` | CLI サーバーを自動起動 |
| `AutoRestart` | `bool` | `true` | CLI サーバーがクラッシュした場合に自動再起動 |
| `GitHubToken` | `string?` | `null` | GitHub 認証トークン（環境変数より優先） |
| `UseLoggedInUser` | `bool?` | `null` | ログイン済みユーザーの認証情報を使用（省略時は `GitHubToken` が未設定なら `true`） |
| `EnvironmentVariables` | `Dictionary<string,string>?` | `null` | CLI プロセスに渡す環境変数 |
| `TimeoutSeconds` | `int` | `120` | CLI 呼び出しのタイムアウト秒数 |
| `ModelId` | `string?` | `null` | 使用するモデル ID（例：`"gpt-5-mini"`, `"claude-sonnet-4.6"`） |
| `ReasoningEffort` | `ReasoningEffortLevel?` | `null` | 推論努力レベル（`Low` / `Medium` / `High` / `XHigh`） |
| `SystemMessageMode` | `SystemMessageMode?` | `null` | システムメッセージの適用方法（`Append` / `Replace`） |
| `AvailableTools` | `IReadOnlyList<string>?` | `null` | 許可するツール名のリスト |
| `ExcludedTools` | `IReadOnlyList<string>?` | `null` | 除外するツール名のリスト |
| `ClientName` | `string?` | `null` | SDK クライアント識別名 |
| `WorkingDirectory` | `string?` | `null` | CLI プロセスの作業ディレクトリ |
| `Streaming` | `bool?` | `null` | ストリーミング応答の有効化 |
| `ConfigDir` | `string?` | `null` | Copilot CLI の設定ディレクトリ（省略時は `~/.copilot`） |
| `InfiniteSessions` | `InfiniteSessionOptions?` | `null` | 無限セッション（コンテキスト自動圧縮）の設定 |
| `ProviderOverride` | `ProviderOverrideOptions?` | `null` | 呼び出すプロバイダーのオーバーライド（BYOK） |

※ GitHub Copilot CLI ラッパーは `ReasoningEffort` をサポートしません。`SupportsReasoningEffort` は常に `false` で、`ReasoningEffort` を指定したリクエストは送信前に拒否されます。

#### `InfiniteSessionOptions`

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Enabled` | `bool?` | `true` | 無限セッションの有効化 |
| `BackgroundCompactionThreshold` | `double?` | `0.80` | バックグラウンド圧縮を開始するコンテキスト使用率（0.0〜1.0） |
| `BufferExhaustionThreshold` | `double?` | `0.95` | 圧縮完了まで処理をブロックするコンテキスト使用率（0.0〜1.0） |

---

## ChatOptions の拡張

`IChatClient.GetResponseAsync` に渡す `ChatOptions` に本ライブラリ独自のパラメータをセットできます。

### `ConversationExecutionOptions`（実行時オーバーライド）

`ChatOptions.AdditionalProperties["meai.execution"]` に `ConversationExecutionOptions` を設定すると、appsettings.json の設定をリクエスト単位で上書きできます。

```csharp
var options = new ChatOptions();
options.AdditionalProperties["meai.execution"] = new ConversationExecutionOptions
{
    ModelId = "gpt-5",
    ReasoningEffort = ReasoningEffortLevel.High,
    Streaming = true,
};

var response = await chatClient.GetResponseAsync(messages, options);
```

| プロパティ | 型 | 説明 |
|---|---|---|
| `ModelId` | `string?` | 使用するモデルを上書き |
| `ReasoningEffort` | `ReasoningEffortLevel?` | 推論努力レベルを上書き（`Low` / `Medium` / `High` / `XHigh`） |
| `SystemMessageMode` | `SystemMessageMode?` | システムメッセージ適用方法（`Append` / `Replace`） |
| `AllowedTools` | `IReadOnlyList<string>?` | 許可するツール名のリスト |
| `ExcludedTools` | `IReadOnlyList<string>?` | 除外するツール名のリスト |
| `ClientName` | `string?` | クライアント識別名 |
| `WorkingDirectory` | `string?` | 作業ディレクトリ |
| `Streaming` | `bool?` | ストリーミング有効化 |
| `ProviderOverride` | `ProviderOverrideOptions?` | プロバイダーのオーバーライド（後述） |

**名前空間：** `MeAiUtility.MultiProvider.Options`

---

### `ExtensionParameters`（プロバイダー固有パラメータ）

`ChatOptions.AdditionalProperties["meai.extensions"]` に `ExtensionParameters` を設定すると、プロバイダー固有のパラメータを渡せます。キーは `"<provider>.<param>"` 形式（例：`"openai.logprobs"`, `"azure.deploymentName"`, `"copilot.configDir"`）です。

```csharp
var ext = new ExtensionParameters();
ext.Set("copilot.configDir", "/custom/.copilot");
ext.Set("openai.logprobs", true);

var options = new ChatOptions();
options.AdditionalProperties["meai.extensions"] = ext;
```

#### `ExtensionParameters` のメソッド

| メソッド | シグネチャ | 説明 |
|---|---|---|
| `Set` | `void Set(string key, object? value)` | パラメータをセット。キーは `"provider.param"` 形式必須 |
| `Get<T>` | `T Get<T>(string key)` | パラメータを取得。存在しない場合は `KeyNotFoundException` |
| `TryGet<T>` | `bool TryGet<T>(string key, out T? value)` | パラメータを安全に取得 |
| `Has` | `bool Has(string key)` | パラメータが存在するか確認 |
| `GetAllForProvider` | `IReadOnlyDictionary<string, object?> GetAllForProvider(string providerName)` | 指定プロバイダーのパラメータを一括取得 |

使用できるプレフィックス：`openai` / `azure` / `copilot`

**名前空間：** `MeAiUtility.MultiProvider.Options`

---

### `ProviderOverrideOptions`（BYOK）

`ConversationExecutionOptions.ProviderOverride` に設定すると、API キーや接続先をリクエスト単位で差し替えられます。

| プロパティ | 型 | 説明 |
|---|---|---|
| `Type` | `string` | プロバイダー種別（例：`"openai"`, `"azure"`） |
| `BaseUrl` | `string` | API エンドポイント URL |
| `ApiKey` | `string?` | API キー |
| `BearerToken` | `string?` | Bearer 認証トークン（`ApiKey` より優先） |
| `AzureApiVersion` | `string?` | Azure OpenAI API バージョン |

**名前空間：** `MeAiUtility.MultiProvider.Options`

---

## 独自インターフェースと型

### `ICopilotSdkWrapper`

GitHub Copilot プロバイダーが使用する SDK ラッパーのインターフェースです。`AddGitHubCopilotProvider` はデフォルトでスタブ実装を登録します。実際に Copilot CLI を呼び出すには、公開実装 `GitHubCopilotCliSdkWrapper` を `AddGitHubCopilotCliSdkWrapper()` で登録するか、独自実装を `ICopilotSdkWrapper` として DI に登録してください。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Abstractions`

#### メソッド

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `ListModelsAsync(CancellationToken)` | `Task<IReadOnlyList<CopilotModelInfo>>` | 利用可能なモデル一覧を取得する |
| `SendAsync(string prompt, CopilotSessionConfig config, CancellationToken)` | `Task<string>` | プロンプトを Copilot に送信して応答テキストを返す |

#### `CopilotModelInfo`

| プロパティ | 型 | 説明 |
|---|---|---|
| `ModelId` | `string` | モデル識別子（例：`"gpt-5"`, `"claude-sonnet-4.6"`） |
| `SupportsReasoningEffort` | `bool` | 推論努力レベルのサポート可否 |

#### `CopilotSessionConfig`

`SendAsync` に渡すセッション設定です。

| プロパティ | 型 | 説明 |
|---|---|---|
| `ModelId` | `string?` | 使用するモデル ID |
| `ReasoningEffort` | `ReasoningEffortLevel?` | 推論努力レベル |
| `Streaming` | `bool?` | ストリーミング有効化 |
| `ProviderOverride` | `ProviderOverrideOptions?` | プロバイダーのオーバーライド |
| `AdvancedOptions` | `Dictionary<string, object?>` | プロバイダー固有の追加オプション |

#### ICopilotSdkWrapper の差し替え方法

```csharp
// 既定の CLI wrapper を使う場合
services.AddGitHubCopilotProvider(configuration);
services.AddGitHubCopilotCliSdkWrapper();
```

```csharp
// 独自実装へ差し替える場合
services.AddGitHubCopilotProvider(configuration);
services.AddSingleton<ICopilotSdkWrapper, MyCopilotSdkWrapper>();
```

---

### `ICopilotModelCatalog`

GitHub Copilot プロバイダーの有効な CLI model id 一覧を取得するための公開 I/F です。`GitHubCopilotChatClient` が実装しており、DI から直接解決するか、`IChatClient.GetService(typeof(ICopilotModelCatalog))` で取得できます。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Abstractions`

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `ListModelsAsync(CancellationToken)` | `Task<IReadOnlyList<CopilotModelInfo>>` | 実 Copilot CLI が受け付ける model id 一覧を取得する |

`GitHubCopilotChatClient.GetResponseAsync` は、この catalog に含まれない model id を受け取った場合、送信前に `InvalidRequestException` を返します。

---

### `IProviderCapabilities`

各プロバイダー実装が提供するインターフェースで、対応機能を問い合わせできます。`IChatClient.GetService(typeof(IProviderCapabilities))` で取得できます。

**名前空間：** `MeAiUtility.MultiProvider.Abstractions`

| メンバー | 型 | 説明 |
|---|---|---|
| `SupportsReasoningEffort` | `bool` | 推論努力レベルをサポートするか |
| `SupportsStreaming` | `bool` | ストリーミングをサポートするか |
| `SupportsModelDiscovery` | `bool` | モデル一覧取得をサポートするか |
| `SupportsEmbeddings` | `bool` | 埋め込みをサポートするか |
| `SupportsProviderOverride` | `bool` | プロバイダーのオーバーライドをサポートするか |
| `SupportsExtensionParameters` | `bool` | 拡張パラメータをサポートするか |
| `IsSupported(FeatureName)` | `bool` | 指定した機能（`FeatureName` 列挙体）をサポートするか |

```csharp
var caps = chatClient.GetService(typeof(IProviderCapabilities)) as IProviderCapabilities;
if (caps?.SupportsStreaming == true)
{
    // ストリーミング処理
}
```

---

## 例外クラス

すべての例外は `MultiProviderException`（基底クラス）を継承します。

**名前空間：** `MeAiUtility.MultiProvider.Exceptions`

### `MultiProviderException`（基底）

| プロパティ | 型 | 説明 |
|---|---|---|
| `ProviderName` | `string` | 例外が発生したプロバイダー名 |
| `TraceId` | `string?` | 診断用トレース ID |
| `Timestamp` | `DateTimeOffset` | 例外発生時刻（UTC） |
| `StatusCode` | `int?` | HTTP ステータスコード（該当する場合） |
| `ResponseBody` | `string?` | エラーレスポンス本文（該当する場合） |

### 派生例外クラス

| クラス | 発生条件 | 追加プロパティ |
|---|---|---|
| `AuthenticationException` | 認証失敗（API キー不正など） | — |
| `RateLimitException` | レート制限に到達 | — |
| `InvalidRequestException` | 不正なリクエスト（サポートされないパラメータなど） | — |
| `ProviderException` | プロバイダー側の一般的なエラー | — |
| `TimeoutException` | タイムアウト | `TimeoutSeconds`（int）：タイムアウト設定秒数 |
| `NotSupportedException` | 未対応機能の使用 | `FeatureName`（string）：対応していない機能名 |
| `CopilotRuntimeException` | GitHub Copilot CLI の実行エラー | `CliPath`（string?）、`ExitCode`（int?） |

```csharp
try
{
    var response = await chatClient.GetResponseAsync(messages);
}
catch (RateLimitException ex)
{
    Console.Error.WriteLine($"[{ex.ProviderName}] レート制限: {ex.Message}");
}
catch (MultiProviderException ex)
{
    Console.Error.WriteLine($"[{ex.ProviderName}] TraceId={ex.TraceId}: {ex.Message}");
}
```

---

## 使用例

### OpenAI での基本的なチャット

```json
// appsettings.json
{
  "MultiProvider": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-xxxx",
      "ModelName": "gpt-4.1"
    }
  }
}
```

```csharp
// Program.cs
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddOpenAIProvider(builder.Configuration);
```

```csharp
// サービスクラス
public class ChatService(IChatClient chatClient)
{
    public async Task<string> ChatAsync(string userMessage, CancellationToken ct = default)
    {
        var response = await chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "あなたは親切なアシスタントです。"),
            new ChatMessage(ChatRole.User, userMessage),
        ], cancellationToken: ct);

        return response.Message.Text;
    }
}
```

---

### GitHub Copilot CLI でのチャット

```json
// appsettings.json
{
  "MultiProvider": {
    "Provider": "GitHubCopilot",
    "GitHubCopilot": {
      "UseLoggedInUser": true,
      "ModelId": "gpt-5-mini",
      "TimeoutSeconds": 120
    }
  }
}
```

```csharp
// Program.cs（公開 CLI wrapper を使って実際の CLI を呼び出す）
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddGitHubCopilotProvider(builder.Configuration);
builder.Services.AddGitHubCopilotCliSdkWrapper();
```

```csharp
// 実行時に有効な CLI model id を取得する
var catalog = serviceProvider.GetRequiredService<ICopilotModelCatalog>();
var models = await catalog.ListModelsAsync();
```

---

### プロバイダーを切り替える（コード変更なし）

```json
// OpenAI から Azure OpenAI に切り替える場合は Provider の値を変更するだけ
{
  "MultiProvider": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://myinstance.openai.azure.com/",
      "DeploymentName": "gpt-4",
      "Authentication": {
        "Type": "ApiKey",
        "ApiKey": "YOUR_AZURE_KEY"
      }
    }
  }
}
```

---

### リクエスト単位でモデルを切り替える

```csharp
var options = new ChatOptions();
options.AdditionalProperties["meai.execution"] = new ConversationExecutionOptions
{
    ModelId = "claude-sonnet-4.6",
    ReasoningEffort = ReasoningEffortLevel.High,
};

var response = await chatClient.GetResponseAsync(messages, options);
```

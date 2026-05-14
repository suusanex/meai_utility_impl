# MeAiUtility.MultiProvider

複数の LLM プロバイダー（OpenAI / Azure OpenAI / OpenAI 互換 / GitHub Copilot / Codex App Server）を統一した API で切り替えて利用するためのクラスライブラリです。
[Microsoft.Extensions.AI (MEAI)](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) の `IChatClient` インターフェースを実装しており、DI コンテナへの登録と `appsettings.json` による設定のみで動作します。
プロバイダーの切り替えはアプリケーションコードを変更せず設定ファイルの書き換えだけで行えます。

ただし Open AI / Azure OpenAI はこの目的を満たすMEAIの実装がすでに公開されているため、本ライブラリの役割は主に、 GitHub Copilot SDK 向けの基本的なチャット実装をMEAIのインターフェースで提供して切り替えを行いやすくする点となっています。

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
| `MeAiUtility.MultiProvider.GitHubCopilot` | GitHub Copilot SDK プロバイダー |
| `MeAiUtility.MultiProvider.CodexAppServer` | OpenAI Codex App Server プロバイダー |

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
  <!-- または -->
  <Reference Include="libs\MeAiUtility.MultiProvider.CodexAppServer.dll" />
</ItemGroup>
```

#### 1-3. NuGet 依存パッケージの追加

本ライブラリが依存する NuGet パッケージを別途インストールします。

```bash
# 共通（必須）
dotnet add package Microsoft.Extensions.AI.Abstractions --version 10.4.1
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Options.ConfigurationExtensions
dotnet add package Microsoft.Extensions.Configuration.Json

# GitHub Copilot を使う場合のみ追加
dotnet add package GitHub.Copilot.SDK --version 0.2.1-preview.1
```

`Microsoft.Extensions.AI` の公開型は `Microsoft.Extensions.AI.Abstractions` を唯一の供給元として使用します。DLL 配布物を参照する場合も、利用側アプリで同じバージョンの `Microsoft.Extensions.AI.Abstractions` を追加してください。

旧版から更新する場合の注意:

- 旧実装に含まれていた独自 `Microsoft.Extensions.AI` 型は削除されました。
- アプリ側は再ビルドが必要です。
- `ChatResponse.Message.Text` を使っていたコードは `ChatResponse.Text` へ置き換えてください。
- `ChatOptions.AdditionalProperties` は初期状態で `null` の場合があります。値を書き込むときは `options.AdditionalProperties ??= new AdditionalPropertiesDictionary()` で初期化してください。

DLL 参照で配布する場合に GitHub Copilot SDK をアプリ側で直接参照したくないときは、次の設定を推奨します。

```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.2.1-preview.1">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime;build;native;contentfiles;analyzers;buildtransitive</IncludeAssets>
</PackageReference>
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
builder.Services.AddGitHubCopilot(builder.Configuration);

// Codex App Server を使う場合
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddCodexAppServer(builder.Configuration);
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
        return response.Text;
    }
}
```

    `AdditionalProperties` を設定する例:

    ```csharp
    var options = new ChatOptions();
    (options.AdditionalProperties ??= new AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName]
      = new ConversationExecutionOptions { ModelId = "gpt-5" };
    ```

    `ResponseFormat` の扱い:

    - OpenAI: サポート
    - Azure OpenAI: サポート
    - OpenAI 互換: サポート
    - GitHub Copilot: 非対応。指定時は `NotSupportedException` を返します

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
| `"CodexAppServer"` | `MeAiUtility.MultiProvider.CodexAppServer` |

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

### `AddGitHubCopilot`（推奨）

```csharp
IServiceCollection AddGitHubCopilot(this IServiceCollection services, IConfiguration configuration)
```

GitHub Copilot プロバイダーを実運用向けに一括登録します。内部で `AddGitHubCopilotProvider()` と `AddGitHubCopilotSdkWrapper()` の両方を呼び出します。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Configuration`

#### GitHub Copilot の構成パターン

| パターン | 用途 | 挙動 |
|---|---|---|
| `AddGitHubCopilot(configuration)` | 本番利用（推奨） | provider + SDK wrapper を一括登録 |
| `AddGitHubCopilotProvider(configuration)` + `AddGitHubCopilotSdkWrapper()` | 段階的に登録したい場合 | 上記と同等 |
| `AddGitHubCopilotProvider(configuration)` + 独自 `ICopilotSdkWrapper` | テスト・カスタム実装 | 独自 wrapper が優先される |
| `AddGitHubCopilotProvider(configuration)` のみ | 設定漏れ検出 | 実行時に fail-fast (`InvalidOperationException`) |

---

### `AddGitHubCopilotProvider`

```csharp
IServiceCollection AddGitHubCopilotProvider(this IServiceCollection services, IConfiguration configuration)
```

GitHub Copilot プロバイダーを DI に登録します。`appsettings.json` の `MultiProvider:GitHubCopilot` セクションを読み込みます。

このメソッド単体では `ICopilotSdkWrapper` に fail-fast 実装が登録され、`ListModelsAsync` / `SendAsync` 呼び出し時に `InvalidOperationException` を返します。実運用では `AddGitHubCopilot()` または `AddGitHubCopilotSdkWrapper()` を使用してください。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Configuration`

---

### `AddGitHubCopilotSdkWrapper`

```csharp
IServiceCollection AddGitHubCopilotSdkWrapper(this IServiceCollection services)
```

`ICopilotSdkWrapper` を、公式 `GitHub.Copilot.SDK` ベースの `GitHubCopilotSdkWrapper` で上書き登録します。`AddGitHubCopilotProvider()` の後で呼び出してください。

互換目的で `AddGitHubCopilotCliSdkWrapper()` も残していますが、内部的には同じ SDK ベース実装を登録する旧 API です。

**名前空間：** `MeAiUtility.MultiProvider.GitHubCopilot.Configuration`

---

### `AddCodexAppServer`

```csharp
IServiceCollection AddCodexAppServer(this IServiceCollection services, IConfiguration configuration)
```

Codex App Server プロバイダーを DI に登録します。`appsettings.json` の `MultiProvider:CodexAppServer` セクションを読み込みます。

**名前空間：** `MeAiUtility.MultiProvider.CodexAppServer.Configuration`

---

## プロバイダー別オプション

### OpenAI プロバイダー（`OpenAIProviderOptions`）

`appsettings.json` セクション：`MultiProvider:OpenAI`

実装は公式の `Microsoft.Extensions.AI.OpenAI` を利用し、本ライブラリの `IChatClient` / `IEmbeddingGenerator` へ bridge しています。通常テストでは unit / integration に加えて、ローカル Kestrel スタブサーバーを使った CI 安全な E2E テストで HTTP 経路も確認しています。

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

実装は公式の `Azure.AI.OpenAI` を利用し、本ライブラリの `IChatClient` / `IEmbeddingGenerator` へ bridge しています。通常テストではローカル Kestrel スタブサーバーを使い、Azure OpenAI 互換の HTTP 応答に対する E2E 経路を確認しています。

| プロパティ | 型 | デフォルト | 必須 | 説明 |
|---|---|---|---|---|
| `Endpoint` | `string` | — | ✅ | Azure OpenAI エンドポイント URL |
| `DeploymentName` | `string` | — | ✅ | デプロイメント名 |
| `ApiVersion` | `string` | `"2024-06-01"` | | API バージョン |
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
| `GitHubToken` | `string?` | `null` | GitHub 認証トークン（`null` / 空文字 / 空白以外が設定されている場合は SDK に `githubToken` を渡し、`useLoggedInUser` は `false` として明示的にトークン認証する） |
| `UseLoggedInUser` | `bool?` | `null` | ログイン済みユーザーの認証情報を使用（`GitHubToken` が `null` / 空文字 / 空白のみの場合に有効。省略時は SDK 既定動作に委譲） |
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

#### `CliPath` 解決方針

- `CliPath` を指定した場合はその値を最優先で使用します。
- 未指定の場合は SDK の既定解決（`COPILOT_CLI_PATH` → bundled CLI → `PATH`）に委譲します。
- 初期化失敗時は `CopilotRuntimeException` に OS / `PATH` / 既知候補パス情報を含めます。

`CliPath` を固定する目安:

1. 複数バージョンの `copilot` が `PATH` に存在して実行体を固定したい場合  
2. CI / サービス実行環境で `PATH` が最小構成になっている場合  
3. npm グローバルの shim (`copilot.cmd`, `copilot.ps1`) ではなく実バイナリを明示したい場合

#### `InfiniteSessionOptions`

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Enabled` | `bool?` | `true` | 無限セッションの有効化 |
| `BackgroundCompactionThreshold` | `double?` | `0.80` | バックグラウンド圧縮を開始するコンテキスト使用率（0.0〜1.0） |
| `BufferExhaustionThreshold` | `double?` | `0.95` | 圧縮完了まで処理をブロックするコンテキスト使用率（0.0〜1.0） |

---

### Codex App Server プロバイダー（`CodexAppServerProviderOptions`）

`appsettings.json` セクション：`MultiProvider:CodexAppServer`

このプロバイダーは `codex app-server` を stdio JSON-RPC で呼び出す run-to-completion 型の `IChatClient` です。認証情報は Codex CLI 側（`codex login`）に委譲され、このライブラリは認証情報を直接管理しません。

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `CodexCommand` | `string` | `"codex"` | Codex CLI 実行ファイル名または絶対パス |
| `CodexArguments` | `IReadOnlyList<string>` | `[]` | Codex CLI に渡す引数（`app-server` はライブラリが自動付与） |
| `Transport` | `string` | `"stdio"` | トランスポート種別（MVP は `stdio` のみ） |
| `ModelId` | `string?` | `null` | 既定モデル ID |
| `ReasoningEffort` | `string` | `"medium"` | 既定推論努力レベル |
| `WorkingDirectory` | `string?` | `null` | 作業ディレクトリ |
| `ApprovalPolicy` | `string` | `"never"` | 承認ポリシー |
| `SandboxMode` | `string` | `"workspace-write"` | サンドボックスモード（`read-only` / `workspace-write` / `danger-full-access`） |
| `NetworkAccess` | `bool` | `false` | `turn/start.sandboxPolicy` に渡すネットワークアクセス設定 |
| `TimeoutSeconds` | `int` | `1800` | 呼び出しタイムアウト秒数 |
| `AutoApprove` | `bool` | `true` | 承認 request 受信時に `acceptForSession` を返すか |
| `CaptureEventsForDiagnostics` | `bool` | `false` | JSON-RPC イベントを診断ログ出力するか |
| `ServiceName` | `string?` | `null` | Codex `serviceName` |
| `Summary` | `string?` | `null` | Codex `summary` |
| `Personality` | `string?` | `null` | Codex `personality` |
| `ThreadReusePolicy` | `CodexThreadReusePolicy` | `AlwaysNew` | スレッド再利用ポリシー（`AlwaysNew` / `ReuseByThreadId` / `ReuseOrCreateByKey`） |
| `ThreadId` | `string?` | `null` | `ReuseByThreadId` で使用する既存 thread ID |
| `ThreadKey` | `string?` | `null` | `ReuseOrCreateByKey` で使用する安定キー |
| `ThreadName` | `string?` | `null` | 永続化時に保存する表示名（非一意） |
| `ThreadStorePath` | `string?` | `null` | thread 永続化 JSON の保存先。未指定時は `%LOCALAPPDATA%\\MeAiUtility\\CodexAppServer\\threads.json` |
| `EnvironmentVariables` | `Dictionary<string,string>?` | `null` | subprocess に渡す環境変数 |

運用上の注意:
- `CodexArguments` は通常未指定（`[]`）のまま使用してください。ライブラリが `app-server` を自動付与します。
- `CodexCommand` が `codex` 系の場合、`CodexArguments` に単独の `"app-server"` を明示すると `InvalidRequestException` で fail-fast します（既定と冗長なため）。
- `Summary` は指定する場合は `auto | concise | detailed | none` のみ有効です。
- `Personality` は指定する場合は `none | friendly | pragmatic` のみ有効です。
- `ThreadReusePolicy = ReuseByThreadId` の場合は `ThreadId` 必須です。
- `ThreadReusePolicy = ReuseOrCreateByKey` の場合は `ThreadKey` 必須です。
- thread reuse を有効にした場合、Codex 側 thread に文脈が保持されます。重複投入を避けるため、原則として「今回の追加指示のみ」を `messages` に渡してください。

推奨の安全寄り既定値:
- `approvalPolicy = "never"`
- `sandboxMode = "workspace-write"`
- `networkAccess = false`

---

## ChatOptions の拡張

`IChatClient.GetResponseAsync` に渡す `ChatOptions` に本ライブラリ独自のパラメータをセットできます。

### `ConversationExecutionOptions`（実行時オーバーライド）

`ChatOptions.AdditionalProperties` に `ConversationExecutionOptions` を設定すると、appsettings.json の設定をリクエスト単位で上書きできます。

```csharp
var options = new ChatOptions();
(options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
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
| `Attachments` | `IReadOnlyList<FileAttachment>?` | GitHub Copilot へ添付するファイル一覧（`Path` は絶対パス必須） |
| `SkillDirectories` | `IReadOnlyList<string>?` | GitHub Copilot の `skillDirectories` を typed 指定 |
| `DisabledSkills` | `IReadOnlyList<string>?` | GitHub Copilot の `disabledSkills` を typed 指定 |
| `TimeoutSeconds` | `int?` | GitHub Copilot 呼び出しの request 単位タイムアウト上書き（`> 0` 必須） |
| `ClientName` | `string?` | クライアント識別名 |
| `WorkingDirectory` | `string?` | 作業ディレクトリ |
| `Streaming` | `bool?` | ストリーミング有効化 |
| `ProviderOverride` | `ProviderOverrideOptions?` | プロバイダーのオーバーライド（後述） |

> `Attachments` / `SkillDirectories` / `DisabledSkills` は GitHub Copilot 専用です。OpenAI / AzureOpenAI / OpenAICompatible では `NotSupportedException` を返します。  
> `TimeoutSeconds` は GitHub Copilot でのみ有効で、他プロバイダーでは無視されます。

**名前空間：** `MeAiUtility.MultiProvider.Options`

---

### `ExtensionParameters`（プロバイダー固有パラメータ）

`ChatOptions.AdditionalProperties` に `ExtensionParameters` を設定すると、プロバイダー固有のパラメータを渡せます。キーは `"<provider>.<param>"` 形式（例：`"openai.logprobs"`, `"azure.deploymentName"`, `"copilot.configDir"`, `"codex.approvalPolicy"`）です。

```csharp
var ext = new ExtensionParameters();
ext.Set("copilot.configDir", "/custom/.copilot");
ext.Set("openai.logprobs", true);
ext.Set("codex.approvalPolicy", "never");

var options = new ChatOptions();
(options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.extensions"] = ext;
```

#### `ExtensionParameters` のメソッド

| メソッド | シグネチャ | 説明 |
|---|---|---|
| `Set` | `void Set(string key, object? value)` | パラメータをセット。キーは `"provider.param"` 形式必須 |
| `Get<T>` | `T Get<T>(string key)` | パラメータを取得。存在しない場合は `KeyNotFoundException` |
| `TryGet<T>` | `bool TryGet<T>(string key, out T? value)` | パラメータを安全に取得 |
| `Has` | `bool Has(string key)` | パラメータが存在するか確認 |
| `GetAllForProvider` | `IReadOnlyDictionary<string, object?> GetAllForProvider(string providerName)` | 指定プロバイダーのパラメータを一括取得 |

使用できるプレフィックス：`openai` / `azure` / `copilot` / `codex`

#### Codex 拡張キー（`codex.*`）

| キー | 受け取り先 | 説明 |
|---|---|---|
| `codex.threadReusePolicy` | `CodexRuntimeOptions.ThreadReusePolicy` | `alwaysNew` / `reuseByThreadId` / `reuseOrCreateByKey` |
| `codex.threadId` | `CodexRuntimeOptions.ThreadId` | `ReuseByThreadId` で使用する既存 thread ID |
| `codex.threadKey` | `CodexRuntimeOptions.ThreadKey` | `ReuseOrCreateByKey` で使用する thread キー |
| `codex.threadName` | `CodexRuntimeOptions.ThreadName` | 新規作成時に保存する表示名 |
| `codex.threadStorePath` | `CodexRuntimeOptions.ThreadStorePath` | 永続化 JSON の保存先 |

#### Copilot 拡張キー（`copilot.*`）

| キー | 受け取り先 | 説明 |
|---|---|---|
| `copilot.mode` / `copilot.messageMode` | `MessageOptions.Mode` | 実行モード（例: `plan` / `edit`） |
| `copilot.configDir` / `copilot.config_dir` | `SessionConfig.ConfigDir` | Copilot 設定ディレクトリ |
| `copilot.workingDirectory` / `copilot.working_directory` | `SessionConfig.WorkingDirectory` | 実行時作業ディレクトリ |
| `copilot.availableTools` / `copilot.available_tools` | `SessionConfig.AvailableTools` | 利用可能ツール |
| `copilot.excludedTools` / `copilot.excluded_tools` | `SessionConfig.ExcludedTools` | 除外ツール |
| `copilot.mcpServers` / `copilot.mcp_servers` | `SessionConfig.McpServers` | MCP サーバー定義 |
| `copilot.agent` | `SessionConfig.Agent` | 使用する agent 名 |
| `copilot.skillDirectories` / `copilot.skill_directories` | `SessionConfig.SkillDirectories` | typed `SkillDirectories` 未指定時のフォールバック |
| `copilot.disabledSkills` / `copilot.disabled_skills` | `SessionConfig.DisabledSkills` | typed `DisabledSkills` 未指定時のフォールバック |

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

GitHub Copilot プロバイダーが使用する SDK ラッパーのインターフェースです。`AddGitHubCopilotProvider` 単体では fail-fast 実装が登録されるため、実運用では `AddGitHubCopilot()` もしくは `AddGitHubCopilotSdkWrapper()` を使用してください。テスト用途では独自実装を `ICopilotSdkWrapper` として DI に差し替えられます。

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
| `Attachments` | `IReadOnlyList<FileAttachment>?` | 添付ファイル一覧 |
| `SkillDirectories` | `IReadOnlyList<string>?` | skill ディレクトリ一覧 |
| `DisabledSkills` | `IReadOnlyList<string>?` | 無効化する skill 一覧 |
| `TimeoutSeconds` | `int?` | request 単位タイムアウト |
| `ProviderOverride` | `ProviderOverrideOptions?` | プロバイダーのオーバーライド |
| `AdvancedOptions` | `Dictionary<string, object?>` | プロバイダー固有の追加オプション |

#### ICopilotSdkWrapper の差し替え方法

```csharp
// 既定の SDK wrapper を使う場合
services.AddGitHubCopilotProvider(configuration);
services.AddGitHubCopilotSdkWrapper();
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
| `ListModelsAsync(CancellationToken)` | `Task<IReadOnlyList<CopilotModelInfo>>` | 実 Copilot SDK が提供する model id 一覧を取得する |

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
| `CopilotRuntimeException` | GitHub Copilot CLI / SDK の実行エラー | `CliPath`（string?）、`ExitCode`（int?）、`Operation`（`CopilotOperation?`） |

`CopilotOperation` の値:

- `ClientInitialization`: Copilot client 初期化時の失敗  
- `ListModels`: モデル一覧取得時の失敗  
- `Send`: メッセージ送信時の失敗

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

        return response.Text;
    }
}
```

---

### GitHub Copilot SDK でのチャット

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
// Program.cs（公開 SDK wrapper を使って実際の Copilot へ接続する）
builder.Services.AddMultiProviderChat(builder.Configuration);
builder.Services.AddGitHubCopilot(builder.Configuration);
```

```csharp
// 実行時に有効な model id を取得する
var catalog = serviceProvider.GetRequiredService<ICopilotModelCatalog>();
var models = await catalog.ListModelsAsync();
```

---

### GitHub Copilot でファイル添付 + SkillDirectories を使う

```csharp
var options = new ChatOptions();
(options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
{
    TimeoutSeconds = 300,
    SkillDirectories = [@"D:\skills"],
    DisabledSkills = ["legacy-skill"],
    Attachments =
    [
        new FileAttachment
        {
            Path = @"D:\input\payload.json",
            DisplayName = "payload.json",
        },
    ],
};

var response = await chatClient.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "Read attached JSON and summarize."),
], options);
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
(options.AdditionalProperties ??= new AdditionalPropertiesDictionary())["meai.execution"] = new ConversationExecutionOptions
{
    ModelId = "claude-sonnet-4.6",
    ReasoningEffort = ReasoningEffortLevel.High,
};

var response = await chatClient.GetResponseAsync(messages, options);
```

# Tasks: MEAI マルチプロバイダー抽象化ライブラリ

**Input**: Design documents from `/specs/002-meai-multi-provider/`
**Branch**: `002-meai-multi-provider`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tech Stack**: .NET 8.0 LTS + .NET 10.0, NUnit + Moq, Microsoft.Extensions.AI, GitHub Copilot SDK
**Tests**: ユニットテスト必須（NUnit + Moq）。実プロバイダーAPIキー不要（スタブ/モック使用）。

**Organization**: User Story 単位でフェーズを構成。各フェーズは依存関係を明示したうえで段階的にテスト可能。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 並列実行可能（別ファイル、未完了タスクへの依存なし）
- **[Story]**: 対応する User Story（US1〜US4）
- タスク説明には正確なファイルパスを含める

---

## Phase 1: セットアップ（共有インフラ）

**目的**: ソリューション・プロジェクト構造の初期化、共通ビルド設定の確立

- [X] T001 ソリューションファイルを作成する `MeAiUtility.sln`
- [X] T002 コアパッケージプロジェクトを作成する `src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj`（TargetFrameworks: net8.0;net10.0, Nullable, ImplicitUsings 有効化）
- [X] T003 [P] OpenAI プロバイダーパッケージを作成する `src/MeAiUtility.MultiProvider.OpenAI/MeAiUtility.MultiProvider.OpenAI.csproj`（TargetFrameworks: net8.0;net10.0）
- [X] T004 [P] Azure OpenAI プロバイダーパッケージを作成する `src/MeAiUtility.MultiProvider.AzureOpenAI/MeAiUtility.MultiProvider.AzureOpenAI.csproj`（TargetFrameworks: net8.0;net10.0）
- [X] T005 [P] GitHub Copilot プロバイダーパッケージを作成する `src/MeAiUtility.MultiProvider.GitHubCopilot/MeAiUtility.MultiProvider.GitHubCopilot.csproj`（TargetFrameworks: net8.0;net10.0）
- [X] T006 [P] サンプルコンソールアプリプロジェクトを作成する `src/MeAiUtility.MultiProvider.Samples/MeAiUtility.MultiProvider.Samples.csproj`
- [X] T007 コアパッケージテストプロジェクトを作成する `tests/MeAiUtility.MultiProvider.Tests/MeAiUtility.MultiProvider.Tests.csproj`（NUnit, Moq 参照追加）
- [X] T008 [P] OpenAI プロバイダーテストプロジェクトを作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/MeAiUtility.MultiProvider.OpenAI.Tests.csproj`
- [X] T009 [P] Azure OpenAI プロバイダーテストプロジェクトを作成する `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests.csproj`
- [X] T010 [P] GitHub Copilot プロバイダーテストプロジェクトを作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests.csproj`
- [X] T011 [P] 統合テストプロジェクトを作成する `tests/MeAiUtility.MultiProvider.IntegrationTests/MeAiUtility.MultiProvider.IntegrationTests.csproj`
- [X] T012 [P] Directory.Build.props を作成してバージョン・AnalyzerRule 等を集中管理する `Directory.Build.props`
- [X] T013 [P] .gitignore に appsettings.Development.json、*.user、bin/、obj/ 等を追加する `.gitignore`
- [X] T014 `dotnet restore` を実行し、全 NuGet パッケージが正常に取得できることを確認する（GitHub.Copilot.SDK, Microsoft.Extensions.AI.*, Azure.Identity 等の存在確認）
- [X] T015 `dotnet build -f net8.0` と `dotnet build -f net10.0` でソリューション全体がビルド成功することを確認する（エラーゼロ）

**チェックポイント**: ソリューション構造が確立され `dotnet restore && dotnet build -f net8.0 && dotnet build -f net10.0` が通る状態

---

## Phase 2: 基盤（全 User Story の前提）

**目的**: 全プロバイダーで共通利用される型・インターフェース・DI基盤の実装

**重要**: このフェーズが完了するまで User Story の実装を開始できない

### 例外階層

- [X] T015a `MultiProviderException` 基底例外クラスを実装する `src/MeAiUtility.MultiProvider/Exceptions/MultiProviderException.cs`（ProviderName, TraceId, Timestamp, StatusCode, ResponseBody プロパティ必須）
- [X] T016 [P] `AuthenticationException`、`RateLimitException`、`InvalidRequestException`、`ProviderException`、`TimeoutException`、`NotSupportedException`、`CopilotRuntimeException` を実装する `src/MeAiUtility.MultiProvider/Exceptions/`（各クラスは MultiProviderException 派生。HTTP系例外は StatusCode / ResponseBody を保持し、非HTTP例外は TimeoutSeconds / FeatureName / CliPath / ExitCode など原因特定に必要な文脈情報を保持）

### コア型・インターフェース

- [X] T017 `IProviderCapabilities` インターフェースを定義する `src/MeAiUtility.MultiProvider/Abstractions/IProviderCapabilities.cs`（IsSupported(FeatureName) メソッド、SupportsReasoningEffort/SupportsStreaming/SupportsModelDiscovery 等のプロパティ）
- [X] T018 [P] `IProviderFactory` インターフェースを定義する `src/MeAiUtility.MultiProvider/Abstractions/IProviderFactory.cs`
- [X] T019 [P] `ReasoningEffortLevel` 列挙型を定義する `src/MeAiUtility.MultiProvider/Options/ReasoningEffortLevel.cs`（Low, Medium, High, XHigh）
- [X] T020 [P] `SystemMessageMode` 列挙型を定義する `src/MeAiUtility.MultiProvider/Options/SystemMessageMode.cs`（Append, Replace）
- [X] T021 [P] `ProviderOverrideOptions` クラスを実装する `src/MeAiUtility.MultiProvider/Options/ProviderOverrideOptions.cs`（Type, BaseUrl, ApiKey, BearerToken, AzureApiVersion）
- [X] T022 `ConversationExecutionOptions` クラスを実装する `src/MeAiUtility.MultiProvider/Options/ConversationExecutionOptions.cs`（ModelId, ReasoningEffort, SystemMessageMode, AllowedTools, ExcludedTools, ClientName, WorkingDirectory, Streaming, ProviderOverride）、`ChatOptions.AdditionalProperties["meai.execution"]` からの読み出しロジック含む
- [X] T023 [P] `ExtensionParameters` クラスを実装する `src/MeAiUtility.MultiProvider/Options/ExtensionParameters.cs`（`{provider}.{param}` キー規約検証、Set/Get<T>/TryGet<T>/Has/GetAllForProvider メソッド）
- [X] T024 [P] `CommonProviderOptions` クラスを実装する `src/MeAiUtility.MultiProvider/Options/CommonProviderOptions.cs`（DefaultTemperature, DefaultMaxTokens, DefaultTimeout, EnableTelemetry, CapturePrompts, LogRequestResponse, MaskSensitiveData）
- [X] T025 `MultiProviderOptions` クラスを実装する `src/MeAiUtility.MultiProvider/Options/MultiProviderOptions.cs`（Provider, OpenAI, AzureOpenAI, OpenAICompatible, GitHubCopilot, Common）、Provider 値検証ロジック含む

### テレメトリ・ロギング

- [X] T026 `ChatTelemetry` クラスを実装する `src/MeAiUtility.MultiProvider/Telemetry/ChatTelemetry.cs`（TraceId, RequestId, Timestamp, ProviderId, ModelId, ReasoningEffort プロパティ、OpenTelemetry ActivitySource 統合）
- [X] T027 [P] `LoggingExtensions` を実装する `src/MeAiUtility.MultiProvider/Telemetry/LoggingExtensions.cs`（機密情報マスキング、トレース相関ID付与。APIキー・トークンはマスク必須）
- [X] T027a [P] `ChatTelemetry` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Telemetry/ChatTelemetryTests.cs`（TraceId/StatusCode/ReasoningEffort の記録、例外時タグ付けを検証）
- [X] T027b [P] `LoggingExtensions` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Telemetry/LoggingExtensionsTests.cs`（機密情報マスキング、StatusCode/ResponseBody の記録、トレース相関ID付与を検証）

### DI 登録・設定バインド

- [X] T028 `ProviderRegistry` を実装する `src/MeAiUtility.MultiProvider/Configuration/ProviderRegistry.cs`（プロバイダー名→IChatClient 実装のマッピング管理）
- [X] T028a [P] `ProviderFactory` を実装する `src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs`（`IProviderFactory` 実装。`ProviderRegistry` と DI から選択済みプロバイダーを解決）
- [X] T029 `ProviderConfigurationExtensions` を実装する `src/MeAiUtility.MultiProvider/Configuration/ProviderConfigurationExtensions.cs`（`AddMultiProviderChat(IServiceCollection, IConfiguration)` 拡張メソッド。MultiProviderOptions.ValidateOnStart 設定。Provider 指定がない場合は起動時例外）

### 基盤テスト

- [X] T030 `MultiProviderException` と各派生例外のユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Exceptions/MultiProviderExceptionTests.cs`（TraceId/StatusCode/ResponseBody の保持に加え、TimeoutSeconds / FeatureName / CliPath / ExitCode など非HTTP例外の文脈情報を検証）
- [X] T031 [P] `ConversationExecutionOptions` の `ChatOptions.AdditionalProperties["meai.execution"]` 読み出しロジックのユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Options/ConversationExecutionOptionsTests.cs`
- [X] T032 [P] `ExtensionParameters` の Set/Get/検証ロジックのユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Options/ExtensionParametersTests.cs`
- [X] T033 [P] `MultiProviderOptions` の検証ロジック（Provider 値、対応セクション必須）のユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Options/MultiProviderOptionsTests.cs`
- [X] T034 [P] `ProviderRegistry` の登録・解決ロジックのユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Configuration/ProviderRegistryTests.cs`
- [X] T034a [P] `ProviderFactory` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.Tests/Configuration/ProviderFactoryTests.cs`（未知 Provider の拒否、選択済み実装の解決を検証）
- [X] T035 `dotnet test -f net8.0` と `dotnet test -f net10.0` でフェーズ2のテストが全件 Pass することを確認する

**チェックポイント**: コア型・DI 基盤が確立。`dotnet build && dotnet test` が全件 Pass。

---

## Phase 3: User Story 1 - 設定ベースのプロバイダー切り替え (Priority: P1)

**目標**: appsettings.json の Provider フィールド変更のみで、アプリコードを変更せずに 4 プロバイダーすべてに切り替えられる。

**Independent Test**: 各プロバイダー用のスタブ IChatClient を用意し、`AddMultiProviderChat` を呼び出してプロバイダーを切り替えても同一の IChatClient インターフェースが返り、正しいスタブが注入されることをテストで確認する。

### US1: OpenAI アダプタ

- [X] T036 [P] [US1] `OpenAIProviderOptions` クラスを実装する `src/MeAiUtility.MultiProvider.OpenAI/Options/OpenAIProviderOptions.cs`（ApiKey, OrganizationId, BaseUrl, ModelName, TimeoutSeconds。ApiKey 非空バリデーション含む）
- [X] T037 [US1] `OpenAIChatClientAdapter` を実装する `src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs`（Microsoft.Extensions.AI.OpenAI の既存 OpenAIChatClient をラップ。temperature / max tokens / ConversationExecutionOptions / ChatOptions.StopSequences / CancellationToken を OpenAI 設定へ変換。ExtensionParameters の openai.* キーのみ解釈し他プレフィックスは送信前例外）
- [X] T038 [US1] `OpenAICompatibleProvider` を実装する `src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs`（BaseUrl 差し替え + ModelMapping + stop sequences / error payload 保持。既定動作として互換崩れは fail-fast で即例外とし、`StrictCompatibilityMode` は追加の互換性検証を有効化する）
- [X] T039 [US1] OpenAI プロバイダーの DI 登録拡張 `AddOpenAIProvider` を実装する `src/MeAiUtility.MultiProvider.OpenAI/Configuration/OpenAIServiceExtensions.cs`

### US1: Azure OpenAI アダプタ

- [X] T040 [P] [US1] `AzureAuthenticationOptions` クラスと `AuthenticationType` 列挙型を実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/Options/AzureAuthenticationOptions.cs`（Type=ApiKey 時は ApiKey 必須、Type=EntraId 時は ApiKey null バリデーション）
- [X] T041 [P] [US1] `AzureOpenAIProviderOptions` クラスを実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/Options/AzureOpenAIProviderOptions.cs`（Endpoint, DeploymentName, ApiVersion, Authentication, TimeoutSeconds）
- [X] T042 [US1] `AzureOpenAIChatClientAdapter` を実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs`（Microsoft.Extensions.AI.AzureAIInference の既存実装をラップ。temperature / max tokens / ExtensionParameters の azure.* キーのみ解釈）
- [X] T043 [US1] Azure OpenAI プロバイダーの DI 登録拡張 `AddAzureOpenAIProvider` を実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/Configuration/AzureOpenAIServiceExtensions.cs`

### US1: GitHub Copilot ベースライン切り替え

- [X] T043a [P] [US1] `GitHubCopilotProviderOptions` のベースライン接続設定を実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs`（CliPath, CliArgs, CliUrl, UseStdio, GitHubToken, UseLoggedInUser, TimeoutSeconds など基本接続設定）
- [X] T043b [P] [US1] `ICopilotSdkWrapper` のベースライン chat API を定義する `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`（基本 chat 実行に必要な session 作成/送信 API を抽象化）
- [X] T043c [US1] `CopilotClientHost` のベースライン接続管理を実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs`（CLI 接続/破棄と basic chat 用 wrapper 解決）
- [X] T043d [US1] `GitHubCopilotChatClient` のベースライン chat 実行を実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`（高度な session option なしで basic chat を実行し、構成切り替え対象として利用可能にする）
- [X] T043e [US1] GitHub Copilot プロバイダーのベースライン DI 登録拡張 `AddGitHubCopilotProvider` を実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs`

### US1: プロバイダー切り替えテスト

- [X] T044 [P] [US1] `OpenAIChatClientAdapter` のユニットテスト（モック HttpMessageHandler 使用）を作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/OpenAIChatClientAdapterTests.cs`（temperature / max tokens / stop sequences / CancellationToken 伝播、HTTPエラー時の StatusCode / ResponseBody / TraceId 保持を含む）
- [X] T044a [P] [US1] `OpenAICompatibleProvider` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/OpenAICompatibleProviderTests.cs`（BaseUrl 差し替え、ModelMapping、互換崩れ時の fail-fast、HTTPエラー時の StatusCode / ResponseBody / TraceId 保持を検証）
- [X] T045 [P] [US1] `AzureOpenAIChatClientAdapter` のユニットテスト（ApiKey/EntraId 認証の両経路）を作成する `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/AzureOpenAIChatClientAdapterTests.cs`（temperature / max tokens / stop sequences / CancellationToken 伝播、HTTPエラー時の StatusCode / ResponseBody / TraceId 保持を含む）
- [X] T045a [P] [US1] `GitHubCopilotChatClient` のベースライン unit test を作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs`（基本 chat 実行、プロバイダー切替、TraceId を検証）
- [X] T045b [P] [US1] `CopilotClientHost` のベースライン unit test を作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/CopilotClientHostTests.cs`（接続確立/破棄、起動失敗時の例外を検証）
- [X] T046 [US1] `ProviderSwitchTests` 統合テストを作成する `tests/MeAiUtility.MultiProvider.IntegrationTests/ConfigurationTests/ProviderSwitchTests.cs`（4 プロバイダー設定を持つ appsettings-test.json を切り替え、正しい IChatClient 実装が解決されることを確認。スタブ使用）
- [X] T047 [US1] `dotnet test -f net8.0` と `dotnet test -f net10.0` で US1 関連テストが全件 Pass することを確認する

**チェックポイント**: US1 完了。設定変更だけでプロバイダー切り替えが動作し、テストが全件 Pass。

---

## Phase 4: User Story 2 - 共通セッションパラメータでのチャット実行 (Priority: P1)

**目標**: model, reasoning effort, system message, tool allow/deny, streaming 等の共通セッション設定を、すべてのプロバイダーで情報欠落なく指定・実行できる。reasoning effort が未対応モデル/プロバイダーの場合は送信前に NotSupportedException が返る。

**Independent Test**: `ConversationExecutionOptions`（ModelId="gpt-5", ReasoningEffort=High, AllowedTools=["view"], Streaming=true）を `ChatOptions.AdditionalProperties["meai.execution"]` に設定し、`ChatOptions.StopSequences` と `CancellationToken` も合わせて Copilot SDK モック向けに正しく SessionConfig / 呼び出し制御へ変換されることを検証。reasoning effort 未対応モデルで NotSupportedException が返ることを確認。

### US2: GitHub Copilot アダプタの高度設定拡張（reasoning effort 含む）

- [X] T048 [P] [US2] `GitHubCopilotProviderOptions` に高度な session 設定を拡張実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/Options/GitHubCopilotProviderOptions.cs`（LogLevel, AutoStart, AutoRestart, EnvironmentVariables を追加し、セッション既定値: ModelId, ReasoningEffort, SystemMessageMode, AvailableTools, ExcludedTools, ClientName, WorkingDirectory, Streaming, ConfigDir, InfiniteSessions, ProviderOverride を実装）
- [X] T049 [P] [US2] `InfiniteSessionOptions` クラスを実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/Options/InfiniteSessionOptions.cs`（Enabled, BackgroundCompactionThreshold, BufferExhaustionThreshold）
- [X] T050 [P] [US2] `ICopilotSdkWrapper` に capability 問い合わせ API を拡張定義する `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs`（`ListModelsAsync()`, `CreateSessionAsync(SessionConfig)` 等の高度な SDK 呼び出しを抽象化し、テスト時のモック注入を可能にする）
- [X] T050a [US2] `CopilotClientHost` に capability-aware な runtime 管理を拡張実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs`（CLI 起動失敗時の詳細例外、TraceId 付与、wrapper 解決を強化）
- [X] T051 [US2] `GitHubCopilotChatClient` に高度な session option 変換を拡張実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`（CopilotClientHost 使用。temperature / max tokens / ConversationExecutionOptions / ChatOptions.StopSequences / CancellationToken を SessionConfig / 実行制御へ変換。ReasoningEffort 指定時は ListModelsAsync() でモデル能力を取得し、Capabilities.Supports.ReasoningEffort == false なら送信前に NotSupportedException をスロー。ExtensionParameters["copilot.*"] を advanced options へ変換。IProviderCapabilities 実装を提供）
- [X] T052 [US2] GitHub Copilot プロバイダーの DI 登録拡張 `AddGitHubCopilotProvider` を高度設定対応に拡張する `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs`

### US2: 共通 ConversationExecutionOptions 変換ロジック

- [X] T053 [P] [US2] `OpenAIChatClientAdapter` に `ConversationExecutionOptions` 変換（ModelId, SystemMessageMode, Streaming, temperature, max tokens, ChatOptions.StopSequences, CancellationToken）を追加する `src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs`
- [X] T054 [P] [US2] `AzureOpenAIChatClientAdapter` に `ConversationExecutionOptions` 変換（ModelId, SystemMessageMode, Streaming, temperature, max tokens, ChatOptions.StopSequences, CancellationToken）を追加する `src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs`
- [X] T055 [P] [US2] `ProviderCapabilities` 実装を OpenAI/Azure OpenAI アダプタに追加する（SupportsReasoningEffort=true が返る場合のみ ReasoningEffort を受け付け、それ以外は NotSupportedException）

### US2: テスト

- [X] T056 [US2] `GitHubCopilotChatClient` のユニットテスト（SDK モック使用）を作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs`（SessionConfig への変換検証、temperature / max tokens / stop sequences / CancellationToken の伝播、reasoning effort 非対応モデルでの例外検証、TraceId 付与を含む）
- [X] T057 [P] [US2] `CopilotClientHost` のユニットテスト（SDK モック使用）を作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/CopilotClientHostTests.cs`（CLI 起動失敗時の CopilotRuntimeException、TraceId 付与、`Exception.ToString()` を含むログ記録を検証）
- [X] T058 [P] [US2] `ChatClientContractTests` を作成する `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/ChatClientContractTests.cs`（全プロバイダースタブで GetResponseAsync / GetStreamingResponseAsync が共通 IChatClient 契約、temperature、max tokens、stop sequences、CancellationToken を満たすことを検証）
- [X] T059 [US2] `dotnet test -f net8.0` と `dotnet test -f net10.0` で US2 関連テストが全件 Pass することを確認する

**チェックポイント**: US2 完了。全プロバイダーで共通セッション設定が動作し、reasoning effort 事前検証が機能する。

---

## Phase 5: User Story 3 - ベンダー固有拡張または高度セッション設定の指定 (Priority: P2)

**目標**: ExtensionParameters 経由でベンダー固有の高度設定（azure.data_sources, copilot.mcp_servers, Copilot BYOK ProviderOverride）を、共通 I/F を破壊せずに指定・実行できる。未対応/型不一致の拡張は黙って捨てられず送信前例外となる。

**Independent Test**: Azure OpenAI アダプタに azure.data_sources を ExtensionParameters 経由で渡し、HTTP モックで Azure OpenAI リクエスト本体に data_sources が含まれていることを確認。Copilot アダプタに ProviderOverride を指定し、SDK に渡される Provider 設定が正しいことを確認。openai.top_logprobs を AzureOpenAI アダプタに渡した場合に送信前例外が返ることを確認。

### US3: 拡張パラメータ変換実装

- [X] T060 [P] [US3] `OpenAIChatClientAdapter` に `ExtensionParameters["openai.*"]` の変換ロジックを実装する `src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs`（openai.top_logprobs, openai.response_format 等を OpenAI リクエストに注入。他プレフィックスのキーは送信前に InvalidRequestException）
- [X] T061 [P] [US3] `AzureOpenAIChatClientAdapter` に `ExtensionParameters["azure.*"]` の変換ロジックを実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs`（azure.data_sources を Azure OpenAI リクエスト body に注入。他プレフィックスは送信前に InvalidRequestException）
- [X] T062 [US3] `GitHubCopilotChatClient` に `ExtensionParameters["copilot.*"]` の変換ロジックを実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`（copilot.mcp_servers, copilot.custom_agents を SessionConfig.McpServers/CustomAgents へ渡す。他プレフィックスは送信前に InvalidRequestException）
- [X] T063 [US3] `ConversationExecutionOptions.ProviderOverride` を `GitHubCopilotChatClient` の SessionConfig.Provider へ変換する処理を実装する `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs`（ProviderOverride が設定されている場合に Copilot SDK の BYOK provider override として SessionConfig に反映。他プロバイダーで ProviderOverride が指定されている場合は NotSupportedException）

### US3: テスト

- [X] T064 [P] [US3] `OpenAIChatClientAdapter` の拡張パラメータ変換・検証テストを作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/ExtensionParametersOpenAITests.cs`（azure.* キーや `openai.top_logprobs` の型不一致値で InvalidRequestException、openai.* キーで正常変換を検証）
- [X] T065 [P] [US3] `AzureOpenAIChatClientAdapter` の azure.data_sources 変換テストを作成する `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/ExtensionParametersAzureTests.cs`（HTTP モックでリクエスト本体の data_sources 含有を確認し、`azure.data_sources` の型不一致値で送信前例外を検証）
- [X] T066 [P] [US3] `GitHubCopilotChatClient` の ProviderOverride 変換テストを作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/ProviderOverrideTests.cs`（ProviderOverride の正常変換、未対応プロバイダー指定時の例外、TraceId を含む失敗ログを検証）
- [X] T067 [P] [US3] `ProviderSpecificTests` を作成する `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/ProviderSpecificTests.cs`（BYOK, data_sources がそれぞれ正しく動作し、非対応プロバイダーまたは型不一致値では送信前例外になることを検証）
- [X] T068 [US3] `dotnet test -f net8.0` と `dotnet test -f net10.0` で US3 関連テストが全件 Pass することを確認する

**チェックポイント**: US3 完了。BYOK, Azure data_sources 等のベンダー固有設定が共通 I/F 経由で指定可能。

---

## Phase 6: User Story 4 - Embedding 生成（将来拡張） (Priority: P3)

**目標**: `IEmbeddingGenerator<string, Embedding<float>>` 準拠の Embedding 生成 I/F を OpenAI/Azure OpenAI で提供する。OpenAICompatible/GitHubCopilot は非対応として NotSupportedException。

**Independent Test**: OpenAI/Azure OpenAI アダプタで GenerateEmbeddingAsync("test text") を呼び出し、モック HTTP で float[] が返ることを確認。OpenAICompatible/GitHubCopilot では GenerateEmbeddingAsync 呼び出し時に NotSupportedException が返ることを確認。

- [X] T069 [P] [US4] `IEmbeddingGenerator<string, Embedding<float>>` インターフェース確認・再エクスポートを `src/MeAiUtility.MultiProvider/Abstractions/` に追加する（MEAI 標準の IEmbeddingGenerator を使用）
- [X] T070 [P] [US4] `OpenAIEmbeddingAdapter` を実装する `src/MeAiUtility.MultiProvider.OpenAI/OpenAIEmbeddingAdapter.cs`（Microsoft.Extensions.AI.OpenAI の既存 EmbeddingGenerator をラップ）
- [X] T071 [P] [US4] `AzureOpenAIEmbeddingAdapter` を実装する `src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIEmbeddingAdapter.cs`
- [X] T072 [US4] DI 登録拡張 `AddEmbeddingGenerator` を各プロバイダーパッケージに追加する（OpenAI: `src/MeAiUtility.MultiProvider.OpenAI/Configuration/OpenAIServiceExtensions.cs`、Azure OpenAI: `src/MeAiUtility.MultiProvider.AzureOpenAI/Configuration/AzureOpenAIServiceExtensions.cs`、OpenAICompatible/GitHubCopilot 選択時は fail-fast な `UnsupportedEmbeddingGenerator` または同等の実装を登録し、`GenerateEmbeddingAsync` 呼び出し時に `NotSupportedException` を返す）
- [X] T073 [P] [US4] `OpenAIEmbeddingAdapter` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/OpenAIEmbeddingAdapterTests.cs`
- [X] T074 [P] [US4] `AzureOpenAIEmbeddingAdapter` のユニットテストを作成する `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/AzureOpenAIEmbeddingAdapterTests.cs`
- [X] T074a [P] [US4] `GitHubCopilot` の Embedding 非対応テストを作成する `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotEmbeddingTests.cs`（`GenerateEmbeddingAsync` 呼び出し時に NotSupportedException が返ることを検証）
- [X] T074b [P] [US4] `OpenAICompatible` の Embedding 非対応テストを作成する `tests/MeAiUtility.MultiProvider.OpenAI.Tests/OpenAICompatibleEmbeddingTests.cs`（`GenerateEmbeddingAsync` 呼び出し時に NotSupportedException が返ることを検証）
- [X] T075 [US4] `dotnet test -f net8.0` と `dotnet test -f net10.0` で US4 関連テストが全件 Pass することを確認する

**チェックポイント**: US4 完了。OpenAI/Azure OpenAI で Embedding 生成が動作。

---

## Phase 7: ポリッシュ・横断的関心事

**目的**: 全 User Story に影響する改善、サンプル実装、動作確認

- [X] T076 [P] `ProviderCapabilities` の全プロバイダー実装を確認し、機能マトリクスと一致することをテストで検証する `tests/MeAiUtility.MultiProvider.IntegrationTests/ContractTests/CapabilityMatrixTests.cs`（FR-017 対応）
- [X] T077 [P] `BasicChatSample` サンプルを実装する `src/MeAiUtility.MultiProvider.Samples/BasicChatSample.cs`（GitHubCopilot 既定設定で最小チャット実行を示す）
- [X] T078 [P] `ProviderSwitchSample` サンプルを実装する `src/MeAiUtility.MultiProvider.Samples/ProviderSwitchSample.cs`（appsettings.json 切り替えで全プロバイダー動作を示す）
- [X] T079 [P] `ExtensionParametersSample` サンプルを実装する `src/MeAiUtility.MultiProvider.Samples/ExtensionParametersSample.cs`（Azure data_sources と Copilot BYOK override の使用例）
- [X] T079a [P] `SampleSmokeTests` を作成する `tests/MeAiUtility.MultiProvider.IntegrationTests/Samples/SampleSmokeTests.cs`（BasicChatSample / ProviderSwitchSample / ExtensionParametersSample をスタブ構成で自動実行し、主要シナリオが成功することを検証）
- [X] T080 quickstart.md の手順に従い dotnet run が期待通りに動作することを確認する（SC-001〜SC-009 の成功基準の手動検証）
- [X] T081 [P] `ProviderRegistry` に対して `IProviderCapabilities.IsSupported()` を使った事前チェックロジックを追加する `src/MeAiUtility.MultiProvider/Configuration/ProviderRegistry.cs`（DI 解決時に ProviderCapabilities を検証し、不整合があれば起動時例外）
- [X] T082 [P] `dotnet build -warnAsError` でビルド警告ゼロを確認する（Roslyn アナライザー含む）
- [X] T083 `dotnet test -f net8.0 --collect:"XPlat Code Coverage"` と `dotnet test -f net10.0 --collect:"XPlat Code Coverage"` で全テスト Pass かつカバレッジレポートを生成する
- [X] T084 [P] `README.md` にパッケージのセマンティックバージョニング方針、破壊的変更時のメジャーバージョン更新ルール、リリース手順上の確認事項を追記する（FR-020 対応）
- [X] T084a [P] GitHub Actions CI ワークフローを追加する `.github/workflows/ci.yml`（`net8.0` / `net10.0` の restore, build -warnAsError, test を継続実行し、NFR-005 を担保）

**チェックポイント**: 全 User Story が完成。quickstart.md が手順通りに動作する。

---

## 依存関係と実行順序

### フェーズ依存関係

```
Phase 1 (Setup)
    └─▶ Phase 2 (Foundational) ← 全 User Story の前提
            ├─▶ Phase 3 (US1: プロバイダー切り替え) P1
            │       ├─▶ Phase 4 (US2: OpenAI/Azure 共通セッション補強) P1
            │       └─▶ Phase 5 (US3: OpenAI/Azure 拡張パラメータ) P2
            │       └─▶ Phase 4 (US2: GitHub Copilot セッション設定) P1
            │               ├─▶ Phase 5 (US3: Copilot 拡張パラメータ) P2
            │               └─▶ Phase 6 (US4: Copilot Embedding fail-fast) P3
            ├─▶ Phase 6 (US4: OpenAI/Azure Embedding) P3 ← US1 の DI 拡張を再利用
            └─▶ Phase 7 (ポリッシュ) ← Phase 3〜6 完了後
```

### User Story 依存関係

| User Story | 依存 | 独立性 |
|-----------|------|--------|
| US1 (P1): プロバイダー切り替え | Phase 2 完了のみ | 独立してテスト可能 |
| US2 (P1): 共通セッション設定 | Phase 2 完了。OpenAI/Azure 変換タスクは US1 の対応アダプタ完了後。GitHub Copilot advanced option は US1 のベースライン実装完了後 | OpenAI/Azure 系も GitHub Copilot 系も US1 の各ベースライン実装を拡張する |
| US3 (P2): 拡張パラメータ | Phase 2 完了。OpenAI/Azure 系は US1、Copilot 系は US2 の対応アダプタ完了後 | 各プロバイダー枝ごとに段階的に実装/テスト可能 |
| US4 (P3): Embedding | Phase 2 完了。OpenAI/Azure の DI 拡張は US1 の AddOpenAIProvider / AddAzureOpenAIProvider 完了後、Copilot fail-fast は US2 の capability/host 方針確定後 | OpenAI/Azure 実装は US1 に依存し、Copilot fail-fast は US2 に依存 |

### フェーズ内の実行順序

1. 例外型 → 共通オプション型 → DI 基盤（依存あり）
2. 各プロバイダーオプション型はフェーズ内で並列可能
3. アダプタ実装は対応オプション型完了後に開始

---

## 並列実行例

### Phase 3: User Story 1

```
# プロバイダーオプション型を並列作成
T036 (OpenAIProviderOptions) || T040 (AzureAuthenticationOptions) || T041 (AzureOpenAIProviderOptions) || T043a (GitHubCopilotProviderOptions)

# アダプタ実装（オプション型完了後）
T037 (OpenAIChatClientAdapter) || T042 (AzureOpenAIChatClientAdapter) || T043b (ICopilotSdkWrapper) → T043c (CopilotClientHost) → T043d (GitHubCopilotChatClient) → T043e (AddGitHubCopilotProvider)

# テスト（アダプタ完了後に並列実行）
T044 (OpenAI テスト) || T044a (OpenAICompatible テスト) || T045 (Azure テスト) || T045a (Copilot chat テスト) || T045b (Copilot host テスト)
```

### Phase 4: User Story 2

```
# Copilot 設定型を並列作成
T048 (GitHubCopilotProviderOptions) || T049 (InfiniteSessionOptions)

# ICopilotSdkWrapper → CopilotClientHost → GitHubCopilotChatClient（順序依存）
T050 (ICopilotSdkWrapper)
T050a (CopilotClientHost) ← T050 完了後
T051 (GitHubCopilotChatClient) ← T050a 完了後

# 他プロバイダーの ExecutionOptions 変換は並列
T053 (OpenAI変換) || T054 (Azure変換) || T055 (ProviderCapabilities)
```

---

## 実装戦略

### MVP ファースト（User Story 1 のみ）

1. Phase 1 完了: ソリューション構造の確立
2. Phase 2 完了: コア型・DI 基盤（必須・全 US をブロック）
3. Phase 3 完了: US1（設定ベース切り替え）
4. **STOP & VALIDATE**: US1 を独立してテスト・デモ
5. デプロイ/ライブラリ公開の判断

### インクリメンタルデリバリー

1. Phase 1 + 2 → 基盤確立
2. Phase 3 (US1) → **MVP**（全プロバイダーで基本チャットが動く）
3. Phase 4 (US2) → GitHub Copilot SDK の reasoning effort 含む主要機能と全プロバイダーの共通 session 設定拡張
4. Phase 5 (US3) → Azure data_sources 等のベンダー固有高度機能
5. Phase 6 (US4) → Embedding 生成（P3 拡張）
6. Phase 7 → ポリッシュ・サンプル

### 並列チーム戦略

Phase 2 完了後:
- 開発者 A: OpenAI / Azure OpenAI 系（Phase 3 の T036-T045、Phase 5 の T060-T065、Phase 6 の T069-T074）
- 開発者 B: GitHub Copilot 系（Phase 3 の T043a-T045b、Phase 4 の T048-T052 と T056-T057、Phase 6 の T072/T074a）

---

## Notes

- **[P] タスク**: 別ファイル・依存なし → 並列実行可
- **[Story] ラベル**: タスクと User Story のトレーサビリティ
- **GitHub Copilot SDK**: Technical Preview のため API が変化する可能性あり。CopilotClientHost に抽象化境界を集中させること
- **モックの原則**: 全テストで実プロバイダー API キー不要。HTTP は MockHttpMessageHandler、Copilot SDK は `ICopilotSdkWrapper` モック、Azure は MockTokenCredential を使用
- **テスト形式**: 全アサーションは `Assert.That` 形式（NUnit）で記述すること
- **例外のログ**: Exception.ToString() をトレースログへ出力すること（全例外・全プロバイダー共通）
- **機密情報**: APIキー・トークンは LoggingExtensions のマスキングで保護し、テストコードにも含めない
- **FR トレーサビリティ**: FR-008/FR-021 は Phase 4 (US2) で完全対応。FR-006 は Phase 5 (US3) で完全対応


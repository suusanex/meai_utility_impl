# Implementation Plan: MEAI マルチプロバイダー抽象化ライブラリ

**Branch**: `002-meai-multi-provider` | **Date**: 2026-02-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-meai-multi-provider/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

GitHub Copilot SDK を主用途としつつ、OpenAI、Azure OpenAI、OpenAI互換ローカルへ移行しやすい共通インターフェース（Microsoft.Extensions.AI の IChatClient 準拠 + 共通セッション実行オプション）を提供する .NET ライブラリを開発する。構成ファイルの変更のみでプロバイダーを切り替え可能にし、Copilot SDK で必要な model / reasoning effort / tool policy / streaming などの主要 session 設定を失わないことを設計上の最優先要件とする。

## Technical Context

**Language/Version**: .NET 8.0 LTS + .NET 10.0（ライブラリは multi-target、検証も両 TFM を対象）  
**Primary Dependencies**: Microsoft.Extensions.AI（標準チャットクライアントI/F）、GitHub Copilot SDK（Copilot 向け主要バックエンド）、Microsoft.Extensions.DependencyInjection（DI統合）、Microsoft.Extensions.Configuration（構成管理）
**Storage**: N/A（ステートレスライブラリ、永続化なし）  
**Testing**: NUnit + Moq（ユニットテスト）、統合テストはスタブ/モック使用（実プロバイダー呼び出しなし）  
**Target Platform**: .NET ランタイム環境（Windows/Linux/macOS）、クロスプラットフォーム対応
**Project Type**: Class Library（マルチパッケージ構成：コア + プロバイダー別）  
**Performance Goals**: 開発者向けライブラリとして追加オーバーヘッドを最小化する。数値SLOは本計画では固定せず、正確性・明確な失敗・デバッグ容易性を優先する  
**Constraints**: 
  - 実プロバイダーAPIキー不要でテスト可能（スタブ/モック使用）
  - 機密情報（APIキー、エンドポイントURL）をリポジトリにコミットしない
  - GitHub Copilot SDK依存は分離可能だが、設計上は Copilot SDK の session 構成を第一級要件として扱う
**Scale/Scope**: 
  - サポートプロバイダー数：4（OpenAI、Azure OpenAI、OpenAI互換、GitHub Copilot SDK）
  - 公開型：約10-15（インターフェース、オプション、ファクトリ等）
  - 想定ユーザー：.NETアプリケーション開発者

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Article 1: ビルド・コンパイル・静的チェック検証

- ✅ **合格**: .NET プロジェクトは `dotnet build` でコンパイル検証可能
- ✅ **合格**: Roslyn アナライザーで静的チェック実施
- 📋 **対応方針**: `net8.0` / `net10.0` の build + test + warnings-as-errors を GitHub Actions で継続実行する

### Article 2: 自動テスト同梱

- ✅ **合格**: 全実装タスクに対応するユニットテスト必須（NUnit + Moq）
- ✅ **合格**: 外部LLMプロバイダー依存は全てインターフェース抽象化してスタブ/モック化
- ✅ **合格**: 統合テストは実APIキー不要（モック使用）
- 📋 **対応方針**:
  - 各プロバイダー実装に対して契約テスト（IChatClient の振る舞い検証）
  - HTTP通信はモックHTTPハンドラーでスタブ化
  - GitHub Copilot SDK依存はインターフェース経由でモック注入可能に

### Article 3: 正常系優先・エラー明確化

- ✅ **合格**: 開発者向けライブラリなので可用性より正常系を優先
- ✅ **合格**: エラー時は詳細ログを出力し、明確な例外をスロー（リトライ・フォールバックなし）
- 📋 **対応方針**:
  - 認証失敗、ネットワークエラー、レート制限等は即座に例外スロー
  - 例外メッセージにプロバイダー名、エラー詳細、トレースIDを含める
  - ログは構造化ロギング（ILogger）で出力

### Article 4: 秘匿情報の非コミット

- ✅ **合格**: APIキー、エンドポイントURL等の秘匿情報をコミットしない
- ✅ **合格**: テストコードはモック/スタブ使用で実APIキー不要
- 📋 **対応方針**:
  - サンプルコードは環境変数または User Secrets からAPIキーを読み込む形式
  - README にダミーURL・キーの例示（実値は含まない）
  - `.gitignore` で `appsettings.Development.json` 等を除外

### Verifiable Specification

- ✅ **合格**: spec.md に User Scenarios & Testing セクションあり
- ✅ **合格**: 各 User Story に Independent Test と Acceptance Scenarios が定義済み
- 📋 **検証方針**:
  - User Story 1（設定ベース切り替え）: 4つのプロバイダーそれぞれで構成変更→チャット呼び出し成功を自動テストで検証
  - User Story 2（共通パラメータ）: 温度、最大トークン、ストリーミング等の共通パラメータが全プロバイダーで動作することを自動テストで検証
  - User Story 3（拡張パラメータ）: Azure OpenAI の data_sources 等の拡張パラメータが正しくリクエストに反映されることを HTTP モックで検証
  - User Story 4（Embedding）: OpenAI / Azure OpenAI の埋め込み生成と OpenAICompatible / GitHub Copilot の NotSupported を自動テストで検証

**Gate Decision**: ✅ **PASS** - Constitution に違反なし、Phase 0 へ進行可能

## Project Structure

### Documentation (this feature)

```text
specs/002-meai-multi-provider/
├── spec.md              # Feature specification (completed)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── chat-request.schema.json
│   ├── chat-response.schema.json
│   └── provider-config.schema.json
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── MeAiUtility.MultiProvider/                      # Core package（公開API＋抽象化）
│   ├── Abstractions/
│   │   ├── IChatClient.cs                          # MEAI準拠インターフェース（再エクスポート）
│   │   ├── IProviderFactory.cs                     # プロバイダー生成インターフェース
│   │   └── IProviderCapabilities.cs                # 機能サポート状況問い合わせ
│   ├── Options/
│   │   ├── MultiProviderOptions.cs                 # ルートオプション（Provider種別選択）
│   │   ├── CommonProviderOptions.cs                # 共通既定値・テレメトリ設定
│   │   ├── ConversationExecutionOptions.cs          # モデル/ReasoningEffort/ToolPolicy 等の共通セッション設定
│   │   ├── ProviderOverrideOptions.cs               # BYOK provider override 設定
│   │   └── ExtensionParameters.cs                  # ベンダー固有拡張パラメータ辞書
│   ├── Configuration/
│   │   ├── ProviderConfigurationExtensions.cs      # DI登録拡張メソッド
│   │   ├── ProviderRegistry.cs                     # プロバイダー登録管理
│   │   └── ProviderFactory.cs                      # IProviderFactory 実装
│   ├── Telemetry/
│   │   ├── ChatTelemetry.cs                        # テレメトリコンテキスト
│   │   └── LoggingExtensions.cs                    # 構造化ロギングヘルパー
│   └── MeAiUtility.MultiProvider.csproj
│
├── MeAiUtility.MultiProvider.OpenAI/               # OpenAI/OpenAI互換プロバイダー
│   ├── OpenAIChatClientAdapter.cs                  # 既存MEAI実装のラッパー
│   ├── OpenAICompatibleProvider.cs                 # OpenAI互換（ベースURL差し替え）
│   ├── Options/
│   │   └── OpenAIProviderOptions.cs                # OpenAI固有設定
│   └── MeAiUtility.MultiProvider.OpenAI.csproj
│
├── MeAiUtility.MultiProvider.AzureOpenAI/          # Azure OpenAI プロバイダー
│   ├── AzureOpenAIChatClientAdapter.cs             # 既存MEAI実装のラッパー
│   ├── Options/
│   │   ├── AzureOpenAIProviderOptions.cs           # AOAI固有設定
│   │   └── AzureAuthenticationOptions.cs           # APIキー/Entra ID認証
│   └── MeAiUtility.MultiProvider.AzureOpenAI.csproj
│
├── MeAiUtility.MultiProvider.GitHubCopilot/        # GitHub Copilot SDK プロバイダー
│   ├── Abstractions/
│   │   └── ICopilotSdkWrapper.cs                   # SDK 呼び出し抽象化
│   ├── GitHubCopilotChatClient.cs                  # Copilot SDK→IChatClient アダプタ
│   ├── CopilotClientHost.cs                        # SDK client / CLI server 接続管理
│   ├── Options/
│   │   └── GitHubCopilotProviderOptions.cs         # Copilot client / session 既定設定
│   └── MeAiUtility.MultiProvider.GitHubCopilot.csproj
│
└── MeAiUtility.MultiProvider.Samples/              # サンプルコンソールアプリ
    ├── BasicChatSample.cs                          # 最小チャット例
    ├── ProviderSwitchSample.cs                     # プロバイダー切り替え例
    ├── ExtensionParametersSample.cs                # 拡張パラメータ例
    └── MeAiUtility.MultiProvider.Samples.csproj

tests/
├── MeAiUtility.MultiProvider.Tests/                # コアパッケージのユニットテスト
│   ├── Configuration/
│   │   └── ProviderRegistryTests.cs
│   ├── Options/
│   │   └── ExtensionParametersTests.cs
│   └── Telemetry/
│       └── ChatTelemetryTests.cs
│
├── MeAiUtility.MultiProvider.OpenAI.Tests/         # OpenAIプロバイダーのテスト
│   ├── OpenAIChatClientAdapterTests.cs             # モックHTTPハンドラー使用
│   └── OpenAICompatibleProviderTests.cs
│
├── MeAiUtility.MultiProvider.AzureOpenAI.Tests/    # AOAIプロバイダーのテスト
│   ├── AzureOpenAIChatClientAdapterTests.cs
│   └── AzureAuthenticationTests.cs
│
├── MeAiUtility.MultiProvider.GitHubCopilot.Tests/  # Copilotプロバイダーのテスト
│   ├── GitHubCopilotChatClientTests.cs             # SDKモック使用
│   └── CopilotClientHostTests.cs
│
└── MeAiUtility.MultiProvider.IntegrationTests/     # 統合テスト（全プロバイダー）
    ├── ContractTests/                              # IChatClient契約テスト
    │   ├── ChatClientContractTests.cs              # 全プロバイダー共通振る舞い
    │   └── ProviderSpecificTests.cs                # プロバイダー固有動作
    └── ConfigurationTests/
        └── ProviderSwitchTests.cs                  # 構成変更による切り替え検証
```

**Structure Decision**: 

マルチパッケージ構成を採用する理由：
1. **依存関係の分離**: 各プロバイダーパッケージは独立してインストール可能（例：OpenAIのみ使用する場合、Copilot SDK依存を持ち込まない）
2. **バージョニングの独立性**: プロバイダー固有の破壊的変更（特にCopilot SDK Technical Preview）がコアパッケージに影響しない
3. **テストの分離**: 各プロバイダーのテストが独立して実行可能、CIでの並列実行が容易
4. **NuGetパッケージ配布**: コア + 必要なプロバイダーのみをユーザーがインストール可能

## Complexity Tracking

> Constitution Check で違反なし。複雑性の正当化は不要。

---

## Architecture & Design

### Layering Strategy

```text
┌─────────────────────────────────────────────────────┐
│  Application Layer                                  │
│  (ユーザーのアプリケーションコード)                    │
└────────────────┬────────────────────────────────────┘
                 │ 依存
                 ▼
┌─────────────────────────────────────────────────────┐
│  Public API Layer (MeAiUtility.MultiProvider)      │
│  - IChatClient (MEAI準拠インターフェース)            │
│  - DI拡張メソッド (AddMultiProviderChat)            │
│  - 共通オプション (CommonProviderOptions)            │
│  - 拡張パラメータ (ExtensionParameters)             │
└────────────────┬────────────────────────────────────┘
                 │ プロバイダー解決
                 ▼
┌─────────────────────────────────────────────────────┐
│  Provider Abstraction Layer                        │
│  - IProviderFactory (プロバイダー生成)              │
│  - IProviderCapabilities (機能サポート問い合わせ)   │
│  - ProviderRegistry (プロバイダー登録管理)          │
└────────────────┬────────────────────────────────────┘
                 │ 実装選択
                 ▼
┌─────────────────────────────────────────────────────┐
│  Provider Implementation Layer                      │
│  ┌────────────┬──────────────┬────────────────────┐ │
│  │ OpenAI/    │ Azure OpenAI │ GitHub Copilot SDK │ │
│  │ Compatible │              │                    │ │
│  └────────────┴──────────────┴────────────────────┘ │
│  各プロバイダーがIChatClientを実装                   │
└────────────────┬────────────────────────────────────┘
                 │ HTTP/SDK呼び出し
                 ▼
┌─────────────────────────────────────────────────────┐
│  External Services                                  │
│  - OpenAI API                                       │
│  - Azure OpenAI Service                             │
│  - OpenAI互換エンドポイント (Foundry Local等)       │
│  - GitHub Copilot SDK (.NET) / Copilot CLI server │
└─────────────────────────────────────────────────────┘
```

**レイヤリング原則**:
1. **依存の方向**: アプリケーション → 公開API → プロバイダー抽象 → プロバイダー実装 → 外部サービス
2. **抽象化の境界**: 公開APIはMEAI準拠のIChatClientのみを露出し、プロバイダー固有の型を漏らさない
3. **DI統合**: アプリケーションはDIコンテナからIChatClientを解決するだけで、プロバイダー実装を意識しない

### Component Responsibilities

#### Core Package (MeAiUtility.MultiProvider)

| 型名 | 責務 | 公開/内部 |
|------|------|----------|
| **IChatClient** | MEAI準拠のチャットクライアントインターフェース（再エクスポート） | 公開 |
| **IProviderFactory** | プロバイダー選択ロジックを隠蔽する抽象。DI から具象 `ProviderFactory` を解決するための境界 | 公開 |
| **IProviderCapabilities** | プロバイダーの機能サポート状況を問い合わせ（ストリーミング対応、拡張パラメータ対応等） | 公開 |
| **MultiProviderOptions** | プロバイダー選択のルート設定（Provider名、各プロバイダー固有オプション） | 公開 |
| **CommonProviderOptions** | 全プロバイダー共通の既定値・テレメトリ・ロギング設定 | 公開 |
| **ConversationExecutionOptions** | `ChatOptions.AdditionalProperties["meai.execution"]` から読み出す共通セッション設定 | 公開 |
| **ExtensionParameters** | ベンダー固有拡張パラメータを格納する型安全辞書（キー: "azure.data_sources"、"openai.top_logprobs"等） | 公開 |
| **ProviderConfigurationExtensions** | DI拡張メソッド群（AddMultiProviderChat、AddOpenAIProvider等） | 公開 |
| **ProviderRegistry** | 利用可能なプロバイダーの登録と解決を管理 | 内部 |
| **ProviderFactory** | `IProviderFactory` の具象実装。`ProviderRegistry` と構成済み DI から選択済み実装を生成 | 内部 |
| **ChatTelemetry** | リクエスト・レスポンスのテレメトリコンテキスト（トレースID、タイムスタンプ等） | 公開 |
| **LoggingExtensions** | 構造化ロギングヘルパー（機密情報マスキング、トレース相関） | 内部 |

#### Provider Packages

| パッケージ名 | 主要型 | 責務 |
|-------------|--------|------|
| **MeAiUtility.MultiProvider.OpenAI** | OpenAIChatClientAdapter | 既存のMicrosoft.Extensions.AI.OpenAI実装をラッパーし、共通オプション・拡張パラメータを変換 |
|  | OpenAICompatibleProvider | OpenAI互換エンドポイント用にベースURLを差し替え、モデル名マッピング等の互換性調整 |
|  | OpenAIProviderOptions | APIキー、組織ID、ベースURL、タイムアウト等のOpenAI固有設定 |
| **MeAiUtility.MultiProvider.AzureOpenAI** | AzureOpenAIChatClientAdapter | 既存のAzure OpenAI MEAI実装をラッパー |
|  | AzureAuthenticationOptions | APIキー認証 or Entra ID（DefaultAzureCredential）の選択と設定 |
|  | AzureOpenAIProviderOptions | エンドポイントURL、デプロイメント名、APIバージョン等のAOAI固有設定 |
| **MeAiUtility.MultiProvider.GitHubCopilot** | GitHubCopilotChatClient | GitHub Copilot SDK を呼び出して IChatClient 契約 + 共通セッションオプションに適合させるアダプタ |
|  | ICopilotSdkWrapper | SDK 呼び出し面の抽象化。テストではモック注入し、Host と Adapter の境界を固定する |
|  | CopilotClientHost | Copilot SDK client と CLI server の接続/ライフサイクルを管理 |
|  | GitHubCopilotProviderOptions | Copilot client 初期化、認証、モデル既定値、reasoning effort 等の設定 |

### Configuration Schema

```json
{
  "MultiProvider": {
    "Provider": "OpenAI",  // "OpenAI" | "AzureOpenAI" | "OpenAICompatible" | "GitHubCopilot"
    "OpenAI": {
      "ApiKey": "${OPENAI_API_KEY}",  // 環境変数参照推奨
      "OrganizationId": null,
      "BaseUrl": null,  // null = デフォルト (api.openai.com)
      "ModelName": "gpt-4",
      "TimeoutSeconds": 60
    },
    "AzureOpenAI": {
      "Endpoint": "${AZURE_OPENAI_ENDPOINT}",
      "DeploymentName": "gpt-4-deployment",
      "ApiVersion": "2024-02-15-preview",
      "Authentication": {
        "Type": "ApiKey",  // "ApiKey" | "EntraId"
        "ApiKey": "${AZURE_OPENAI_API_KEY}"
      },
      "TimeoutSeconds": 60
    },
    "OpenAICompatible": {
      "BaseUrl": "http://localhost:8080",  // Foundry Local等
      "ApiKey": "dummy-key",
      "ModelName": "local-model",
      "TimeoutSeconds": 60
    },
    "GitHubCopilot": {
      "CliPath": null,
      "GitHubToken": "${GITHUB_TOKEN}",
      "UseLoggedInUser": false,
      "ModelId": "gpt-5",
      "ReasoningEffort": "high",
      "AvailableTools": ["view", "rg"],
      "TimeoutSeconds": 120
    },
    "Common": {
      "DefaultTemperature": 0.7,
      "DefaultMaxTokens": 1000,
      "EnableTelemetry": true,
      "LogRequestResponse": false  // 本番ではfalse推奨（PII漏洩防止）
    }
  }
}
```

**設計方針**:
- **環境変数参照**: 機密情報（APIキー）は `${VAR_NAME}` 形式で環境変数から読み込み
- **プロバイダー固有セクション**: 各プロバイダーの設定は独立したセクションに分離
- **共通設定**: Common セクションで全プロバイダーのデフォルト値を指定
- **null = デフォルト**: 省略可能な設定はnullでデフォルト動作

### Request/Response Normalization

#### メッセージロールの正規化

| MEAI標準 | OpenAI | Azure OpenAI | GitHub Copilot SDK |
|----------|--------|--------------|-------------------|
| System   | system | system       | system            |
| User     | user   | user         | user              |
| Assistant| assistant | assistant | assistant         |
| Tool     | tool | tool | 初期リリースでは明示再送非対応 |

**方針**:
- `system` / `user` / `assistant` は MEAI 標準のロール名をそのまま正規化対象とする
- `tool` ロールの履歴再送は OpenAI / Azure OpenAI / OpenAI互換では pass-through 対象とする
- GitHub Copilot では `AvailableTools` / `ExcludedTools` を first-class に扱い、`ChatRole.Tool` の履歴再送は初期リリース対象外とする。入力された場合は `NotSupportedException` で fail-fast する

#### ストリーミングレスポンスの正規化

全プロバイダーでMEAIの `IAsyncEnumerable<ChatMessageChunk>` 形式に統一。

- **OpenAI/Azure OpenAI**: Server-Sent Events (SSE) → ChatMessageChunk
- **GitHub Copilot SDK**: SDK のストリーミング機能 → ChatMessageChunk
- **OpenAI互換**: SSE互換形式を期待し、差異が解消できない場合は明確なエラーとして失敗させる

#### キャンセルの正規化

全プロバイダーで `CancellationToken` を受け取り、キャンセル時は `OperationCanceledException` をスロー。

- **HTTP系プロバイダー**: HttpClient の CancellationToken 伝播
- **Copilot SDK**: SDKプロセスに中断シグナル送信

#### エラー（例外）の正規化

| エラー種別 | 例外型 | HTTP Status | 含む情報 |
|-----------|--------|-------------|---------|
| 認証失敗 | `AuthenticationException` | 401/403 | プロバイダー名、ステータスコード、エラーメッセージ、レスポンス本文、TraceId |
| レート制限 | `RateLimitException` | 429 | Retry-After ヘッダー、ステータスコード、レスポンス本文、TraceId |
| 無効リクエスト | `InvalidRequestException` | 400 | 検証エラー詳細、プロバイダー名、ステータスコード、レスポンス本文、TraceId |
| サーバーエラー | `ProviderException` | 500-599 | ステータスコード、レスポンス本文、プロバイダー名、TraceId |
| タイムアウト | `TimeoutException` | N/A | タイムアウト時間、プロバイダー名、TraceId |
| 未対応機能 | `NotSupportedException` | N/A | 機能名、プロバイダー名、TraceId |
| Copilot起動失敗 | `CopilotRuntimeException` | N/A | CLIパス、プロセス終了コード、イベント情報、TraceId |

**例外設計方針**:
- 全例外は `MultiProviderException` 基底クラスから派生
- `ProviderName`、`TraceId`、`Timestamp`、`StatusCode`、`ResponseBody` プロパティを必須で含む
- 内部例外（InnerException）で元のHTTP例外やSDK例外を保持
- ログ出力は例外スロー時に自動実行（呼び出し側での再ログ不要）

#### 使用量・メタデータの正規化

| メタデータ | OpenAI | Azure OpenAI | GitHub Copilot SDK |
|-----------|--------|--------------|-------------------|
| プロンプトトークン数 | ✅ | ✅ | ❓（SDKによる） |
| 完了トークン数 | ✅ | ✅ | ❓ |
| 総トークン数 | ✅ | ✅ | ❓ |
| 終了理由 | ✅ | ✅ | ✅ |
| モデル名 | ✅ | ✅ | ✅ |
| レスポンスID | ✅ | ✅ | ❓ |

**方針**: 取得可能なメタデータのみを `ChatResponse.Metadata` 辞書に格納。取得不可能な項目はnullまたは辞書に含めない（プロバイダー差を許容）。

### Extension Parameters Design

#### 命名規約とキー設計

拡張パラメータは「プロバイダー名.パラメータ名」形式でキーを定義し、衝突を防ぐ。

```csharp
var extensionParams = new ExtensionParameters
{
    ["azure.data_sources"] = new[] 
    {
        new { type = "azure_search", parameters = new { endpoint = "...", key = "..." } }
    },
    ["openai.top_logprobs"] = 5,
    ["openai.response_format"] = new { type = "json_object" }
};
```

**型安全ラッパー（オプション）**:

将来的に以下のような型安全ヘルパーを提供可能：

```csharp
extensionParams.SetAzureDataSources(dataSources);  // 型安全メソッド
extensionParams.SetOpenAITopLogProbs(5);
```

**処理方針**:
- 各プロバイダー実装は自分のプレフィックス（"azure."、"openai."、"copilot."等）のパラメータのみを解釈
- 選択中プロバイダーで解釈できない拡張パラメータは送信前に検証エラーとする
- パラメータ値の型が期待と異なる場合は例外を返し、処理を継続しない

### Feature Matrix（機能サポート状況）

| 機能 | OpenAI | Azure OpenAI | OpenAI互換 | GitHub Copilot SDK | 備考 |
|------|--------|--------------|-----------|-------------------|------|
| 基本チャット | ✅ | ✅ | ✅ | ✅ | 全プロバイダー対応 |
| ストリーミング | ✅ | ✅ | ✅ | ✅ | |
| モデル選択 | ✅ | ⚠️ | ✅ | ✅ | Azure は deployment との対応付けが必要 |
| Reasoning effort | ⚠️ | ⚠️ | ⚠️ | ✅ | モデル依存。Copilot は model capability で事前検証 |
| システムメッセージ制御 | ✅ | ✅ | ✅ | ✅ | Append/Replace は provider 差異あり |
| Tool allow/deny | ⚠️ | ⚠️ | ⚠️ | ✅ | Copilot は SessionConfig で直接指定可能 |
| キャンセル | ✅ | ✅ | ✅ | ✅ | |
| 拡張パラメータ | ✅ | ✅ | ⚠️ | ✅ | Copilot は advanced options / provider override を含む |
| モデル能力取得 | ⚠️ | ⚠️ | ❌ | ✅ | Copilot は `ListModelsAsync()` を利用 |
| Provider override (BYOK) | ❌ | ❌ | ❌ | ✅ | Copilot SDK 固有 |
| 永続セッション | ❌ | ❌ | ❌ | ✅ | Copilot infinite sessions |
| 埋め込み生成 | ✅ (P3) | ✅ (P3) | ❌ | ❌ | OpenAI / Azure OpenAI のみ対象、OpenAICompatible / Copilot は fail-fast |

**凡例**: ✅ = 完全対応、⚠️ = 部分対応（制限あり）、❌ = 非対応

**未対応機能の動作**:
- `NotSupportedException` をスローし、ログに「プロバイダーXは機能Yをサポートしていません」を記録
- ランタイム問い合わせ: `IProviderCapabilities.IsSupported(FeatureName)` で事前チェック可能

### Provider-Specific Design

#### OpenAI API / OpenAI互換

**既存実装の再利用**:
- `Microsoft.Extensions.AI.OpenAI` パッケージが存在する場合、その `OpenAIChatClient` を内部で使用
- アダプタは `CommonProviderOptions` / `ChatOptions` / `ConversationExecutionOptions` を OpenAI固有オプションへ正規化する

**OpenAI互換エンドポイント対応**:
- `OpenAICompatibleProvider` を dedicated adapter として用意しつつ、実際の差分吸収は `BaseUrl` 差し替えを中心に行う
- 互換保証範囲は **`/v1/chat/completions` 系のチャット実行、ストリーミング、モデル名マッピング、stop sequences、基本生成パラメータ、HTTP エラーレスポンス保持** に限定する
- 互換差異（例：モデル名形式、一部パラメータ解釈、structured output/tool replay 差異）は以下で吸収または fail-fast する：
  - モデル名マッピング設定（"gpt-4" → "local-model"等）
  - 未対応パラメータや互換崩れは送信前または受信直後に検証エラー/パース例外とする
- **動作確認済みエンドポイント**をドキュメントで明示（例：Foundry Local、Ollama、LM Studio）

#### Azure Open AI

**認証方式**:
- **APIキー認証**: `AzureAuthenticationOptions.Type = ApiKey`、`ApiKey` 設定
- **Entra ID認証**: `Type = EntraId`、`DefaultAzureCredential` 使用（環境変数、マネージドID、Azure CLI等）

**デプロイメント名の扱い**:
- AOAI はモデル名ではなくデプロイメント名を指定
- `AzureOpenAIProviderOptions.DeploymentName` で明示的に指定
- モデル名（"gpt-4"）とデプロイメント名の対応は設定で管理

**拡張機能**:
- `azure.data_sources` パラメータでRAG機能（Azure AI Search統合）をサポート
- 拡張パラメータ経由で指定、AOAIのREST API形式に変換

#### GitHub Copilot SDK

**設計方針**:
- GitHub Copilot SDK を**第一級のバックエンド**として扱い、共通I/Fは SDK の主要 session 設定を情報欠落なく表現する
- 共通化する対象は `model`, `reasoning effort`, `system message`, `tool allow/deny`, `streaming`, `working directory`, `auth selection`, `BYOK provider override`
- 他プロバイダーに自然写像できない要素（hooks, ask_user, MCP servers, custom agents 等）は、provider-specific advanced options として明示的に保持する

**SDK/CLIランタイム依存**:
- GitHub Copilot SDK は Copilot CLI server と JSON-RPC で通信する
- SDK が CLI 起動/接続を管理するため、ライブラリ側は独自のプロセスプールではなく **SDK client host の構成と破棄**に責務を限定する
- `ICopilotSdkWrapper` は SDK 呼び出し抽象化、`CopilotClientHost` は wrapper を利用した接続/ライフサイクル管理に責務を分離する
- `CliPath` だけでなく `CliArgs`, `CliUrl`, `UseStdio`, `LogLevel`, `AutoStart`, `AutoRestart` を設定対象とする

**認証**:
- `GitHubToken`、`UseLoggedInUser`、環境変数トークン、BYOK provider override を選択可能にする
- `GitHubToken` が明示指定された場合は SDK client 作成時に `githubToken` を渡し、`useLoggedInUser=false` を強制してトークン認証経路を固定する
- 明示トークンがない場合でもログイン済みユーザー認証を利用できるため、トークン必須前提にしない
- 認証不成立時は即失敗し、ログへ認証経路と例外情報 (`Exception.ToString()`) を出力する

**モデルと reasoning effort**:
- モデル一覧/能力情報を取得し、選択モデルが reasoning effort をサポートするかを実行前に確認する
- `low / medium / high / xhigh` を共通列挙に正規化する
- 未対応モデルに対する reasoning effort 指定は `NotSupportedException` とし、送信前に失敗させる

**Copilot 固有の追加機能**:
- `ProviderOverride` で BYOK を扱う
- `InfiniteSessions` で context compaction と workspace 永続化を扱う
- `AvailableTools` / `ExcludedTools` を first-class にし、Copilot の主用途である tool policy 指定を失わない

**失敗時の取り回し**:
- CLI 起動/接続失敗: `CopilotRuntimeException` をスローし、CLI設定・接続先・例外詳細を記録
- モデル能力不整合: `NotSupportedException` または設定検証例外をスロー
- SDK 由来のエラーはベストエフォートで続行せず、レスポンス本文/イベント情報があればログへ残して失敗させる

### Cross-Cutting Concerns

#### Logging & Telemetry

**ロギング方針**:
- `Microsoft.Extensions.Logging` の `ILogger` を使用
- 構造化ロギング（Key-Value形式）でトレース相関を実現
- ログレベル:
  - **Information**: リクエスト開始・終了、プロバイダー切り替え
  - **Warning**: 追加観測に有用だが失敗条件ではない運用上の注意
  - **Error**: 例外発生、プロバイダー呼び出し失敗
  - **Debug**: リクエスト・レスポンス詳細（本番では無効化推奨）

**機密情報のマスキング**:
- APIキー、認証トークン、エンドポイントURL、ユーザー入力（PII）はログに出力しない
- `LoggingExtensions` でマスキング処理を自動化
- 例: `ApiKey = "***MASKED***"`、`Endpoint = "***MASKED***"`

**OpenTelemetry統合（オプション）**:
- `EnableTelemetry = true` の場合、OpenTelemetry のアクティビティ（分散トレース）を生成
- `ChatTelemetry` でスパン作成、プロバイダー名・モデル名・トークン使用量をタグとして記録
- アプリケーション側でOpenTelemetry Exporterを設定すれば自動的に送信

#### Testing Strategy

**契約テスト（ContractTests）**:
- 全プロバイダーが `IChatClient` の契約を満たすことを検証
- 共通テストスイートを各プロバイダーに適用
- モックHTTPハンドラー/SDKモックを使用し、実APIキー不要

**ユニットテスト**:
- 各コンポーネント（オプション変換、パラメータ正規化、エラーハンドリング等）を個別にテスト
- Moq でインターフェースをモック化

**統合テスト**:
- プロバイダー切り替えが構成変更のみで動作することを検証
- 全て実APIキー不要のモックで実行（CI環境で安全に実行可能）

**実プロバイダーテスト（手動）**:
- 開発者ローカル環境で実APIキーを使用してE2Eテスト
- ドキュメントに手順を記載、CIでは実行しない

#### Sample Applications

**BasicChatSample**:
- 最小構成でチャット実行を示す（10行以内）
- DI登録、IChatClient取得、チャット呼び出し

**ProviderSwitchSample**:
- 構成ファイル変更でプロバイダー切り替えを実演
- OpenAI → Azure OpenAI → OpenAICompatible → Copilot の4パターンを同じコードで実行

**ExtensionParametersSample**:
- Azure OpenAI の RAG機能（data_sources）を拡張パラメータで指定する例

**サンプル配置**:
- `MeAiUtility.MultiProvider.Samples` プロジェクトにまとめる
- README に各サンプルの目的と実行手順を記載

#### Versioning & Compatibility

**セマンティックバージョニング**:
- コアパッケージ: `1.0.0` から開始
- プロバイダーパッケージ: コアと独立してバージョン管理
- 破壊的変更（Public API変更）時はメジャーバージョンアップ
- 共有 version metadata は `Directory.Build.props` に集約し、README のリリース手順節でも破壊的変更時の判定基準を明示する
- メジャー/マイナー/パッチ判定は **NuGet パッケージ単位**で行い、コア公開 API が不変なら provider package の major 更新は独立して実施できる

**プレビュー依存の隔離**:
- GitHub Copilot SDK (Technical Preview) はオプショナル依存
- `MeAiUtility.MultiProvider.GitHubCopilot` パッケージを別配布
- コアパッケージはCopilot依存を持たない（NuGet依存なし）

**後方互換性**:
- 構成スキーマの拡張は可能（新規キー追加）、既存キーの削除・型変更は破壊的変更
- オプションクラスに新規プロパティ追加は非破壊的（デフォルト値で互換性維持）

**破壊的変更の抑制策**:
- Public API は最小限に（内部実装詳細は internal）
- プロバイダー実装は `IProviderFactory` 経由で隠蔽（直接型参照不要）
- 拡張パラメータは辞書形式で柔軟性確保（新規パラメータ追加は非破壊的）

### Failure Modes & Mitigation

| 失敗モード | 原因 | 検出方法 | 対策 |
|-----------|------|---------|------|
| **認証失敗** | APIキー無効、トークン期限切れ | HTTP 401/403 | `AuthenticationException` スロー、ログに「認証失敗」明記、設定確認を促すメッセージ |
| **レート制限** | プロバイダーのクォータ超過 | HTTP 429、Retry-Afterヘッダー | `RateLimitException` スロー、Retry-After情報をログ記録、呼び出し側で再試行判断 |
| **ネットワークタイムアウト** | プロバイダー応答遅延 | HttpClient タイムアウト | `TimeoutException` スロー、タイムアウト時間をログ記録、設定で調整可能 |
| **互換エンドポイント差異** | OpenAI互換の実装差 | レスポンスパースエラー | エラー詳細をログ出力し、`ProviderException` として失敗 |
| **Copilot CLI 起動失敗** | CLIパス不正、未インストール | プロセス起動エラー | `CopilotRuntimeException` スロー、CLIパス・PATH設定確認を促す |
| **Copilot プロセス異常終了** | CLI内部エラー | 終了コード非0 | ログに標準エラー出力を記録し、`CopilotRuntimeException` をスロー |
| **未対応機能呼び出し** | プロバイダーが機能非対応 | `IProviderCapabilities` チェック | `NotSupportedException` スロー、「プロバイダーXは機能Yをサポートしていません」をログ記録 |
| **設定不備** | 必須設定項目の欠落 | DI登録時の検証 | 起動時に `InvalidOperationException` スロー、不足項目を明記 |
| **拡張パラメータ型不一致** | 期待する型と異なる値 | 実行時の型チェック | 例外をスローし、型情報と失敗理由をログ記録 |
| **ストリーミング接続断** | ネットワーク切断 | ストリーム読み取りエラー | 受信済みメッセージを保持、`IOException` を `ProviderException` でラップしてスロー |

**共通対策方針**:
- **エラー即失敗**: リトライ・フォールバックは行わず、明確なエラーで失敗
- **詳細ログ**: 失敗原因特定に必要な情報（プロバイダー名、設定値、HTTPステータス等）を全てログ記録
- **トレース相関**: 全ログに `TraceId` を含め、分散トレースで追跡可能に
- **機密情報保護**: エラーログにもAPIキーやトークンを含めない（マスキング）

---

## Phase 0: Research & Decisions

[Phase 0 output will generate `research.md` based on NEEDS CLARIFICATION items and technology choices]

**Research Tasks to be performed**:
1. Microsoft.Extensions.AI の現在の実装状況調査（OpenAI/Azure OpenAI パッケージの有無と機能範囲）
2. GitHub Copilot SDK の最新ドキュメント調査（認証方式、CLI起動方法、レスポンス形式）
3. 代表的なOpenAI互換実装（Foundry Local、Ollama）の互換性レベル調査
4. OpenTelemetry .NET の統合パターン調査（アクティビティ生成、タグ設計）
5. .NET DI拡張メソッドのベストプラクティス調査（オプションパターン、検証）

---

## Phase 1: Data Model & Contracts

[Phase 1 output will generate `data-model.md`, `contracts/`, and `quickstart.md`]

**Planned Artifacts**:
- `data-model.md`: 主要エンティティ（MultiProviderOptions、ExtensionParameters、ChatTelemetry等）の詳細定義
- `contracts/chat-request.schema.json`: チャットリクエスト構造のJSON Schema
- `contracts/chat-response.schema.json`: チャットレスポンス構造のJSON Schema
- `contracts/provider-config.schema.json`: プロバイダー設定のJSON Schema
- `quickstart.md`: 5分で動作させる最小構成ガイド

---

## Phase 2: Task Breakdown

[Phase 2 is out of scope for `/speckit.plan` command. Generated by `/speckit.tasks` command.]

**Note**: 実装タスク（Tasks.md）はこのドキュメントには含まれません。`/speckit.tasks` コマンドで別途生成されます。

---

## Plan Detailed Addendum (2026-03-03)

### 1. 粒度宣言（Granularity）

- 対象粒度: `Container` + `Component`（DI境界、プロバイダー切替境界、外部API境界）
- 非対象粒度: `Class` / `Method` / 具体APIシグネチャ
- 目的: 設計レビューで「どの境界を固定し、何を後段検証で確定するか」を明確化する

### 2. C4語彙テーブル（Vocabulary）

| ID | 種別 | 正式名 | 役割（1行） | 住所（実装の場所） | 主要IF/依存 | Alias |
|---|---|---|---|---|---|---|
| Application | Container | Consumer Application | IChatClientを利用する呼び出し側 | samples/ または利用側アプリ | DI / IChatClient | App |
| C_Config | Component | Configuration Binder | 構成値を読み込み、選択プロバイダーを取得 | src/MeAiUtility.MultiProvider/Configuration | IConfiguration | Config |
| C_Registry | Component | Provider Registry | プロバイダー選択と登録管理 | src/MeAiUtility.MultiProvider/Configuration | Provider key | Registry |
| C_Factory | Component | Provider Factory | 選択済みプロバイダー実装を生成 | src/MeAiUtility.MultiProvider/Configuration | DI container | Factory |
| P_OpenAI | Component | OpenAI Adapter | OpenAI向けチャット実行 | src/MeAiUtility.MultiProvider.OpenAI | HTTP | OpenAI |
| P_AzureOpenAI | Component | Azure OpenAI Adapter | Azure OpenAI向けチャット実行 | src/MeAiUtility.MultiProvider.AzureOpenAI | HTTP | AOAI |
| P_OpenAICompatible | Component | OpenAI Compatible Adapter | ベースURL差し替え型の互換呼び出し | src/MeAiUtility.MultiProvider.OpenAI | HTTP | Compatible |
| P_GitHubCopilot | Component | GitHub Copilot Adapter | Copilot SDK/Runtimeへのチャット委譲 | src/MeAiUtility.MultiProvider.GitHubCopilot | SDK / Runtime | Copilot |
| P_OpenAIEmbedding | Component | OpenAI Embedding Adapter | OpenAI向け Embedding 生成 | src/MeAiUtility.MultiProvider.OpenAI | HTTP | OpenAIEmbedding |
| P_AzureOpenAIEmbedding | Component | Azure OpenAI Embedding Adapter | Azure OpenAI向け Embedding 生成 | src/MeAiUtility.MultiProvider.AzureOpenAI | HTTP | AOAIEmbedding |
| X_ModelAPI | External | Provider Model API | 外部LLM APIエンドポイント | external | HTTPS | - |
| T_Logger | Component | Trace Logger | 成功/失敗の観測情報を出力 | src/MeAiUtility.MultiProvider/Telemetry | ILogger | Logger |

### 3. Scenario Ledger

| Scenario ID | 目的/価値（1行） | Given | When | Then | 参加者（Vocabulary ID） | 入出力/メッセージ | 例外・タイムアウト・リトライ | 観測点（ログ/メトリクス） | 参照 |
|---|---|---|---|---|---|---|---|---|---|
| S-010 | 構成のみでプロバイダーを切替可能にする | 構成にProviderが設定済み | DI解決が走る | 対象実装が返る | Application, C_Config, C_Registry, C_Factory, T_Logger | provider key -> IChatClient | 不正設定は即例外、フォールバックしない | provider_resolved / provider_validation_error | spec FR-003, FR-004 |
| S-020 | 共通パラメータでOpenAI実行する | Provider=OpenAI | ChatAsync実行 | 応答が正規化される | Application, P_OpenAI, X_ModelAPI, T_Logger | chat request/response | 401/403/429を例外化 | provider_chat_success / provider_chat_error | spec FR-002, FR-005 |
| S-030 | Azure拡張パラメータを適用する | Provider=AzureOpenAI | 拡張付き実行 | data_sources等が反映 | Application, P_AzureOpenAI, X_ModelAPI, T_Logger | common + extension parameters | 型不一致・不正要求は送信前または応答時に例外化 | extension_applied / extension_validation_error | spec FR-006 |
| S-040 | OpenAI互換をBaseUrl変更のみで使う | Provider=OpenAICompatible | ChatAsync実行 | 互換エンドポイントで応答取得 | Application, P_OpenAICompatible, X_ModelAPI, T_Logger | baseUrl + chat payload | 互換崩れはパース例外化 | compatible_request_sent / compatible_parse_error | spec FR-007 |
| S-050 | GitHub Copilotを共通I/Fで呼び出す | Provider=GitHubCopilot | ChatAsync実行 | 共通I/Fで応答取得 | Application, P_GitHubCopilot, X_ModelAPI, T_Logger | runtime request/response | 起動失敗・未対応機能は例外 | copilot_runtime_start / copilot_runtime_error | spec FR-002, FR-008 |
| S-060 | ストリーミング/キャンセルを統一する | streaming=true | StreamAsync中に受信/取消 | チャンク配信または取消例外 | Application, P_OpenAI, P_AzureOpenAI, P_GitHubCopilot, X_ModelAPI, T_Logger | async stream chunks | 取消はOperationCanceledException | stream_chunk_count / stream_canceled | spec FR-011, FR-012 |
| S-070 | エラーレスポンスを記録して例外通知する | 外部APIが4xx/5xx/timeout | ChatAsync実行 | ステータス/本文/traceで追跡可能 | Application, P_OpenAI, P_AzureOpenAI, P_OpenAICompatible, P_GitHubCopilot, T_Logger | error response body | timeout・4xx・5xxを種別例外化 | provider_http_error / provider_timeout | spec FR-013, FR-014, FR-016 |
| S-080 | Embedding を共通I/Fで生成する | Provider=OpenAI / AzureOpenAI / OpenAICompatible / GitHubCopilot | GenerateEmbeddingAsync 実行 | OpenAI/Azure は埋め込みベクトルを返し、OpenAICompatible/Copilot は即例外 | Application, P_OpenAIEmbedding, P_AzureOpenAIEmbedding, P_OpenAICompatible, P_GitHubCopilot, X_ModelAPI, T_Logger | embedding request/response | OpenAICompatible/Copilot は NotSupportedException、4xx/5xx は種別例外化 | embedding_generated / embedding_not_supported / embedding_http_error | spec User Story 4, spec FR-002 |

### 4. コード対応（Mapping）

| Vocabulary ID | 実装の住所（具体） | 主なエントリポイント | テスト観点（最小） |
|---|---|---|---|
| C_Config | src/MeAiUtility.MultiProvider/Configuration | DI登録拡張、構成バインド | 必須設定欠落時の例外化 |
| C_Registry | src/MeAiUtility.MultiProvider/Configuration | Provider登録と解決 | Provider切替の分岐網羅 |
| C_Factory | src/MeAiUtility.MultiProvider/Configuration | IChatClient生成 | 未知Provider拒否 |
| P_OpenAI | src/MeAiUtility.MultiProvider.OpenAI | Chat実行アダプタ | 共通パラメータ変換 |
| P_AzureOpenAI | src/MeAiUtility.MultiProvider.AzureOpenAI | Chat実行アダプタ | 拡張パラメータ変換 |
| P_OpenAICompatible | src/MeAiUtility.MultiProvider.OpenAI | BaseUrl差し替え | 互換応答のパース |
| P_GitHubCopilot | src/MeAiUtility.MultiProvider.GitHubCopilot | Runtime連携アダプタ | Runtime失敗時の例外 |
| P_OpenAIEmbedding | src/MeAiUtility.MultiProvider.OpenAI | Embedding生成アダプタ | 埋め込みベクトル正規化 |
| P_AzureOpenAIEmbedding | src/MeAiUtility.MultiProvider.AzureOpenAI | Embedding生成アダプタ | Embedding 応答変換 |
| T_Logger | src/MeAiUtility.MultiProvider/Telemetry | ログ拡張 | 例外時にException.ToStringを記録 |

### 5. 検証ゲート（Plan段階の具体化）

- 依存関係確認: Tasksで `restore` 実行タスクを必須化し、存在確認を後段で確定する
- 型/API実在確認: Tasksで最小コンパイル（spike含む）を必須化し、Plan上の設計仮定を検証する
- 失敗時方針: 原則フォールバックなしでエラー終了とし、例外は必ず `Exception.ToString()` をトレースに出力する
- ログ方針: HTTP失敗時はステータス/例外だけでなくエラーレスポンス内容も出力対象にする

### 6. 網羅性チェック（Checklist）

| Check ID | 判定 | 根拠 |
|---|---|---|
| A1: Plan内の箱はVocabularyで定義済みか | Yes | Vocabulary表 + S-010〜S-080 |
| A2: 各箱に役割と住所があるか | Yes | Vocabulary表の「役割」「住所」列 |
| B1: 主要要件にScenario IDが紐づくか | Yes | Scenario Ledger「参照」列 |
| B2: 例外/タイムアウト方針があるか | Yes | S-060, S-070 |
| B3: 運用/設定変更シナリオがあるか | Yes | S-010 |
| C1: シナリオ参加者の住所を追跡可能か | Yes | Mapping表 |
| C2: Scenario→Vocabulary→住所で追跡可能か | Yes | S-010〜S-080 + Mapping表 |

### 7. Resolved Decisions

- **GitHub Copilot runtime の配布形態差異**: 自動テスト対象は SDK ラッパーと host 抽象化までに限定し、CLI 配布形態やローカル開発環境差異は quickstart に基づく手動確認対象とする。CI はスタブ/モックのみを使用し、実 OS や実 CLI インストールへ依存しない。
- **OpenAI互換の保証範囲**: 互換保証は chat completions / streaming / model mapping / stop sequences / 基本生成パラメータ / HTTP エラーペイロード保持までとする。structured output や tool replay など互換差が大きい領域は初期リリース対象外とし、検出時は fail-fast する。
- **Embedding（P3）の Vocabulary 拡張**: 既存 chat コンポーネント ID を流用せず、`P_OpenAIEmbedding` / `P_AzureOpenAIEmbedding` / `S-080` を採用して追跡性を維持する。

### 8. 関連ADR一覧

- ADR-001: マルチパッケージ構成（コア/プロバイダー分離）
- ADR-002: プロバイダー切替を構成駆動で行う方式
- ADR-003: 失敗時フォールバック非採用と例外中心のエラー方針
- ADR-004: ログ方針（機密情報非出力、エラーレスポンス記録、TraceId相関）

````

# GitHub Copilot プロバイダー改善・拡張 Plan

**Date**: 2026-04-06  
**Input**: `meai_utility_impl_review.md`（利用者レビュー）、`meai-utility-impl-skill-attachment-extension-request.md`（拡張要求）  
**SDK Reference**: https://github.com/github/copilot-sdk (v0.2.1, Public Preview)

---

## Background / Goal

`meai_utility_impl` ライブラリの GitHub Copilot プロバイダーに対して、実利用者（DLSiteNormalizer 導入事例）からの利便性改善要望と、別利用者（tool_flaui_run_and_generate）からの機能拡張要望を統合的に取り込む。

具体的には以下を達成する：

1. **DI 登録の簡素化**: `AddGitHubCopilotProvider` + `AddGitHubCopilotSdkWrapper` の 2 段階登録を解消し、1 API で実運用構成が整うようにする
2. **File Attachment の公開 API 化**: GitHub Copilot SDK のファイル添付機能を、SDK 直接参照不要で利用側から指定可能にする
3. **SkillDirectories / DisabledSkills の型安全 API 化**: 既に `AdvancedOptions` 経由で動作する機能を、`ConversationExecutionOptions` の first-class プロパティへ昇格する
4. **例外の Phase 情報追加**: ListModels 失敗と Send 失敗を構造的に区別可能にする
5. **Request 単位 Timeout Override**: `ConversationExecutionOptions` でリクエスト単位のタイムアウト指定を可能にする
6. **CLI 解決体験の改善**: Copilot CLI の探索は SDK に委ねつつ、失敗時診断と利用ガイドをライブラリ側で強化して利用側の切り分け負担を軽減する
7. **README / ドキュメントの整備**: 上記変更と既存の利用上の困りどころを反映した文書を整備する

## Non-goals

- GitHub Copilot SDK の session resume 対応（利用側が毎回新規セッション作成する運用のため）
- blob attachment / directory attachment / image 最適化
- slash command / custom agent の公開 API 化
- NuGet パッケージとしての公開（本 Plan では準備方針のみ記載し、実公開は別 issue）
- OpenAI / AzureOpenAI / OpenAICompatible プロバイダーへの機能追加
- ただし、`ConversationExecutionOptions` に新設する GitHub Copilot 専用オプション（`Attachments`, `SkillDirectories`, `DisabledSkills`）を受け取った際に明示的に `NotSupportedException` を返すガード追加は本 Plan の対象に含む
- GitHub Copilot SDK 本体への upstream 変更提案
- `tool_flaui_run_and_generate` 固有の JSON schema 解釈

## Current state summary

### DI 登録

- `AddGitHubCopilotProvider(configuration)` は内部で `DefaultCopilotSdkWrapper`（スタブ）を登録する
- スタブは `Task.FromResult($"Copilot response ({config.ModelId ?? "gpt-5"})")` を返すため、設定漏れが「それっぽく動く」事故を起こす
- 実利用には別途 `AddGitHubCopilotSdkWrapper()` の呼び出しが必要
- この 2 段階パターンは README に記載があるが、API 設計として見落としやすい

### File Attachment

- `ICopilotSdkWrapper.SendAsync` は `string prompt` + `CopilotSessionConfig config` のみ受け取る
- SDK の `MessageOptions.Attachments` に相当する公開 API が存在しない
- GitHub Copilot SDK は `UserMessageDataAttachmentsItemFile` (Path + DisplayName) をサポート済み

### SkillDirectories

- `CopilotSessionConfig.AdvancedOptions` → `copilot.skillDirectories` キーで SDK の `SessionConfig.SkillDirectories` へ流し込み済み
- `ConversationExecutionOptions` に typed プロパティがなく、利用側は `ExtensionParameters.Set("copilot.skillDirectories", ...)` で指定する必要がある
- `disabledSkills` も同様

### 例外

- `CopilotRuntimeException` は `CliPath`, `ExitCode` を持つが、どのフェーズで失敗したかの情報がない
- 利用側は decorator で `Exception.Data` に phase 情報を後付けしている

### Timeout

- `GitHubCopilotProviderOptions.TimeoutSeconds` はプロバイダーレベルの固定値
- `ConversationExecutionOptions` にタイムアウト指定がなく、リクエスト単位の調整が不可能

### CLI 解決

- `CopilotClientOptions.CliPath` に値を渡すか、SDK のデフォルト解決に委ねるかの 2 択
- SDK のデフォルト解決は `COPILOT_CLI_PATH` 環境変数 → バンドル CLI → PATH の順
- Windows では `copilot.cmd` / `copilot.bat` が返る場合があり、利用側が吸収している

## Proposed design / architecture delta

### WA-1: DI 登録の簡素化

**方針**: 3 段階のアプローチを取る。

#### 1a. `DefaultCopilotSdkWrapper` を fail-fast 化

現在の `DefaultCopilotSdkWrapper` がスタブ応答を返す挙動を、即座に `InvalidOperationException` をスローする挙動に変更する。

メッセージ例: `"GitHub Copilot SDK wrapper is not configured. Call AddGitHubCopilotSdkWrapper() or use AddGitHubCopilot() for production use."`

これにより、`AddGitHubCopilotProvider` のみ呼んだ場合に実行時エラーで即座に気付ける。

#### 1b. 統合 API `AddGitHubCopilot(configuration)` の追加

```text
利用側の最短コード:
  services.AddMultiProviderChat(configuration);
  services.AddGitHubCopilot(configuration);    // ← 新規。provider + SDK wrapper を一括登録
```

`AddGitHubCopilot` は内部で `AddGitHubCopilotProvider` + `AddGitHubCopilotSdkWrapper` を呼ぶ統合メソッド。

#### 1c. 既存 API の後方互換維持

- `AddGitHubCopilotProvider` + `AddGitHubCopilotSdkWrapper` の個別呼び出しパターンはそのまま維持
- `AddGitHubCopilotProvider` 単体呼び出しはテスト用途（独自 wrapper 差し替え）向けとして位置づけを明確化

### WA-2: File Attachment 公開 API

**方針**: SDK 固有型を漏らさずに、ファイル添付を利用側から指定可能にする。

#### 型設計

Core パッケージ（`MeAiUtility.MultiProvider`）に以下を追加する方針：

```text
ConversationExecutionOptions
  └── Attachments: IReadOnlyList<FileAttachment>?

FileAttachment
  ├── Path: string (required, 絶対パス)
  └── DisplayName: string? (表示名、省略時はファイル名)
```

- `FileAttachment` は Core パッケージの `Options` 名前空間に配置
- GitHub Copilot 以外のプロバイダーで `Attachments` が指定された場合は `NotSupportedException` で fail-fast
- GitHub Copilot プロバイダーは `FileAttachment` → SDK の `UserMessageDataAttachmentsItemFile` へ変換

#### 伝播経路

```text
ConversationExecutionOptions.Attachments
  → CopilotSessionConfig.Attachments  (新規プロパティ)
  → CopilotSdkInvocation.Attachments  (新規フィールド)
  → MessageOptions.Attachments         (SDK API)
```

- `CopilotSessionConfig` に `IReadOnlyList<FileAttachment>? Attachments` を追加
- `ICopilotSdkWrapper.SendAsync` のシグネチャは変更せず、`CopilotSessionConfig` 経由で伝播
- `BuildSdkSessionConfig` ではなく `SendAsync` 内の `MessageOptions` 構築時に Attachments をマッピング

### WA-3: SkillDirectories / DisabledSkills の型安全 API 化

**方針**: `ConversationExecutionOptions` に first-class プロパティを追加し、`AdvancedOptions` 経由の指定も引き続き動作させる。

```text
ConversationExecutionOptions
  ├── SkillDirectories: IReadOnlyList<string>?     (新規)
  └── DisabledSkills: IReadOnlyList<string>?       (新規)
```

#### マージ戦略

1. `ConversationExecutionOptions.SkillDirectories` が指定されていれば、それを優先
2. 未指定の場合は `AdvancedOptions["copilot.skillDirectories"]` にフォールバック
3. 両方指定された場合は `ConversationExecutionOptions` の typed プロパティを優先（AdvancedOptions は無視）
4. `DisabledSkills` も同様

#### GitHubCopilotChatClient での処理

`ValidateExtensions` 内で、`ConversationExecutionOptions` の typed プロパティを `CopilotSessionConfig.AdvancedOptions` よりも優先して `CopilotSessionConfig` に設定する。

### WA-4: 例外 Phase 情報

**方針**: `CopilotRuntimeException` に操作フェーズを示すプロパティを追加する。

```text
CopilotRuntimeException
  └── Operation: CopilotOperation? (新規)

enum CopilotOperation
  ├── ClientInitialization
  ├── ListModels
  └── Send
```

- `CopilotClientHost.ListModelsAsync` で catch 時に `Operation = CopilotOperation.ListModels` を設定
- `GitHubCopilotChatClient.GetResponseAsync` で catch 時に `Operation = CopilotOperation.Send` を設定
- `GetOrCreateClientAsync` 内の初期化失敗時に `Operation = CopilotOperation.ClientInitialization` を設定
- 既存の `CopilotRuntimeException` コンストラクタに `CopilotOperation?` パラメータを追加（既定値 null で後方互換）

### WA-5: Request 単位 Timeout Override

**方針**: `ConversationExecutionOptions` にタイムアウト指定を追加する。

```text
ConversationExecutionOptions
  └── TimeoutSeconds: int?  (新規)
```

- 未指定時は `GitHubCopilotProviderOptions.TimeoutSeconds` にフォールバック（現行動作と同一）
- 指定時は `CopilotSdkInvocation.TimeoutSeconds` を上書き
- `CopilotSessionConfig` にも `TimeoutSeconds` を追加し、`GitHubCopilotChatClient` → `GitHubCopilotSdkWrapper.BuildInvocation` の経路で request 単位値を伝播する
- 0 以下の値は `InvalidRequestException` で拒否

### WA-6: CLI 解決体験の改善

**方針**: `GitHubCopilotSdkWrapper` の初期化時に、`CliPath` 未指定の場合に独自の探索ロジックを実行する。SDK 自体にもデフォルト解決があるため、ライブラリの探索は SDK の前段で補助的に動作し、見つからなければ SDK のデフォルトに委ねる。

#### 探索順序（案）

1. `GitHubCopilotProviderOptions.CliPath` が明示指定されていればそれを使用
2. 環境変数 `COPILOT_CLI_PATH` が設定されていればそれを使用
3. 上記いずれも未指定の場合は SDK のデフォルト解決に委ねる（バンドル CLI → PATH）

この方針では、ライブラリ側は CLI 探索ロジック自体を独自実装しない。代わりに以下を行う：

- SDK のデフォルト解決で CLI が見つからなかった場合のエラーメッセージを強化する
- 初期化失敗時に、環境情報（OS、PATH、既知の CLI 配置候補パス）をログ出力する
- README にプラットフォームごとの CLI 配置例と、`CliPath` を固定すべきケースを文書化する

**理由**: SDK 自体が CLI 解決ロジックを持つため（バンドル CLI を含む）、ライブラリ側で重複した探索を実装するとバージョンアップ時の互換性リスクが高い。SDK の解決に委ねつつ、失敗時の診断情報を充実させる方がメンテナンス性が高い。

### WA-7: README / ドキュメント整備

以下を README に追記する：

1. **GitHub Copilot 最短構成コード**: `AddGitHubCopilot(configuration)` を使った 2 行の最短例を冒頭に配置
2. **構成パターン一覧表**: 統合 API / 個別 API / テスト用スタブの使い分けを表形式で説明
3. **DLL 配布時の依存関係ガイド**: GitHub Copilot 利用時に必要な追加 package と推奨 `IncludeAssets` / `ExcludeAssets` 設定
4. **CliPath 解決方針**: プラットフォーム別の CLI 配置候補パスと `CliPath` の固定が必要なケース
5. **Copilot パラメータ一覧**: `copilot.*` キーで指定可能なパラメータの一覧表と、provider option vs request option の責務分担
6. **例外の診断情報一覧**: 例外型ごとの保持プロパティと `Operation` フィールドの意味
7. **File Attachment の利用例**: JSON ファイル添付のサンプルコード
8. **SkillDirectories の利用例**: スキル読み込みのサンプルコード

## Coarse interaction scenarios

> 詳細なシーケンス図と Component–Step Map は [Runtime Evidence](copilot-provider-improvements-and-extensions-runtime-evidence.md) を参照。

| ID | シナリオ | 分類 | 概要 |
|----|---------|------|------|
| S-01 | 統合 API による最短利用 | 正常系 | `AddGitHubCopilot` → `GetResponseAsync` → 応答取得 |
| S-02 | File Attachment + SkillDirectories 併用 | 正常系 | typed プロパティで Attachments + SkillDirectories 指定 → SDK まで伝播 |
| S-03 | AddGitHubCopilotProvider のみ (fail-fast) | エラー系 | SDK wrapper 未登録 → `InvalidOperationException` → `CopilotRuntimeException` |
| S-04a | ListModels 失敗 Phase 付き例外 | エラー系 | `Operation = ListModels` 付き `CopilotRuntimeException` |
| S-04b | Send 失敗 Phase 付き例外 | エラー系 | `Operation = Send` 付き `CopilotRuntimeException` |
| S-05 | Request 単位 Timeout Override | 正常系 | `TimeoutSeconds = 300` → `SendAndWaitAsync` タイムアウト上書き |
| S-06 | CLI 未検出時の診断ログ | エラー系 | 初期化失敗 → OS/PATH/候補パスの診断ログ |
| S-07 | 非 Copilot プロバイダーでの拒否 | エラー系 | Attachments/SkillDirectories → `NotSupportedException` |

## Impacted code / files / modules

### 新規追加

| ファイル | 内容 |
|---------|------|
| `src/MeAiUtility.MultiProvider/Options/FileAttachment.cs` | ファイル添付の公開型 |
| `src/MeAiUtility.MultiProvider/Options/CopilotOperation.cs` | 操作フェーズ enum |

### 変更

| ファイル | 変更内容 |
|---------|----------|
| `src/MeAiUtility.MultiProvider/Options/ConversationExecutionOptions.cs` | `Attachments`, `SkillDirectories`, `DisabledSkills`, `TimeoutSeconds` プロパティ追加 |
| `src/MeAiUtility.MultiProvider/Exceptions/ProviderExceptions.cs` | `CopilotRuntimeException` に `Operation` プロパティ追加 |
| `src/MeAiUtility.MultiProvider.GitHubCopilot/Configuration/GitHubCopilotServiceExtensions.cs` | `AddGitHubCopilot` 統合メソッド追加、`DefaultCopilotSdkWrapper` fail-fast 化 |
| `src/MeAiUtility.MultiProvider.GitHubCopilot/Abstractions/ICopilotSdkWrapper.cs` | `CopilotSessionConfig` に `Attachments`, `SkillDirectories`, `DisabledSkills`, `TimeoutSeconds` typed プロパティ追加 |
| `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotSdkWrapper.cs` | `BuildInvocation` / `SendAsync` で Attachments / SkillDirectories typed 化対応、Phase 付き例外、CLI 初期化診断 |
| `src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs` | `ConversationExecutionOptions` から SkillDirectories / Attachments / TimeoutSeconds の取り出しと設定 |
| `src/MeAiUtility.MultiProvider.GitHubCopilot/CopilotClientHost.cs` | 例外 catch 時の Phase 情報設定 |
| `src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs` | `Attachments`, `SkillDirectories`, `DisabledSkills` 指定時の `NotSupportedException` ガード追加 |
| `src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs` | `Attachments`, `SkillDirectories`, `DisabledSkills` 指定時の `NotSupportedException` ガード追加 |
| `src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs` | `Attachments`, `SkillDirectories`, `DisabledSkills` 指定時の `NotSupportedException` ガード追加 |
| `src/MeAiUtility.MultiProvider.Samples/GitHubCopilotSampleHost.cs` | `AddGitHubCopilot` 利用例に更新 |
| `README.md` | WA-7 の全項目 |

### テスト変更

| ファイル | 変更内容 |
|---------|----------|
| `tests/MeAiUtility.MultiProvider.Tests/Options/ConversationExecutionOptionsTests.cs` | 新プロパティのシリアライズ・デシリアライズ |
| `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotChatClientTests.cs` | Attachments / SkillDirectories / TimeoutSeconds の伝播、Phase 付き例外 |
| `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/GitHubCopilotSdkWrapperTests.cs` | BuildInvocation の typed プロパティ変換、Attachments マッピング |
| `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/CopilotClientHostTests.cs` | Phase 付き例外 |
| `tests/MeAiUtility.MultiProvider.OpenAI.Tests/...` | `Attachments`, `SkillDirectories`, `DisabledSkills` 指定時の `NotSupportedException` |
| `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/...` | `Attachments`, `SkillDirectories`, `DisabledSkills` 指定時の `NotSupportedException` |
| 新規テスト: DI 登録テスト | `AddGitHubCopilot` 統合 API、fail-fast スタブの検証 |

## Verification design

> 詳細なブラックボックステスト観点（55 観点）は [Integration Test Points](copilot-provider-improvements-and-extensions-integration-test-points.md) を参照。

### Unit Tests

| テスト対象 | 検証内容 | 対応 WA |
|-----------|---------|---------|
| `DefaultCopilotSdkWrapper` | `ListModelsAsync` / `SendAsync` が `InvalidOperationException` をスローする | WA-1 |
| `AddGitHubCopilot` | DI コンテナから `ICopilotSdkWrapper` 解決時に `GitHubCopilotSdkWrapper` が返る | WA-1 |
| `AddGitHubCopilotProvider` 単体 | DI コンテナから `ICopilotSdkWrapper` 解決後、`SendAsync` が fail-fast する | WA-1 |
| `ConversationExecutionOptions` | `Attachments` / `SkillDirectories` / `DisabledSkills` / `TimeoutSeconds` の set/get | WA-2, WA-3, WA-5 |
| `FileAttachment` | `Path` 必須、`DisplayName` 省略可のバリデーション | WA-2 |
| `BuildInvocation` | typed `SkillDirectories` が `CopilotSdkInvocation.SkillDirectories` にマッピングされる | WA-3 |
| `BuildInvocation` | typed `Attachments` が `CopilotSdkInvocation.Attachments` にマッピングされる | WA-2 |
| `BuildInvocation` | `AdvancedOptions` と typed プロパティ両方指定時に typed 優先 | WA-3 |
| `BuildInvocation` | `TimeoutSeconds` specified → invocation.TimeoutSeconds 上書き | WA-5 |
| `BuildInvocation` | `TimeoutSeconds` 未指定 → provider default | WA-5 |
| `SendAsync` | `Attachments` が SDK `MessageOptions.Attachments` へ変換される | WA-2 |
| `CopilotRuntimeException` | `Operation` プロパティの正常取得 | WA-4 |
| `GitHubCopilotChatClient` | ListModels 失敗時 `Operation = ListModels` | WA-4 |
| `GitHubCopilotChatClient` | Send 失敗時 `Operation = Send` | WA-4 |
| `GitHubCopilotChatClient` | Attachments 指定時の伝播確認（モック SDK wrapper） | WA-2 |
| GitHub Copilot 以外のプロバイダー | `Attachments` 指定時に `NotSupportedException` | WA-2 |
| 後方互換 | 新プロパティ未指定時に既存動作が変わらない | 全 WA |

### Integration Tests (スタブ/モック使用)

| テスト対象 | 検証内容 | 対応 WA |
|-----------|---------|---------|
| DI 構成テスト | `AddGitHubCopilot(configuration)` → `IChatClient` 解決成功 | WA-1 |
| fail-fast テスト | `AddGitHubCopilotProvider(configuration)` のみ → send 時例外 | WA-1 |
| E2E パス検証 | Attachment + SkillDirectories → モック wrapper まで正しく伝播 | WA-2, WA-3 |
| 例外チェーン | Phase 付き `CopilotRuntimeException` が利用側まで到達 | WA-4 |
| 非 Copilot ガード | OpenAI / AzureOpenAI / OpenAICompatible が新設 Copilot 専用オプションを明示拒否する | WA-2, WA-3 |

### 手動検証（実 CLI 環境）

| 検証項目 | 内容 | 対応 WA |
|---------|------|---------|
| 実 Copilot 呼び出し | `AddGitHubCopilot` → 実 CLI で応答取得 | WA-1 |
| SkillDirectories 実動作 | SKILL.md を含むディレクトリ指定 → skill 内容が反映された応答 | WA-3 |
| File Attachment 実動作 | JSON ファイル添付 → JSON 内容を読んだ応答 | WA-2 |
| 併用検証 | SkillDirectories + File Attachment 同時指定 → 両方反映 | WA-2, WA-3 |
| CLI 未検出診断 | CliPath 未指定 + CLI 未インストール環境 → 診断ログ出力確認 | WA-6 |
| ドキュメント検証 | README に記載する DLL 参照 / package 参照パターンを最小サンプル csproj で restore/build して成立確認 | WA-7 |

## Traceability matrix

| 要求 / 期待動作 | シナリオ | 検証方法 |
|----------------|---------|---------|
| DI 1 API で実運用構成 (Review High-1) | S-01 | UT: AddGitHubCopilot DI テスト + 手動: 実 CLI 呼び出し |
| Provider-only 構成が fail-fast (Review High-1) | S-03 | UT: DefaultCopilotSdkWrapper fail-fast + IT: DI fail-fast テスト |
| File Attachment 指定可能 (RQ-02) | S-02 | UT: BuildInvocation / SendAsync マッピング + 手動: 実動作確認 |
| SkillDirectories typed API (RQ-01) | S-02 | UT: ConversationExecutionOptions / BuildInvocation + 手動: 実動作確認 |
| SkillDirectories + Attachment 併用 (AC-04) | S-02 | IT: E2E パス検証 + 手動: 併用検証 |
| 後方互換 (RQ-05 / AC-05) | - | UT: 新プロパティ未指定時の既存動作 |
| Phase 情報で障害分類 (Review Med-3) | S-04 | UT: Operation プロパティ検証 |
| Request 単位 Timeout (Review Med-2) | S-05 | UT: BuildInvocation timeout マッピング |
| CLI 解決の診断改善 (Review Med-1) | S-06 | 手動: CLI 未検出時のログ確認 |
| Provider abstraction 維持 (RQ-03) | 全シナリオ | UT: FileAttachment が Core pkg の型、SDK 型を参照しない |
| 非 Copilot プロバイダーの明示拒否 | S-07 | IT/UT: OpenAI / AzureOpenAI / OpenAICompatible の `NotSupportedException` |
| README 整備 (Review High-2,3) | - | 手動: README 記載例に対応する最小サンプル csproj の restore/build 成立確認 |

## Definition of Done

1. `AddGitHubCopilot(configuration)` で provider + SDK wrapper が一括登録され、実 CLI での応答取得が確認できる
2. `AddGitHubCopilotProvider(configuration)` のみで wrapper 直呼び出し時は `InvalidOperationException` がスローされ、`IChatClient.GetResponseAsync` 経由では `CopilotRuntimeException(Operation = ListModels)` として観測できる
3. `ConversationExecutionOptions.Attachments` に `FileAttachment` を設定し、GitHub Copilot 経由で JSON 内容を読んだ応答が得られる
4. `ConversationExecutionOptions.SkillDirectories` に typed API でパスを設定し、skill 内容が応答に反映される
5. `SkillDirectories` + `Attachments` の同時指定で両方が反映される
6. 新プロパティ未指定時に既存動作が変わらない（後方互換）
7. `CopilotRuntimeException.Operation` で ListModels / Send / ClientInitialization の区別が可能
8. `ConversationExecutionOptions.TimeoutSeconds` で request 単位 timeout が機能する
9. CLI 未検出時の例外メッセージに診断情報（OS、PATH 情報等）が含まれる
10. 全 Unit Test がパスする
11. 全 Integration Test（スタブ使用）がパスする
12. README に WA-7 の全項目が記載されている
13. README に記載した DLL / package 参照パターンが最小サンプル csproj の restore/build で成立確認されている

## Risks / rollout / rollback

### Risks

| リスク | 影響 | 緩和策 |
|--------|------|--------|
| `DefaultCopilotSdkWrapper` fail-fast 化が既存利用者を破壊 | `AddGitHubCopilotProvider` のみ使用している利用者に影響 | 例外メッセージに解決方法を明記。リリースノートで移行手順を案内 |
| GitHub Copilot SDK の `MessageOptions.Attachments` の型互換性 | SDK Public Preview のため API が変更される可能性 | SDK 特定バージョンへの依存を明示。Attachments マッピングを集約してリカバリしやすくする |
| `ConversationExecutionOptions` のプロパティ追加が Core パッケージの API surface を拡大 | GitHub Copilot 以外のプロバイダーで意味を持たないプロパティが増える | `Attachments` / `SkillDirectories` は「プロバイダーがサポートしない場合は `NotSupportedException`」というプロバイダー固有の既存方針に沿う |
| CLI 解決の診断ログが過剰になり他のログを埋没させる | 初起動時のみのためリスクは低い | ログレベルを Warning に設定し、初回失敗時のみ出力 |

### Rollback

- 全変更は後方互換（新プロパティ追加 + default null）なので、利用側がプロパティを使わなければ従来動作する
- `DefaultCopilotSdkWrapper` fail-fast 化のみが breaking change であり、ロールバックが必要な場合はこの変更のみ revert する

## Open questions / assumptions

| # | 内容 | 想定 / 仮定 |
|---|------|------------|
| OQ-1 | GitHub Copilot SDK の `UserMessageDataAttachmentsItemFile` は将来も安定するか？ | Public Preview 段階のため不安定の可能性あり。マッピング層を分離して影響範囲を制限する |
| OQ-2 | `FileAttachment` に MIME type 指定は必要か？ | 現時点では不要と仮定（JSON ファイルが主用途、SDK が自動判定）。将来要望があれば追加 |
| OQ-3 | `SkillDirectories` を `ConversationExecutionOptions` に置くと、OpenAI 等で無意味なプロパティになるが許容可能か？ | 許容する。既存の `ProviderOverride`（Copilot 専用）と同じ方針。非対応プロバイダーでは `NotSupportedException` |
| OQ-4 | `DefaultCopilotSdkWrapper` の fail-fast 化は major version bump が必要か？ | スタブ応答を本番利用することは設計上想定外のため、patch/minor でリリース可能と仮定。ただしリリースノートで明記する |
| OQ-5 | `ConversationExecutionOptions.TimeoutSeconds` は GitHub Copilot 以外でも有用か？ | HTTP プロバイダーでは `HttpClient.Timeout` で制御済みのため、当面は GitHub Copilot のみ対応。他プロバイダーでは無視する |

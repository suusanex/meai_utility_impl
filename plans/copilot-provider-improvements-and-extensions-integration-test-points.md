# GitHub Copilot プロバイダー改善・拡張 — ブラックボックステスト観点

**対象 Plan**: `plans/copilot-provider-improvements-and-extensions.md`
**Date**: 2026-04-06

---

## テスト前提

- テストフレームワーク: NUnit + Moq
- 外部 API 呼び出しなし。`ICopilotSdkWrapper` をモック/スタブで差し替え
- 実 OS 環境（レジストリ、サービス、CLI バイナリ等）を変更しない
- DI 構成テストは `ServiceCollection` → `BuildServiceProvider` で検証
- GitHub Copilot 以外のプロバイダー（OpenAI / AzureOpenAI / OpenAICompatible）の挙動差異も対象

---

## テスト観点一覧

### WA-1: DI 登録の簡素化

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-1-01 | WA-1 | 正常系 | `AddGitHubCopilot` 統合 API による一括登録 | `services.AddGitHubCopilot(configuration)` を呼び出し、DI コンテナから `ICopilotSdkWrapper` を解決する | `GitHubCopilotSdkWrapper` 型のインスタンスが返る。`DefaultCopilotSdkWrapper` ではない |
| T-1-02 | WA-1 | 正常系 | `AddGitHubCopilot` で `IChatClient` が解決可能 | `AddMultiProviderChat` + `AddGitHubCopilot` の構成で `IChatClient` を解決する | `GitHubCopilotChatClient` を含むチェーンが返る |
| T-1-03 | WA-1 | 正常系 | `AddGitHubCopilot` で `ICopilotModelCatalog` が解決可能 | 同上の構成で `ICopilotModelCatalog` を解決する | `ListModelsAsync` が呼び出し可能 |
| T-1-04 | WA-1 | 異常系 | `AddGitHubCopilotProvider` のみで `ListModelsAsync` 呼び出し → fail-fast | `AddGitHubCopilotProvider` のみ登録し、`ICopilotSdkWrapper.ListModelsAsync` を呼び出す | `InvalidOperationException` がスローされる。メッセージに `AddGitHubCopilotSdkWrapper` または `AddGitHubCopilot` の案内を含む |
| T-1-05 | WA-1 | 異常系 | `AddGitHubCopilotProvider` のみで `SendAsync` 呼び出し → fail-fast | `AddGitHubCopilotProvider` のみ登録し、`ICopilotSdkWrapper.SendAsync` を呼び出す | `InvalidOperationException` がスローされる。メッセージに解決方法の案内を含む |
| T-1-06 | WA-1 | 異常系 | `AddGitHubCopilotProvider` のみで `GetResponseAsync` → 例外伝播 | `AddGitHubCopilotProvider` のみ登録した構成で `IChatClient.GetResponseAsync` を呼び出す | `DefaultCopilotSdkWrapper` の `InvalidOperationException` が `CopilotRuntimeException` にラップされて伝播する |
| T-1-07 | WA-1 | 後方互換 | 既存 `AddGitHubCopilotProvider` + `AddGitHubCopilotSdkWrapper` パターン | 従来の 2 段階登録で `IChatClient.GetResponseAsync` を呼び出す（モック wrapper で応答返却） | 正常に応答が得られる。既存動作に変更なし |
| T-1-08 | WA-1 | 後方互換 | `AddGitHubCopilotProvider` + カスタム `ICopilotSdkWrapper` 差し替え | `AddGitHubCopilotProvider` 呼び出し後、テスト用のカスタム `ICopilotSdkWrapper` 実装を DI に登録する | カスタム実装が優先される。`DefaultCopilotSdkWrapper` は使用されない |
| T-1-09 | WA-1 | 異常系 | `AddGitHubCopilot` に `null` configuration | `services.AddGitHubCopilot(null!)` を呼び出す | `ArgumentNullException` がスローされる |

### WA-2: File Attachment 公開 API

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-2-01 | WA-2 | 正常系 | `FileAttachment` (Path + DisplayName) が SDK wrapper まで伝播 | `ConversationExecutionOptions.Attachments` に `FileAttachment { Path = "C:\\data.json", DisplayName = "data" }` を設定し `GetResponseAsync` を呼び出す | モック `ICopilotSdkWrapper.SendAsync` に渡された `CopilotSessionConfig.Attachments` に同一の Path / DisplayName が含まれる |
| T-2-02 | WA-2 | 正常系 | `FileAttachment` の `DisplayName` 省略 | `FileAttachment { Path = "C:\\file.txt" }` のみ設定（DisplayName = null） | 伝播される Attachment の Path が正しく、DisplayName が null または省略される |
| T-2-03 | WA-2 | 正常系 | 複数 `FileAttachment` の伝播 | Attachments に 3 件の `FileAttachment` を設定 | 3 件すべてが順序を保って `CopilotSessionConfig.Attachments` に伝播する |
| T-2-04 | WA-2 | 正常系 | `Attachments` 未指定時の従来動作 | `ConversationExecutionOptions.Attachments` を未設定（null）で `GetResponseAsync` を呼び出す | `CopilotSessionConfig.Attachments` が null または空。送信処理は正常完了 |
| T-2-05 | WA-2 | 正常系 | 空リストの `Attachments` | `Attachments = new List<FileAttachment>()` を設定 | エラーにならない。Attachments なしとして処理される |
| T-2-06 | WA-2 | 異常系 | OpenAI プロバイダーで `Attachments` 指定 | OpenAI プロバイダーに対して `Attachments` 付きの `ConversationExecutionOptions` で `GetResponseAsync` を呼び出す | `NotSupportedException` がスローされる。FeatureName に "Attachments" 相当を含む |
| T-2-07 | WA-2 | 異常系 | AzureOpenAI プロバイダーで `Attachments` 指定 | AzureOpenAI プロバイダーに対して同上 | `NotSupportedException` がスローされる |
| T-2-08 | WA-2 | 異常系 | OpenAICompatible プロバイダーで `Attachments` 指定 | OpenAICompatible プロバイダーに対して同上 | `NotSupportedException` がスローされる |
| T-2-09 | WA-2 | 境界値 | `FileAttachment.Path` が空文字列 | `FileAttachment { Path = "" }` を設定 | バリデーションエラー（`ArgumentException` または `InvalidRequestException`）がスローされる |
| T-2-10 | WA-2 | 境界値 | `FileAttachment.Path` が null | `FileAttachment { Path = null! }` を設定 | バリデーションエラーがスローされる（コンストラクタまたはバリデーション時） |

### WA-3: SkillDirectories / DisabledSkills の型安全 API

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-3-01 | WA-3 | 正常系 | typed `SkillDirectories` の伝播 | `ConversationExecutionOptions.SkillDirectories = ["D:\\skills"]` を設定し `GetResponseAsync` を呼び出す | `CopilotSessionConfig.SkillDirectories` に `"D:\\skills"` が設定される |
| T-3-02 | WA-3 | 正常系 | typed `DisabledSkills` の伝播 | `ConversationExecutionOptions.DisabledSkills = ["skill-a", "skill-b"]` を設定 | `CopilotSessionConfig` の DisabledSkills に `"skill-a"`, `"skill-b"` が設定される |
| T-3-03 | WA-3 | 正常系 | `SkillDirectories` 未指定、`AdvancedOptions` 経由のフォールバック | typed プロパティ未指定、`ExtensionParameters.Set("copilot.skillDirectories", [...])` で設定 | `AdvancedOptions` 経由の値が `CopilotSessionConfig` に反映される |
| T-3-04 | WA-3 | 正常系 | `DisabledSkills` 未指定、`AdvancedOptions` 経由のフォールバック | typed プロパティ未指定、`ExtensionParameters.Set("copilot.disabledSkills", [...])` で設定 | `AdvancedOptions` 経由の値が反映される |
| T-3-05 | WA-3 | 正常系 | typed と `AdvancedOptions` 両方指定時の優先順位（SkillDirectories） | typed: `["D:\\skills-typed"]`, AdvancedOptions: `["D:\\skills-adv"]` を両方指定 | typed プロパティ `"D:\\skills-typed"` が優先される。AdvancedOptions の値は無視 |
| T-3-06 | WA-3 | 正常系 | typed と `AdvancedOptions` 両方指定時の優先順位（DisabledSkills） | typed: `["skill-typed"]`, AdvancedOptions: `["skill-adv"]` を両方指定 | typed プロパティ `"skill-typed"` が優先される |
| T-3-07 | WA-3 | 後方互換 | 新プロパティ未指定、`AdvancedOptions` のみで従来動作 | typed プロパティすべて null、`ExtensionParameters` 経由でのみ copilot 拡張パラメータ設定 | 既存動作と同一。AdvancedOptions の値がそのまま `CopilotSessionConfig.AdvancedOptions` に反映 |
| T-3-08 | WA-3 | 後方互換 | 新プロパティもAdvancedOptionsも未指定 | すべて未指定で `GetResponseAsync` を呼び出す | エラーにならない。SkillDirectories / DisabledSkills は空/null |
| T-3-09 | WA-3 | 境界値 | `SkillDirectories` に空リストを指定 | `SkillDirectories = new List<string>()` | エラーにならない。空リストとして伝播 |
| T-3-10 | WA-3 | 境界値 | `SkillDirectories` に複数ディレクトリを指定 | `SkillDirectories = ["D:\\skills1", "D:\\skills2", "D:\\skills3"]` | 3 件すべてが順序を保って伝播 |
| T-3-11 | WA-3 | 異常系 | OpenAI プロバイダーで `SkillDirectories` 指定 | OpenAI プロバイダーに対して typed `SkillDirectories` 付きで呼び出す | `NotSupportedException` がスローされる |

### WA-4: 例外 Phase 情報

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-4-01 | WA-4 | 正常系 | ListModels 失敗時の Operation 設定 | `ICopilotSdkWrapper.ListModelsAsync` が例外をスローする状況で `GetResponseAsync` を呼び出す | `CopilotRuntimeException.Operation == CopilotOperation.ListModels` |
| T-4-02 | WA-4 | 正常系 | Send 失敗時の Operation 設定 | `ICopilotSdkWrapper.SendAsync` が例外をスローする状況で `GetResponseAsync` を呼び出す | `CopilotRuntimeException.Operation == CopilotOperation.Send` |
| T-4-03 | WA-4 | 正常系 | Client 初期化失敗時の Operation 設定 | `CopilotClientHost` の内部初期化（`GetOrCreateClientAsync`）で例外が発生する状況 | `CopilotRuntimeException.Operation == CopilotOperation.ClientInitialization` |
| T-4-04 | WA-4 | 正常系 | Operation と既存プロパティの共存 | Phase 付き例外が `CliPath`, `ExitCode` も保持しているか | `CopilotRuntimeException` に `Operation`, `CliPath`, `ExitCode` すべてが設定される |
| T-4-05 | WA-4 | 後方互換 | Operation 未設定の `CopilotRuntimeException` 生成（既存コンストラクタ） | 既存コンストラクタ（Operation パラメータなし）で `CopilotRuntimeException` を生成する | `Operation` が null。他のプロパティ（`CliPath`, `ExitCode`, `Message`）は正常 |
| T-4-06 | WA-4 | 後方互換 | 既存の例外ハンドリングコードで Phase 無視 | Operation を参照せずに `CopilotRuntimeException` を catch するコードパス | 既存コードが破壊されない。catch ブロックは新プロパティの有無に影響されない |
| T-4-07 | WA-4 | 正常系 | InnerException チェーン内の Phase 情報 | ListModels → 内部例外をラップした `CopilotRuntimeException` が生成される | `InnerException` に元の例外が保持され、かつ `Operation == ListModels` |

### WA-5: Request 単位 Timeout Override

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-5-01 | WA-5 | 正常系 | `TimeoutSeconds` 指定による上書き | `ConversationExecutionOptions.TimeoutSeconds = 300` を設定し `GetResponseAsync` を呼び出す | モック wrapper に渡される invocation のタイムアウトが 300 秒。provider default (120) は使用されない |
| T-5-02 | WA-5 | 正常系 | `TimeoutSeconds` 未指定時のフォールバック | `ConversationExecutionOptions.TimeoutSeconds` を null（未指定）のまま呼び出す | provider options の `TimeoutSeconds` (例: 120) がフォールバックとして適用される |
| T-5-03 | WA-5 | 異常系 | `TimeoutSeconds = 0` の拒否 | `TimeoutSeconds = 0` を設定 | `InvalidRequestException` がスローされる |
| T-5-04 | WA-5 | 異常系 | `TimeoutSeconds` が負値 | `TimeoutSeconds = -1` を設定 | `InvalidRequestException` がスローされる |
| T-5-05 | WA-5 | 境界値 | `TimeoutSeconds = 1` (最小正値) | `TimeoutSeconds = 1` を設定 | 正常に受け付けられ、1 秒タイムアウトとして機能する |
| T-5-06 | WA-5 | 境界値 | `TimeoutSeconds` が非常に大きい値 | `TimeoutSeconds = int.MaxValue` を設定 | エラーにならず、指定値がそのまま適用される |
| T-5-07 | WA-5 | 後方互換 | 新プロパティ未指定で既存タイムアウト動作維持 | `TimeoutSeconds` 未指定、provider options `TimeoutSeconds = 120` | 従来と同一のタイムアウト動作（120 秒） |

### WA-6: CLI 解決戦略の改善

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-6-01 | WA-6 | 正常系 | CLI 未検出時のエラーメッセージに OS 情報を含む | SDK wrapper 初期化で CLI 未検出をシミュレート（モックで `ClientInitialization` 例外をスロー） | 例外メッセージまたはログに OS 情報（`Environment.OSVersion` 相当）を含む |
| T-6-02 | WA-6 | 正常系 | CLI 未検出時のエラーメッセージに PATH 情報を含む | 同上 | 例外メッセージまたはログに PATH 環境変数の内容を含む |
| T-6-03 | WA-6 | 正常系 | CLI 未検出時のエラーメッセージに候補パスを含む | 同上 | 例外メッセージまたはログに CLI の既知候補パス（プラットフォーム別）を含む |
| T-6-04 | WA-6 | 正常系 | `CliPath` 明示指定時に探索をスキップ | `GitHubCopilotProviderOptions.CliPath = "C:\\custom\\copilot.exe"` を設定 | 指定パスが直接使用される。候補パス探索は行われない |

### WA 間の組み合わせ

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-C-01 | WA-2 + WA-3 | 組合せ | Attachments + SkillDirectories 同時指定 | `Attachments = [FileAttachment]` かつ `SkillDirectories = ["D:\\skills"]` を同時設定し `GetResponseAsync` を呼び出す | 両方が `CopilotSessionConfig` に正しく設定される。どちらか一方が欠落しない |
| T-C-02 | WA-2 + WA-3 | 組合せ | Attachments + DisabledSkills 同時指定 | `Attachments` と `DisabledSkills` を同時設定 | 両方が正しく伝播する |
| T-C-03 | WA-2 + WA-3 | 組合せ | Attachments + SkillDirectories + DisabledSkills 全指定 | 3 プロパティすべてを同時設定 | 3 つすべてが `CopilotSessionConfig` に正しく反映される |
| T-C-04 | WA-2 + WA-5 | 組合せ | Attachments + TimeoutSeconds 同時指定 | `Attachments` と `TimeoutSeconds = 300` を同時設定 | Attachments が伝播し、かつタイムアウトが 300 秒で適用される |
| T-C-05 | WA-3 + WA-5 | 組合せ | SkillDirectories + TimeoutSeconds 同時指定 | `SkillDirectories` と `TimeoutSeconds = 60` を同時設定 | SkillDirectories が伝播し、かつタイムアウトが 60 秒で適用される |
| T-C-06 | WA-4 + WA-5 | 組合せ | Timeout + Phase 付き例外 | `TimeoutSeconds = 1` を設定し、SDK wrapper がタイムアウト相当の例外をスローする | `CopilotRuntimeException` に `Operation = Send` が設定される。タイムアウト情報も保持 |
| T-C-07 | WA-4 + WA-1 | 組合せ | fail-fast スタブ + Phase 情報 | `AddGitHubCopilotProvider` のみ登録で `GetResponseAsync` を呼び出す | `CopilotRuntimeException.Operation == ListModels`。InnerException に `InvalidOperationException` が含まれる |
| T-C-08 | WA-1 + WA-2 + WA-3 | 組合せ | `AddGitHubCopilot` 統合 API + Attachments + SkillDirectories | `AddGitHubCopilot` で登録し、Attachments + SkillDirectories 付きで `GetResponseAsync` を呼び出す | モック wrapper まで全プロパティが正しく伝播する |
| T-C-09 | WA-2 + WA-4 | 組合せ | Attachments 付き Send 失敗時の Phase 情報 | Attachments を設定して `GetResponseAsync` を呼び出し、`SendAsync` が失敗 | `CopilotRuntimeException.Operation == Send`。Attachments に起因するかは InnerException で判別可能 |
| T-C-10 | WA-3 + WA-3(AdvancedOptions) | 組合せ | SkillDirectories typed + DisabledSkills AdvancedOptions の混在 | `SkillDirectories` は typed、`DisabledSkills` は `AdvancedOptions` 経由で指定 | typed `SkillDirectories` と AdvancedOptions 経由の `DisabledSkills` がそれぞれ独立に解決・反映される |

### プロバイダー横断の挙動差異

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-P-01 | WA-2 | 異常系 | 全非 Copilot プロバイダーで Attachments 拒否の統一性 | OpenAI / AzureOpenAI / OpenAICompatible の各プロバイダーで `Attachments` 指定 | 全プロバイダーが統一的に `NotSupportedException` をスロー。FeatureName が一貫 |
| T-P-02 | WA-3 | 異常系 | 全非 Copilot プロバイダーで SkillDirectories 拒否の統一性 | OpenAI / AzureOpenAI / OpenAICompatible の各プロバイダーで `SkillDirectories` 指定 | 全プロバイダーが統一的に `NotSupportedException` をスロー |
| T-P-02a | WA-3 | 異常系 | 全非 Copilot プロバイダーで DisabledSkills 拒否の統一性 | OpenAI / AzureOpenAI / OpenAICompatible の各プロバイダーで `DisabledSkills` 指定 | 全プロバイダーが統一的に `NotSupportedException` をスロー |
| T-P-03 | WA-5 | 正常系 | 非 Copilot プロバイダーで `TimeoutSeconds` 指定 | OpenAI プロバイダーで `TimeoutSeconds = 60` を設定 | 無視される（エラーにならない）。HTTP タイムアウトは `HttpClient.Timeout` で別途制御 |
| T-P-04 | WA-4 | 正常系 | 非 Copilot プロバイダーの例外に Operation がないことの確認 | OpenAI プロバイダーで例外が発生した場合 | `MultiProviderException` 系の例外であり、`CopilotRuntimeException` ではない。`Operation` プロパティは存在しない |

### 負荷・連続実行

| ID | WA | カテゴリ | テスト観点 | 条件 | 期待結果 |
|----|----|---------|-----------|------|---------|
| T-L-01 | WA-1 | 連続 | DI コンテナから `IChatClient` を複数回解決 | `AddGitHubCopilot` 構成で `GetRequiredService<IChatClient>()` を連続呼び出し | Singleton として同一インスタンスが返る |
| T-L-02 | WA-2 | 連続 | `GetResponseAsync` を異なる Attachments で連続呼び出し | 1 回目: Attachments あり、2 回目: Attachments なし、3 回目: 別の Attachments あり | 各呼び出しで正しい Attachments が wrapper に伝播する。前回の値が残留しない |
| T-L-03 | WA-3 | 連続 | `GetResponseAsync` を異なる SkillDirectories で連続呼び出し | 1 回目: SkillDirectories = ["A"]、2 回目: SkillDirectories = ["B"]、3 回目: 未指定 | 各呼び出しでその回の設定が反映される。前回の設定が残留しない |
| T-L-04 | WA-5 | 連続 | 異なる TimeoutSeconds で連続呼び出し | 1 回目: TimeoutSeconds = 60、2 回目: TimeoutSeconds = 300、3 回目: 未指定（provider default へフォールバック） | 各呼び出しでその回の TimeoutSeconds が適用される |
| T-L-05 | WA-4 | 連続 | 失敗→成功→失敗の連続呼び出しでの Phase 情報 | 1 回目: ListModels 失敗、2 回目: 成功、3 回目: Send 失敗 | 1 回目は `Operation = ListModels`、2 回目は正常応答、3 回目は `Operation = Send`。各回で独立した Phase が報告される |

---

## トレーサビリティ

| Plan 要求 / シナリオ | テスト観点 ID |
|---------------------|-------------|
| S-01: 統合 API による最短利用 | T-1-01, T-1-02, T-1-03 |
| S-02: File Attachment + SkillDirectories 併用 | T-2-01, T-3-01, T-C-01, T-C-03, T-C-08 |
| S-03: AddGitHubCopilotProvider のみで fail-fast | T-1-04, T-1-05, T-1-06, T-C-07 |
| S-04: Phase 付き例外による障害分類 | T-4-01, T-4-02, T-4-03, T-4-04 |
| S-05: Request 単位 Timeout Override | T-5-01, T-5-02, T-C-06 |
| DI 登録の後方互換 (Review High-1) | T-1-07, T-1-08 |
| Attachment + 非 Copilot プロバイダー (RQ-03) | T-2-06, T-2-07, T-2-08, T-P-01 |
| SkillDirectories AdvancedOptions フォールバック | T-3-03, T-3-04, T-3-07 |
| typed プロパティ優先 (WA-3 マージ戦略) | T-3-05, T-3-06, T-C-10 |
| DisabledSkills の非 Copilot 拒否 | T-P-02a |
| 既存コンストラクタ後方互換 (WA-4) | T-4-05, T-4-06 |
| TimeoutSeconds バリデーション (WA-5) | T-5-03, T-5-04, T-5-05 |
| CLI 診断情報強化 (WA-6) | T-6-01, T-6-02, T-6-03, T-6-04 |
| プロバイダー横断の一貫性 | T-P-01, T-P-02, T-P-03, T-P-04 |
| 連続実行での状態独立性 | T-L-01, T-L-02, T-L-03, T-L-04, T-L-05 |

# GitHub Copilot プロバイダー改善・拡張 — 実装カバレッジ (Integration Test Coverage)

**対象観点**: `plans\copilot-provider-improvements-and-extensions-integration-test-points.md`
**更新日**: 2026-04-06

---

## 分類サマリー

- Automated: 64 件
- RecordedButSkipped: 4 件
- ManualOnly: 0 件
- NotImplementedOrMismatch: 0 件

---

## 詳細

| ID | 状態 | テスト名 (FQN/メソッド名) | 判定理由 |
|---|---|---|---|
| T-1-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilot_RegistersSdkWrapperAndChatClient` | `AddGitHubCopilot` が GitHub Copilot の本実装登録を行うことを確認した。 |
| T-1-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.UT_IT_T_1_02__AddGitHubCopilotAndExplicitIChatClientResolvesGitHubCopilotChatClient` | `AddGitHubCopilot` 後の明示的 `IChatClient` 登録で `GitHubCopilotChatClient` が解決されることを確認した。 |
| T-1-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilotProvider_RegistersModelCatalog` | `AddGitHubCopilotProvider` 単体で `ICopilotModelCatalog` が登録されることを確認した。 |
| T-1-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilotProvider_DefaultWrapperFailsFastOnListModels` | SDK wrapper 未登録時の fail-fast 動作を `ListModelsAsync` で確認した。 |
| T-1-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilotProvider_DefaultWrapperFailsFastOnSend` | SDK wrapper 未登録時の fail-fast 動作を `SendAsync` で確認した。 |
| T-1-06 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilotProvider_Only_ChatClientWrapsFailFastAsRuntimeException` | fail-fast 例外が chat client で `CopilotRuntimeException` に変換されることを確認した。 |
| T-1-07 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.UT_IT_T_1_07__TwoStepRegistrationWorksCorrectly` | `AddGitHubCopilotProvider` + `AddGitHubCopilotSdkWrapper` の 2 段階登録で本物の wrapper が使われることを確認した。 |
| T-1-08 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.UT_IT_T_1_08__CustomSdkWrapperIsUsedWhenRegisteredAfterProvider` | provider 登録後の後勝ち登録でカスタム wrapper が優先されることを確認した。 |
| T-1-09 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilot_ThrowsOnNullConfiguration` | `null` 構成を拒否するガードを確認した。 |
| T-2-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_01__SingleAttachmentPropagatesPathAndDisplayName` | 単一 attachment の `Path` / `DisplayName` が `CopilotSessionConfig` に伝播することを確認した。 |
| T-2-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_02__AttachmentWithNullDisplayNamePropagates` | `DisplayName=null` がそのまま伝播することを確認した。 |
| T-2-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_03__ThreeAttachmentsPropagateInOrder` | 3 件の attachment の順序保持を確認した。 |
| T-2-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_04__NullAttachmentsCompletesNormally` | `Attachments=null` でも正常終了することを確認した。 |
| T-2-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_05__EmptyAttachmentsListCompletesNormally` | 空 attachment 一覧を許容することを確認した。 |
| T-2-06 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` | OpenAI が Copilot 専用 attachment を拒否することを確認した。 |
| T-2-07 | Automated | `MeAiUtility.MultiProvider.AzureOpenAI.Tests.AzureOpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` | AzureOpenAI が Copilot 専用 attachment を拒否することを確認した。 |
| T-2-08 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAICompatibleProviderTests.RejectsCopilotOnlyExecutionOption` | OpenAICompatible が Copilot 専用 attachment を拒否することを確認した。 |
| T-2-09 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_RejectsAttachmentWithInvalidPath` | 空文字と相対パスを `InvalidRequestException` で拒否することを確認した。 |
| T-2-10 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_2_10__NullAttachmentPathIsRejected` | `Attachment.Path=null` を `path` を含む `InvalidRequestException` で拒否することを確認した。 |
| T-3-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_PropagatesAttachmentsSkillDirectoriesDisabledSkillsAndTimeout` | typed `SkillDirectories` の伝播を確認した。 |
| T-3-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_PropagatesAttachmentsSkillDirectoriesDisabledSkillsAndTimeout` | typed `DisabledSkills` の伝播を確認した。 |
| T-3-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_UsesAdvancedOptionsFallback_WhenTypedSkillPropertiesAreNotSpecified` | typed 未指定時に `copilot.skillDirectories` が fallback として使われることを確認した。 |
| T-3-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_UsesAdvancedOptionsFallback_WhenTypedSkillPropertiesAreNotSpecified` | typed 未指定時に `copilot.disabledSkills` が fallback として使われることを確認した。 |
| T-3-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_PrefersTypedSkillPropertiesOverAdvancedOptions` | typed skill 設定が AdvancedOptions より優先されることを確認した。 |
| T-3-06 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_3_06__TypedDisabledSkillsPreferredOverAdvancedOptions` | typed `DisabledSkills` が AdvancedOptions より優先されることを確認した。 |
| T-3-07 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_3_07__AdvancedOptionsOnlyStillWorksWhenTypedPropertiesAreNull` | typed 値が null の場合のみ AdvancedOptions の skill 設定が有効になることを確認した。 |
| T-3-08 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_3_08__AllPropertiesNullCompletesNormally` | skill 関連プロパティがすべて null でも正常終了することを確認した。 |
| T-3-09 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_3_09__EmptySkillDirectoriesListCompletesNormally` | 空の `SkillDirectories` を許容することを確認した。 |
| T-3-10 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_3_10__MultipleSkillDirectoriesPropagateInOrder` | 複数 `SkillDirectories` の順序保持を確認した。 |
| T-3-11 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` | OpenAI が Copilot 専用 `SkillDirectories` を拒否することを確認した。 |
| T-4-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.CopilotClientHostTests.ListModelsAsync_ThrowsRuntimeExceptionOnFailure` | `ListModelsAsync` 失敗時に `Operation=ListModels` が設定されることを確認した。 |
| T-4-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_WrapsSendFailureWithSendOperation` | `Send` 失敗時に `Operation=Send` が設定されることを確認した。 |
| T-4-03 | Automated | `MeAiUtility.MultiProvider.Tests.Exceptions.MultiProviderExceptionTests.UT_IT_T_4_03__CopilotRuntimeException_ClientInitializationOperationIsSetCorrectly` | `CopilotRuntimeException` が `ClientInitialization` を保持することを確認した。 |
| T-4-04 | Automated | `MeAiUtility.MultiProvider.Tests.Exceptions.MultiProviderExceptionTests.UT_IT_T_4_04__CopilotRuntimeException_OperationCliPathAndExitCodeAllPreserved` | `Operation` / `CliPath` / `ExitCode` / `InnerException` の同時保持を確認した。 |
| T-4-05 | Automated | `MeAiUtility.MultiProvider.Tests.Exceptions.MultiProviderExceptionTests.CopilotRuntimeException_AllowsNullOperation` | `Operation=null` の後方互換動作を確認した。 |
| T-4-06 | Automated | `MeAiUtility.MultiProvider.Tests.Exceptions.MultiProviderExceptionTests.UT_IT_T_4_06__ExistingCatchCodeIsNotAffectedByOperation` | 既存の catch パターンが `Operation` 追加後も壊れないことを確認した。 |
| T-4-07 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_4_07__InnerExceptionIsPreservedAndOperationIsListModels` | `ListModels` 失敗時の `InnerException` 保持と `Operation=ListModels` を確認した。 |
| T-5-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_PropagatesAttachmentsSkillDirectoriesDisabledSkillsAndTimeout` | request timeout が chat client から送信設定に伝播することを確認した。 |
| T-5-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.SendAsync_UsesProviderTimeoutWhenRequestTimeoutIsNotSpecified` | request timeout 未指定時の provider fallback を wrapper で確認した。 |
| T-5-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_RejectsInvalidTimeoutSeconds` | `TimeoutSeconds=0` を拒否することを確認した。 |
| T-5-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_5_04__NegativeTimeoutSecondsIsRejected` | 負の timeout を拒否することを確認した。 |
| T-5-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_5_05__MinimalPositiveTimeoutSecondsIsAccepted` | 最小正値 `1` を許容することを確認した。 |
| T-5-06 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_5_06__VeryLargeTimeoutSecondsIsAccepted` | `int.MaxValue` を許容することを確認した。 |
| T-5-07 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_5_07__NullTimeoutFallsBackToProviderDefault` | request timeout 未指定でも正常終了し、fallback の詳細は T-5-02 で補完されることを確認した。 |
| T-6-01 | RecordedButSkipped | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.UT_IT_T_6_01__CliNotFoundErrorMessageContainsOsInfo` | `BuildCliDiagnostics` が private で、確認には実 SDK 依存の CLI 未検出シナリオが必要なため `[Explicit]` で記録した。 |
| T-6-02 | RecordedButSkipped | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.UT_IT_T_6_02__CliNotFoundErrorMessageContainsPathInfo` | `BuildCliDiagnostics` が private で、確認には実 SDK 依存の PATH 診断が必要なため `[Explicit]` で記録した。 |
| T-6-03 | RecordedButSkipped | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.UT_IT_T_6_03__CliNotFoundErrorMessageContainsCandidatePaths` | `BuildCliDiagnostics` が private で、確認には実 SDK 依存の候補パス診断が必要なため `[Explicit]` で記録した。 |
| T-6-04 | RecordedButSkipped | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.UT_IT_T_6_04__ExplicitCliPathSkipsDiscovery` | CLI 探索スキップの確認が SDK 内部動作依存のため `[Explicit]` で記録した。 |
| T-C-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_01__AttachmentsAndSkillDirectoriesBothPropagate` | Attachments と `SkillDirectories` の同時伝播を確認した。 |
| T-C-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_02__AttachmentsAndDisabledSkillsBothPropagate` | Attachments と `DisabledSkills` の同時伝播を確認した。 |
| T-C-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.GetResponseAsync_PropagatesAttachmentsSkillDirectoriesDisabledSkillsAndTimeout` | Attachments / skill 設定 / timeout を同時指定した通常系を確認した。 |
| T-C-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotSdkWrapperTests.SendAsync_MapsAttachmentsAndRequestTimeoutOverride` | wrapper が Attachments と timeout override を invocation に写像することを確認した。 |
| T-C-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_05__SkillDirectoriesAndTimeoutBothPropagate` | `SkillDirectories` と timeout の同時伝播を確認した。 |
| T-C-06 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_06__TimeoutSpecifiedAndSendFailureHasSendOperation` | timeout 指定付き送信失敗が `Operation=Send` になることを確認した。 |
| T-C-07 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.AddGitHubCopilotProvider_Only_ChatClientWrapsFailFastAsRuntimeException` | fail-fast wrapper と phase 情報の組み合わせを確認した。 |
| T-C-08 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.UT_IT_T_C_08__AddGitHubCopilotWithMockWrapperAndAttachmentsSkillDirectories` | DI で差し替えた mock wrapper まで Attachments と `SkillDirectories` が到達することを確認した。 |
| T-C-09 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_09__AttachmentsWithSendFailureHasSendOperation` | attachment 付き送信失敗が `Operation=Send` になることを確認した。 |
| T-C-10 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_C_10__TypedSkillDirectoriesWithAdvancedOptionsDisabledSkillsMix` | typed `SkillDirectories` と AdvancedOptions `DisabledSkills` の混在を確認した。 |
| T-P-01 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.AzureOpenAI.Tests.AzureOpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAICompatibleProviderTests.RejectsCopilotOnlyExecutionOption` | OpenAI 系 3 実装で attachment を一貫して拒否することを確認した。 |
| T-P-02 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.AzureOpenAI.Tests.AzureOpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAICompatibleProviderTests.RejectsCopilotOnlyExecutionOption` | OpenAI 系 3 実装で `SkillDirectories` を一貫して拒否することを確認した。 |
| T-P-02a | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.AzureOpenAI.Tests.AzureOpenAIChatClientAdapterTests.GetResponseAsync_RejectsCopilotOnlyExecutionOption` / `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAICompatibleProviderTests.RejectsCopilotOnlyExecutionOption` | OpenAI 系 3 実装で `DisabledSkills` を一貫して拒否することを確認した。 |
| T-P-03 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.UT_IT_T_P_03__OpenAITimeoutSecondsIsIgnoredAndNoException` | OpenAI で `TimeoutSeconds` が無視され、例外にならないことを確認した。 |
| T-P-04 | Automated | `MeAiUtility.MultiProvider.OpenAI.Tests.OpenAIChatClientAdapterTests.UT_IT_T_P_04__OpenAIExceptionIsNotCopilotRuntimeException` | OpenAI の `InvalidRequestException` が `CopilotRuntimeException` ではないことを確認した。 |
| T-L-01 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.ConfigurationTests.GitHubCopilotServiceExtensionsTests.UT_IT_T_L_01__GitHubCopilotChatClientIsSingleton` | `GitHubCopilotChatClient` が singleton 登録されることを確認した。 |
| T-L-02 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_L_02__AttachmentsDoNotLeakAcrossConsecutiveCalls` | 連続呼び出しで attachment がリークしないことを確認した。 |
| T-L-03 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_L_03__SkillDirectoriesDoNotLeakAcrossConsecutiveCalls` | 連続呼び出しで `SkillDirectories` がリークしないことを確認した。 |
| T-L-04 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_L_04__TimeoutSecondsDoNotLeakAcrossConsecutiveCalls` | 連続呼び出しで request timeout がリークしないことを確認した。 |
| T-L-05 | Automated | `MeAiUtility.MultiProvider.GitHubCopilot.Tests.GitHubCopilotChatClientTests.UT_IT_T_L_05__PhaseIsIndependentPerCall` | 失敗 / 成功 / 失敗の連続呼び出しで phase 情報が各回独立であることを確認した。 |

## 補足

- `RecordedButSkipped` は T-6-01, T-6-02, T-6-03, T-6-04 のみである。
- `ManualOnly` はなし。
- `NotImplementedOrMismatch` はなし。

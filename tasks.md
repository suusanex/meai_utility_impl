# tasks

- [X] Ensure console input/output encoding set to UTF-8 to avoid Japanese mojibake in sample app.
- [X] Build and run tests (if any) to validate change.
- [X] Promote the GitHub Copilot CLI wrapper into the main GitHubCopilot library and add a public DI hook.
- [X] Add an opt-in real GitHub Copilot E2E test via the common IChatClient interface with the model fixed to "GPT-5 mini".
- [X] Validate the updated unit/integration tests, confirm the opt-in gate behavior, and verify the real Copilot E2E path once on net8.0.
- [X] Expose a public GitHub Copilot model catalog interface backed by the real CLI model list.
- [X] Reject invalid GitHub Copilot CLI model IDs before SendAsync and cover it with unit/integration/E2E tests.
- [X] GitHub Copilot CLI ラッパーの reasoning 対応可否を実装とテストで整合させる。

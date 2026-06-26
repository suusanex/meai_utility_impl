# トラブルシューティング

## `gh` が見つからない

GitHub CLIをインストールし、PATHに追加する。

確認:

```powershell
gh --version
```

## 認証エラーになる

対象リポジトリを読めるアカウントで認証する。

確認:

```powershell
gh auth status
```

## PR情報を取得できない

`--repo owner/name` と `--pr number` が正しいことを確認する。private repository の場合は、認証アカウントに読み取り権限が必要。

## checks取得で失敗する

`--include-checks` は `gh pr checks` を実行する。対象PRにチェックが存在しない場合や、GitHub CLIのバージョンが対応していない場合は失敗することがある。

チェック取得が不要な場合は `--include-checks` を外して再実行する。

## GitHub Copilotレビューが見つからない

このパッケージはGitHub CLIで取得できるPRレビュー、PRコメント、レビューコメントを対象にする。GitHub CopilotレビューがGitHub上にまだ投稿されていない場合、収集結果には含まれない。

Copilot自動レビューが有効な場合は、レビュー完了を待ってから再実行する。自動レビューが無効な場合は、ユーザーが事前にCopilotレビューをリクエストする。このMVPでは、スクリプトから `@copilot` へのレビューリクエストは行わない。

再実行しても見つからない場合は、`review-plan.md` に「GitHub Copilotレビュー: 未取得」と記録し、ローカルCodexレビューのみで進めるか、人間判断へ戻すかを明記する。

## APM導入後にskill assetが見つからない

scratch rootへ導入して、skill配下の相対パスを確認する。

```powershell
apm install --update --target codex suusanex/codex_copilot_pr_review_agent --root <scratch>
```

導入済みリポジトリで再実行する場合、`apm.yml` にこのパッケージが登録済みなら、インストーラはpackage引数なしで既存依存を更新・再展開する。

```powershell
apm install --update --target codex --root <scratch>
```

ローカルのパッケージリポジトリにあるインストーラを使って導入済みリポジトリを更新する場合は、APMの自己依存解決を避けるため、既存の `apm.yml` は維持したままローカル原本からCodex設定とskill assetを同期する。

確認対象:

- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/SKILL.md`
- `<scratch>/.codex/agents/local-reviewer.toml`
- `<scratch>/.codex/agents/review-planner.toml`
- `<scratch>/.codex/agents/spark-implementer.toml`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/scripts/collect-pr-review-context.cs`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/templates/review-plan.md`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/templates/review-result-report.md`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/references/usage.md`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/references/design.md`
- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/references/troubleshooting.md`

ファイルが不足している場合は、APMパッケージの `apm pack --dry-run -v` でskill配下の `scripts/`、`templates/`、`references/` がpack対象に含まれるか確認する。

agentが存在してもモデル指定がない場合は、各 `.agent.md` のfront matterに次の `model` が存在するか確認する。`.codex/agents/*.toml` はインストーラで生成されるため、`model_reasoning_effort` と `sandbox_mode` はインストーラ実行後の `.codex/agents/*.toml` で確認する。

- `local-reviewer`: front matter `model: gpt-5.5`
- `review-planner`: front matter `model: gpt-5.5`
- `spark-implementer`: front matter `model: gpt-5.3-codex-spark`

インストーラ実行後の `.codex/agents/spark-implementer.toml` では、`model_reasoning_effort = "high"` が入ることも確認する。

## commit/pushしないで止まる

次のいずれかを確認する。

- 対象リポジトリのルールで人手承認が必要。
- 関連テストが失敗している。
- 未コミット変更に計画外の差分が含まれる。
- `review-plan.md` の範囲外の判断が必要。

この場合は、結果レポートの「人手で必要な作業」を確認する。

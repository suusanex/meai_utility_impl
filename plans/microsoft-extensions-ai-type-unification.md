# Microsoft.Extensions.AI 型供給単一化 Plan

## Background / Goal

利用者フィードバックとして、公式 Microsoft.Extensions.AI の `ChatOptions.ResponseFormat` をアプリ側で直接使いたくても、現行の MeAiUtility 配布物では安全に完了できない、という指摘を受けた。

レビュー結果として、この指摘は改善対象と判断する。根拠は以下の通り。

- [src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs](src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs) が `namespace Microsoft.Extensions.AI` で `ChatOptions`、`ChatMessage`、`ChatResponse`、`IChatClient` などを独自定義している
- 一方で [src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs) と [src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs) は `extern alias OfficialMeAi` を使って公式 Microsoft.Extensions.AI 型へ変換している
- [specs/002-meai-multi-provider/data-model.md](specs/002-meai-multi-provider/data-model.md#L283) には `ChatMessage` と `ChatOptions` は Microsoft.Extensions.AI.Abstractions の型をそのまま使用すると明記されており、実装と設計が不一致になっている
- [README.md](README.md) は DLL 配布利用とアプリ側依存追加の両方を案内しており、現状の独自型定義と組み合わさると、利用者が公式 `ChatOptions.ResponseFormat` を使うために package を追加した時点で型重複・曖昧参照・キャスト不能の温床になる

本 Plan のゴールは、MeAiUtility の公開面で Microsoft.Extensions.AI 型の供給元を公式 `Microsoft.Extensions.AI.Abstractions` に一本化し、利用者が `ChatOptions.ResponseFormat` を含む公式型を直接使える状態へ移行すること。

## Non-goals

- GitHub Copilot 独自機能や `ConversationExecutionOptions` の別件拡張をこの Plan へ混ぜ込むこと
- OpenAI / Azure OpenAI / OpenAICompatible / GitHub Copilot の機能仕様を全面的に再設計すること
- 既存の全 README セクションを全面改稿すること。今回必要な範囲の依存関係・移行手順・制約説明に限定する
- 実装フェーズ内で新しい配布方式を複数追加すること。サポートする参照形態は明示するが、配布チャネルそのものの増設は対象外
- 旧版バイナリとの完全なバイナリ互換維持。今回の変更は公開型の実体アセンブリが変わるため、再ビルド前提の移行を許容する

## Current state summary

### 1. 公開 API が公式型ではなく独自複製型を供給している

- [src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj](src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj) は `Microsoft.Extensions.AI.Abstractions` を参照していない
- 代わりに [src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs](src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs) が公式と同名の型を定義している
- そのため利用者コードから見える `Microsoft.Extensions.AI.ChatOptions` は、MeAiUtility 側の独自実装と公式 package 実装が競合しうる

### 2. OpenAI / AzureOpenAI 実装が二重の型体系をブリッジしている

- [src/MeAiUtility.MultiProvider.OpenAI/MeAiUtility.MultiProvider.OpenAI.csproj](src/MeAiUtility.MultiProvider.OpenAI/MeAiUtility.MultiProvider.OpenAI.csproj) と [src/MeAiUtility.MultiProvider.AzureOpenAI/MeAiUtility.MultiProvider.AzureOpenAI.csproj](src/MeAiUtility.MultiProvider.AzureOpenAI/MeAiUtility.MultiProvider.AzureOpenAI.csproj) は `Microsoft.Extensions.AI.Abstractions` を alias 付きで直接参照している
- [src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs) と [src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs) は、独自 `ChatOptions` / `ChatMessage` から公式 `ChatOptions` / `ChatMessage` へ変換している
- この構造自体が「公開型と内部実装型の二重管理」を示しており、`ResponseFormat` のような公式側だけが持つプロパティを公開面へ自然に流せない

### 2a. OpenAICompatible の stub 残存は既知仕様ではなくバグ扱いにする必要がある

- 指定コミット `9f408037eedb894415cdc549ba6fb1e2436e02c8` は OpenAI / AzureOpenAI を official SDK ベースの正式実装へ寄せている
- 一方、現行の [src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs) は hard-coded response を返す stub 実装のまま残っている
- 利用者指摘の観点では、これを「OpenAICompatible は未対応」として片付けるのは誤りであり、OpenAICompatible も OpenAI 系 provider として正式実装へ揃えるバグ修正が必要である

### 3. 設計仕様と実装が不整合

- [specs/002-meai-multi-provider/spec.md](specs/002-meai-multi-provider/spec.md#L110) の FR-001 は Microsoft.Extensions.AI 準拠の公開 I/F を要求している
- [specs/002-meai-multi-provider/plan.md](specs/002-meai-multi-provider/plan.md) と [specs/002-meai-multi-provider/data-model.md](specs/002-meai-multi-provider/data-model.md#L283) も公式型をそのまま使う前提で記述されている
- 現行実装は「準拠風」ではあるが、型供給の実体が異なるため利用者にとっては準拠が破れている

### 4. 配布形態の契約が曖昧

- [README.md](README.md) は DLL 配布利用時にアプリ側で複数 package を追加する例を示している
- しかし Microsoft.Extensions.AI 型の唯一の供給元をどれにするかが明文化されていない
- 結果として、DLL 直参照と package 参照を混在させた利用者が型重複問題へ遭遇しやすい

## Proposed design / architecture delta

### D-01: 公開型の唯一の供給元を公式 Microsoft.Extensions.AI.Abstractions に固定する

コア方針として、MeAiUtility は Microsoft.Extensions.AI の公開型を「再定義」せず、「公式 assembly を参照するだけ」に切り替える。

実施内容:

- [src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj](src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj) に `Microsoft.Extensions.AI.Abstractions` の直接参照を追加する
- 公式 MEAI バージョンを `Directory.Build.props` などの共通プロパティへ寄せ、コア・OpenAI・AzureOpenAI・GitHubCopilot・Samples で同一 version を使う
- [src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs](src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs) を削除する

期待効果:

- 利用者が参照する `ChatOptions`、`ChatMessage`、`ChatResponse`、`IChatClient` は常に公式 assembly の型になる
- `ChatOptions.ResponseFormat` を含む公式 API が、ライブラリ境界で別型へ潰されない

### D-02: provider 実装から「独自型→公式型」ブリッジを除去または縮退する

公開型が公式型へ統一された後は、OpenAI / AzureOpenAI 実装が抱える alias 変換レイヤーを最小化する。

実施内容:

- [src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs) と [src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs) の責務を見直す
- `ChatMessage` / `ChatResponse` / `ChatOptions` の相互変換が不要になる箇所は除去する
- 変換が残る場合も、対象は `ConversationExecutionOptions` の正規化や provider 固有設定補完に限定し、型体系の橋渡しはやめる
- `extern alias OfficialMeAi` 依存をなくせるプロジェクトはなくし、必要最小限の参照構成に整理する

期待効果:

- `ResponseFormat` や今後公式型に追加されるプロパティが、独自ブリッジで落ちるリスクを減らせる
- provider 実装の保守コストと設計不一致を同時に減らせる

### D-03: ResponseFormat の扱いを provider ごとに明示する

今回の主目的は型単一化だが、型が単一化された後に `ResponseFormat` をどう扱うかを曖昧にすると、別のサイレント不具合を生む。そこで provider 別に以下を明示する。

- OpenAI: 公式 `ChatOptions.ResponseFormat` をそのまま利用可能とし、既存の request 正規化で落とさない
- AzureOpenAI: OpenAI と同様に、公式 `ChatOptions.ResponseFormat` をそのまま流せる設計とする
- OpenAICompatible: 現状の stub 残存はバグとして扱い、OpenAI 系の正式実装へ揃える。`ResponseFormat` は OpenAICompatible でも pass-through され、少なくとも型単一化によって失われないことを保証する
- GitHubCopilot: SDK 側に同等概念がない前提で、`ResponseFormat` 指定時は型競合ではなく明示的な `NotSupportedException` で fail-fast させる

この Plan では「型競合で使えない」状態を解消することが主眼であり、OpenAI 系 provider では `ResponseFormat` を利用可能にし、未対応 provider では silent ignore を行わず、未対応であることを明示する。

### D-03a: OpenAICompatible を OpenAI 系の正式実装へ揃える

型単一化と `ResponseFormat` 利用可能化を成立させるには、OpenAICompatible の stub 残存を別問題として切り離さず、同じ変更セットで正式実装へ引き上げる必要がある。

実施内容:

- [src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs) の hard-coded response を廃止する
- OpenAICompatible を OpenAI official bridge と同系統の request path に載せ、BaseUrl 差し替えと互換性検証だけを差分として扱う
- `ResponseFormat`、`Temperature`、`MaxOutputTokens`、`StopSequences` など OpenAI 系共通オプションが OpenAICompatible でも落ちないようにする
- 互換 endpoint 側が受け付けない場合は provider / endpoint 由来の明示エラーを返し、stub 応答や silent fallback は行わない

期待効果:

- OpenAICompatible だけが型単一化後も機能的に取り残される状態を防げる
- 利用者は OpenAI / AzureOpenAI / OpenAICompatible の 3 系統で `ResponseFormat` を含む共通 OpenAI 系オプションを同じ形で利用できる

### D-04: 配布契約を「単一の型供給」に合わせて再定義する

実装変更だけでは、DLL 配布利用者がどう参照を揃えるべきか伝わらない。README とリリースノートでサポートする参照契約を明示する。

最小契約:

- MeAiUtility の公開 Microsoft.Extensions.AI 型は公式 `Microsoft.Extensions.AI.Abstractions` が唯一の供給元である
- DLL 直参照利用者は、README で指定する同一 version の `Microsoft.Extensions.AI.Abstractions` をアプリ側 `PackageReference` として追加する
- 同一アプリ内で「旧版 MeAiUtility が再定義した Microsoft.Extensions.AI 型」と「新版公式型」を混在させない
- README に、DLL 配布利用・ProjectReference 利用それぞれの推奨参照パターンを整理して記載する

この Plan では配布方式自体は増やさず、現行 README の運用に合わせて「MeAiUtility DLL は配布物から参照し、Microsoft.Extensions.AI.Abstractions は利用側 package で揃える」方針に固定する。

release artifact には依存 package と exact version の一覧を必ず含める。

### D-05: 参照形態検証のための fixture を追加する

既存の test 群には provider 契約テストはあるが、今回の変更で重要になる「公開型の唯一性」「DLL 直参照 + 利用側 package 追加」の検証受け皿はない。したがって実装時に以下を追加する。

- solution build とは別に、利用者アプリ相当の compile-time smoke test fixture を追加する
- fixture は少なくとも `ProjectReference` 形態と「配布 DLL 参照 + `Microsoft.Extensions.AI.Abstractions` package 追加」形態の 2 パターンを持つ
- fixture では `ChatOptions.ResponseFormat` を直接設定したサンプルコードをビルドし、必要なら 1 リクエスト実行する
- 失敗時は `CS0433`、`TypeLoadException`、`InvalidCastException` のいずれで壊れたかを即座に識別できるようにする

### D-06: 破壊的変更として移行を明示する

今回の変更はソース互換を改善するが、旧版に対してコンパイル済みの利用者バイナリとは binary compatible ではない可能性が高い。したがって、リリース戦略は「無音の差し替え」ではなく「移行を明示する更新」とする。

実施内容:

- 互換性評価結果に応じて major version 更新、または少なくとも breaking change 明記付きリリースにする
- README と release note に以下を記載する
- 旧独自型が削除され、公式 Microsoft.Extensions.AI 型へ移行したこと
- 利用者はアプリ側を再ビルドする必要があること
- `ChatOptions.ResponseFormat` を含む公式 API を直接使えること
- 旧 workaround や alias 回避コードが不要になること

## Coarse interaction scenarios

### S-01: OpenAI で公式 ChatOptions.ResponseFormat を直接使う

1. 利用側アプリは公式 `Microsoft.Extensions.AI.ChatOptions` を生成し、`ResponseFormat` を設定する
2. `IChatClient.GetResponseAsync` に公式 `ChatMessage` 群と公式 `ChatOptions` を渡す
3. MeAiUtility の公開 API はそのまま公式型を受け取る
4. OpenAI adapter は `ConversationExecutionOptions` だけを追加解釈し、`ResponseFormat` を保持したまま official client へ渡す
5. 応答は公式 `ChatResponse` としてアプリへ返る

観測点:

- アプリ側で型曖昧さや alias 追加が不要であること
- `ResponseFormat` がライブラリ境界で失われないこと

### S-02: AzureOpenAI でも同じ利用コードで動作する

1. 利用側コードは S-01 と同じく公式 `ChatOptions` を使う
2. 設定だけ AzureOpenAI へ切り替える
3. MeAiUtility は同一公開型のまま AzureOpenAI adapter へ処理を委譲する
4. AzureOpenAI adapter は公式型を維持したまま official client に接続する
5. 応答は同じ `ChatResponse` 型で返る

観測点:

- provider 切替時に利用側コード変更が不要であること
- OpenAI と AzureOpenAI の両方で `ResponseFormat` 指定が型競合を起こさないこと

### S-03: DLL 配布 + 利用側 package 追加でも型競合が起きない

1. 利用側は MeAiUtility の DLL 群を参照しつつ、README で指定された公式 `Microsoft.Extensions.AI.Abstractions` version を追加する
2. アプリをビルドする
3. 実行時に DI から `IChatClient` を解決して 1 リクエスト送る
4. コンパイル、起動、実行の各段階で `CS0433`、`TypeLoadException`、`InvalidCastException` が起きない

観測点:

- 参照契約が release artifact と README の両方で一致していること
- 公式型が唯一の供給元として機能していること

### S-04: OpenAICompatible でも ResponseFormat を含む OpenAI 系オプションを利用できる

1. 利用側は公式 `ChatOptions.ResponseFormat` を設定して OpenAICompatible を呼ぶ
2. 公開 API は型競合なく受け取る
3. OpenAICompatible provider は OpenAI 系の正式実装として request を互換 endpoint へ送る
4. `ResponseFormat` を含む OpenAI 系オプションが保持されたまま処理される
5. endpoint 非対応時のみ、provider / endpoint 制約に基づく明示エラーを返す

観測点:

- OpenAICompatible でも `ResponseFormat` が型単一化の過程で失われないこと
- 失敗する場合も型重複ではなく、endpoint 制約や provider 実装上の明示エラーになること

### S-05: GitHubCopilot で ResponseFormat が未対応でも型由来の失敗にしない

1. 利用側は公式 `ChatOptions.ResponseFormat` を設定して GitHubCopilot を呼ぶ
2. 公開 API は型競合なく受け取る
3. GitHubCopilot adapter は SDK 対応状況に応じて事前検証する
4. 未対応なら provider 能力に基づく明示的な例外を返す

観測点:

- 失敗理由が「型重複」ではなく「provider 非対応」になること
- エラーが fail-fast で、README / capability 情報と整合すること

### S-06: 旧版からの移行不足は早期に検知する

1. 利用側が旧版前提の参照やコードを一部残したまま新版へ更新する
2. ビルドまたは起動時に不整合が検知される
3. 利用者は README / release note の移行手順に従い参照を揃える

観測点:

- 遅延実行時の不定なキャスト不能より前に異常を検知できること
- 移行すべき差分が文書で追えること

## Impacted code / files / modules

### コア公開面

- [src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj](src/MeAiUtility.MultiProvider/MeAiUtility.MultiProvider.csproj)
- [src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs](src/MeAiUtility.MultiProvider/Abstractions/ChatAbstractions.cs)
- [src/MeAiUtility.MultiProvider/Abstractions/IProviderFactory.cs](src/MeAiUtility.MultiProvider/Abstractions/IProviderFactory.cs)
- [src/MeAiUtility.MultiProvider/Configuration/ProviderConfigurationExtensions.cs](src/MeAiUtility.MultiProvider/Configuration/ProviderConfigurationExtensions.cs)
- [src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs](src/MeAiUtility.MultiProvider/Configuration/ProviderFactory.cs)
- [src/MeAiUtility.MultiProvider/Configuration/ProviderRegistry.cs](src/MeAiUtility.MultiProvider/Configuration/ProviderRegistry.cs)
- [src/MeAiUtility.MultiProvider/Options/ConversationExecutionOptions.cs](src/MeAiUtility.MultiProvider/Options/ConversationExecutionOptions.cs)

### provider 実装

- [src/MeAiUtility.MultiProvider.OpenAI/MeAiUtility.MultiProvider.OpenAI.csproj](src/MeAiUtility.MultiProvider.OpenAI/MeAiUtility.MultiProvider.OpenAI.csproj)
- [src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAIChatClientAdapter.cs)
- [src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAIOfficialBridge.cs)
- [src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleEmbeddingAdapter.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleEmbeddingAdapter.cs)
- [src/MeAiUtility.MultiProvider.AzureOpenAI/MeAiUtility.MultiProvider.AzureOpenAI.csproj](src/MeAiUtility.MultiProvider.AzureOpenAI/MeAiUtility.MultiProvider.AzureOpenAI.csproj)
- [src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs](src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIChatClientAdapter.cs)
- [src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs](src/MeAiUtility.MultiProvider.AzureOpenAI/AzureOpenAIOfficialBridge.cs)
- [src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs](src/MeAiUtility.MultiProvider.OpenAI/OpenAICompatibleProvider.cs)
- [src/MeAiUtility.MultiProvider.GitHubCopilot/MeAiUtility.MultiProvider.GitHubCopilot.csproj](src/MeAiUtility.MultiProvider.GitHubCopilot/MeAiUtility.MultiProvider.GitHubCopilot.csproj)
- [src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs](src/MeAiUtility.MultiProvider.GitHubCopilot/GitHubCopilotChatClient.cs)

### サンプル・ドキュメント

- [src/MeAiUtility.MultiProvider.Samples/MeAiUtility.MultiProvider.Samples.csproj](src/MeAiUtility.MultiProvider.Samples/MeAiUtility.MultiProvider.Samples.csproj)
- サンプルコード一式
- [README.md](README.md)
- release note / 配布物説明

### テスト

- `tests/MeAiUtility.MultiProvider.Tests/` の公開 API / オプション関連テスト
- `tests/MeAiUtility.MultiProvider.OpenAI.Tests/` の OpenAI adapter テスト
- `tests/MeAiUtility.MultiProvider.AzureOpenAI.Tests/` の AzureOpenAI adapter テスト
- `tests/MeAiUtility.MultiProvider.GitHubCopilot.Tests/` の capability / validation テスト
- `tests/MeAiUtility.MultiProvider.IntegrationTests/` の provider 切替・契約テスト
- 新規 compile-time smoke test fixture または参照形態検証用テストプロジェクト

## Verification design

実装後の検証は「型の唯一性」「ResponseFormat の直接利用」「配布形態ごとの安全性」「既存フロー回帰なし」を中心に行う。

### 自動テスト観点

| ID | カテゴリ | 観点 | 条件 | 期待結果 |
|---|---|---|---|---|
| TP-01 | 正常系 | OpenAI で公式 `ChatOptions.ResponseFormat` を利用側が直接設定できる | 利用側相当コードで公式 `ChatOptions` に `ResponseFormat` を設定して OpenAI プロバイダーを呼ぶ | ビルドと実行が成功し、型変換コードなしで応答を取得できる |
| TP-02 | 正常系 | AzureOpenAI で公式 `ChatOptions.ResponseFormat` を利用側が直接設定できる | 公式 `ChatOptions.ResponseFormat` を設定して AzureOpenAI プロバイダーを呼ぶ | 型競合なく応答を取得できる |
| TP-03 | 正常系 | 同一利用コードで provider 切替できる | 公式 `IChatClient` / `ChatOptions` / `ChatMessage` を使った同一コードで OpenAI、AzureOpenAI、GitHubCopilot を切り替える | 利用側の型差し替えが不要 |
| TP-04 | 正常系 | 利用側コンパイルで型重複が解消されている | 公式型のみを using してアプリ相当コードをビルドする | 曖昧参照や alias 回避が不要 |
| TP-05 | 配布/参照形態 | DLL 配布 + 利用側 package 追加でも型競合しない | README 想定構成でビルドし 1 リクエスト実行する | `CS0433`、`TypeLoadException`、`InvalidCastException` が発生しない |
| TP-06 | 配布/参照形態 | ProjectReference / package 参照でも同じ公開型になる | DLL 直参照以外の参照形態でも同一 API を使う | 参照形態で公開型が変わらない |
| TP-07 | 正常系 | OpenAICompatible でも `ResponseFormat` を利用側が直接設定できる | 公式 `ChatOptions.ResponseFormat` を設定して OpenAICompatible を呼ぶ | 型競合なく request に反映され、互換 endpoint が受け付ける構成では正常応答を取得できる |
| TP-08 | 異常系 | OpenAICompatible で endpoint 非対応時は明示エラーになる | `ResponseFormat` を設定して OpenAICompatible を呼び、互換 endpoint が非対応の応答を返す | stub 応答や silent fallback ではなく、provider / endpoint 由来の明示エラーで失敗する |
| TP-09 | 異常系 | GitHubCopilot で `ResponseFormat` 未対応時も型由来の失敗にしない | `ResponseFormat` を設定して GitHubCopilot を呼ぶ | provider 能力に基づく明示例外で失敗し、型競合では失敗しない |
| TP-10 | 異常系 | 旧参照混在時の失敗が明確 | 旧版前提コードや参照を一部残した状態で更新版へ差し替える | ビルド時または起動時に移行不足を追える明確なエラーになる |
| TP-11 | 後方互換 | `ResponseFormat` を使わない既存利用に回帰がない | 既存相当の基本チャット呼び出しを各 provider で実行する | 成功系の既存挙動を維持する |
| TP-12 | 後方互換 | 既存共通パラメータ利用に回帰がない | temperature、max tokens など既存共通パラメータを設定して OpenAI / AzureOpenAI / OpenAICompatible を呼ぶ | 型単一化後も従来どおり動作する |
| TP-13 | 移行/破壊的変更 | 移行手順が利用者に十分提示される | README / release note の更新内容を確認する | 削除型、必要参照、再ビルド要否、更新例が明記されている |
| TP-14 | 連続実行 | `ResponseFormat` 有無と provider 切替を連続実行しても混線しない | OpenAI、AzureOpenAI、OpenAICompatible、GitHubCopilot、再度 OpenAI を短時間で連続実行する | 各回の設定が独立して扱われる |
| TP-15 | 負荷 | 多数実行や並列でも型識別が不安定にならない | モック依存で複数 provider 呼び出しを多数回または並列に実行する | 断続的な `TypeLoadException` や `InvalidCastException` が出ない |
| TP-16 | 配布/参照形態 | version 不一致時の案内が明確 | README 想定と異なる Microsoft.Extensions.AI.Abstractions version を参照した構成で検証する | 非互換時に必要 version や揃え方へ到達できる |
| TP-17 | 配布/参照形態 | compile-time smoke test fixture 自体が参照契約を検証する | 新規 fixture プロジェクト群を build する | 参照形態ごとの build 成否が CI で継続監視される |

### 実装時の検証手段

- コア・各 provider・samples を含む solution build
- 公開 API シグネチャに対する compile-time 契約テスト
- provider ごとの最小応答テスト
- 新規 fixture を使った参照形態テスト
- 依存 version を変えた参照形態テスト
- README 記載例をそのまま再現する smoke test

## Traceability matrix

| Requirement / behavior | Scenario | Verification |
|---|---|---|
| 公開 Microsoft.Extensions.AI 型を公式 assembly へ一本化する | S-01, S-02, S-03 | TP-03, TP-04, TP-05, TP-06, TP-17 |
| `ChatOptions.ResponseFormat` を利用者が直接使えるようにする | S-01, S-02, S-04 | TP-01, TP-02, TP-07 |
| provider 切替時に利用側コード変更を不要にする | S-02, S-04 | TP-03, TP-11, TP-12 |
| DLL 配布利用でも型重複・キャスト不能を防ぐ | S-03 | TP-05, TP-06, TP-16, TP-17 |
| OpenAICompatible を正式実装へ揃え、型起因ではなく endpoint 制約として失敗させる | S-04 | TP-07, TP-08 |
| GitHubCopilot では型起因ではなく能力起因の失敗にする | S-05 | TP-09 |
| 旧版からの移行不足を早期に検知できるようにする | S-06 | TP-10, TP-13 |
| 既存利用に回帰を出さない | S-01, S-02, S-04 | TP-11, TP-12 |
| 連続実行や多件数実行で型解決が不安定にならない | S-01, S-02, S-03, S-04 | TP-14, TP-15 |

## Definition of Done

- コア package が公式 `Microsoft.Extensions.AI.Abstractions` を直接参照し、独自 `ChatAbstractions.cs` が削除されている
- 公開 API の `ChatOptions`、`ChatMessage`、`ChatResponse`、`IChatClient` が公式 assembly 由来であることをコードとビルドで確認できる
- OpenAI / AzureOpenAI の adapter で独自型と公式型の相互変換専用コードが削除されるか、残る場合は残存理由がコード上で説明されている
- OpenAICompatible が stub 応答を返さず、OpenAI 系 provider として正式な request path に載っている
- OpenAI / AzureOpenAI / OpenAICompatible で `ResponseFormat` が request path 上で保持され、型単一化の過程で失われない
- GitHubCopilot で `ResponseFormat` 指定時の失敗仕様が `NotSupportedException` 系として実装とドキュメントで一致している
- `ChatOptions.ResponseFormat` を使う利用例が少なくとも 1 つ README または sample で示されている
- DLL 配布利用時の依存関係と version 揃え方が README に明記されている
- 破壊的変更としての移行手順が README または release note に明記されている
- 新規 compile-time smoke test fixture を含む Verification design の主要観点が自動テストまたは再現手順でカバーされている

## Risks / rollout / rollback

### 主なリスク

- バイナリ互換性の破壊: 旧版に対してコンパイル済みの利用者アプリは再ビルドが必要になる可能性が高い
- 公式 Microsoft.Extensions.AI 型の実際のプロパティ差異により、現在の独自型前提コードが単純削除では済まない可能性がある
- OpenAICompatible の stub 除去と正式実装化を同時に進めるため、型単一化単体より変更範囲が広がる
- OpenAICompatible や GitHubCopilot の `ResponseFormat` 取り扱いを曖昧にすると、新たな silent ignore を招く
- DLL 配布利用者が version を揃えずに導入すると、今回とは別種の assembly conflict を起こす可能性がある

### rollout 方針

- breaking change として扱い、利用者向けの更新ガイドを先に固める
- まず ProjectReference / solution 内テストで型単一化を完成させ、その後 README の DLL 利用例で再現確認する
- リリースノートに「Microsoft.Extensions.AI 型供給単一化」を目立つ変更点として記載する

### rollback 方針

- 実装中に provider 回帰が大きい場合は、独自型削除を一旦見送り、別ブランチで段階移行案を切り出す
- リリース後に重大な回帰が見つかった場合は、旧系統を hotfix line として維持しつつ、型単一化版は次修正版で前進修正する
- 旧独自型への単純復帰は再度同じ設計不一致を固定化するため、最終手段とする

## Open questions / assumptions

- 変更は実質的に binary breaking なので、versioning を major update とする前提で進めることを推奨する
- 既存 test code や sample code が独自型の簡略 API に依存している場合は、公式型への追随で追加修正が必要になる
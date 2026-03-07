# Feature Specification: MEAI マルチプロバイダー抽象化ライブラリ

**Feature Branch**: `002-meai-multi-provider`  
**Created**: 2026-02-08  
**Status**: Draft  
**Input**: .NET 向けのライブラリ仕様で、公開I/Fは Microsoft.Extensions.AI (MEAI) に合わせ、LLM バックエンドを差し替え可能にする

## 概要

本ライブラリは、GitHub Copilot SDK を主用途とする会話実行モデルを基準に、OpenAI、Azure OpenAI、OpenAI互換ローカルへ横展開できる共通抽象化レイヤーを提供する。公開I/Fは Microsoft.Extensions.AI 準拠を維持しつつ、Copilot SDK で頻出するセッション設定（モデル選択、reasoning effort、system message 制御、tool allow/deny、streaming 等）を共通化し、他プロバイダーへ移行する際の呼び出し側コード変更を最小化する。

### 主要な利用者

- **Copilot SDK 前提のアプリケーション開発者**: GitHub Copilot SDK の機能を活用しつつ、将来のバックエンド変更に備えたい開発者
- **アプリケーション開発者**: チャット機能を組み込みたいが、特定のLLMベンダーに依存したくない開発者
- **マルチテナント/エンタープライズ開発者**: 顧客ごとに異なるLLMプロバイダーを提供したい、または環境ごとに切り替えたい開発者
- **AI実験/検証担当者**: 複数のLLMを評価・比較したいが、プロバイダーごとに異なるコードを書きたくない担当者

### 利用シーン

1. **Copilot SDK を標準採用**: 開発支援アプリで GitHub Copilot SDK を既定にし、同じ呼び出しコードのまま OpenAI/Azure OpenAI に切り替える
2. **段階的移行**: まず GitHub Copilot SDK のモデル・reasoning effort・tool policy を利用し、要件に応じて他プロバイダーへ移行する
3. **マルチテナント**: テナントAはCopilot SDK、テナントBはAzure OpenAI、テナントCは自社ホストのOpenAI互換エンドポイントを使用
4. **開発者体験向上**: IDE・CLIに近い Copilot SDK の体験をアプリへ組み込みつつ、本番では商用LLMへ切り替える

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 設定ベースのプロバイダー切り替え (Priority: P1)

開発者が、アプリケーションコードを変更せずに、構成ファイルの変更のみで異なるLLMプロバイダーに切り替える。

**Why this priority**: 本ライブラリの中核価値。プロバイダー非依存性がなければ、ライブラリの存在意義がない。

**Independent Test**: appsettings.jsonでプロバイダー名（"OpenAI"、"AzureOpenAI"、"OpenAICompatible"、"GitHubCopilot"）を変更し、DIコンテナ登録後、同一のIChatClientインターフェースでチャット呼び出しを実行。プロバイダーごとに異なるエンドポイントへのHTTP呼び出しが行われることをログ/トレースで確認。

**Acceptance Scenarios**:

1. **Given** 開発者がappsettings.jsonで `"Provider": "OpenAI"` を設定、**When** DIコンテナからIChatClientを解決してチャット呼び出しを実行、**Then** OpenAI APIエンドポイント (api.openai.com) に対してリクエストが送信され、レスポンスが返される
2. **Given** 開発者がappsettings.jsonで `"Provider": "AzureOpenAI"` に変更、**When** 同じアプリケーションコードでチャット呼び出しを実行、**Then** Azure OpenAIエンドポイント (*.openai.azure.com) に対してリクエストが送信され、レスポンスが返される
3. **Given** 開発者がappsettings.jsonで `"Provider": "OpenAICompatible"` および `"BaseUrl": "http://localhost:8080"` を設定、**When** チャット呼び出しを実行、**Then** 指定したベースURLに対してOpenAI互換形式のリクエストが送信される
4. **Given** 開発者がappsettings.jsonで `"Provider": "GitHubCopilot"` を設定、**When** チャット呼び出しを実行、**Then** GitHub Copilot SDKを介してチャット機能が実行される

---

### User Story 2 - 共通セッションパラメータでのチャット実行 (Priority: P1)

開発者が、どのプロバイダーを選択していても、モデルID、reasoning effort、システムメッセージ制御、tool allow/deny、温度、最大トークン数、ストリーミングなどの代表的なセッション/生成パラメータを指定してチャットを実行する。

**Why this priority**: GitHub Copilot SDK を主用途にする場合、単なるメッセージ送信だけでなく、モデル選択と reasoning effort を含むセッション設定が必須になる。ここが共通化されていないと、プロバイダー移行時に呼び出しコードが分岐し始め、抽象化の価値が大きく低下する。

**Independent Test**: 共通I/F経由で system message・user message・model="gpt-5"・reasoning effort="high"・temperature=0.7・max tokens=500・streaming=true を指定して実行する。Copilot では session 設定に model/reasoning effort が反映され、他プロバイダーでは対応する設定へ正規化されるか、未対応時は送信前に明確な例外が返ることを確認する。

**Acceptance Scenarios**:

1. **Given** プロバイダーがGitHubCopilotに設定され、選択モデルが reasoning effort をサポートしている、**When** model と reasoning effort を指定してチャット実行、**Then** GitHub Copilot SDK のセッション作成時に両方が適用され、応答が返される
2. **Given** プロバイダーがOpenAIまたはAzure OpenAIに設定されている、**When** 同じ共通セッションパラメータでチャット実行、**Then** 各プロバイダーで表現可能な設定へ正規化され、応答が返される
3. **Given** ストリーミングオプションを有効化、**When** チャット実行、**Then** レスポンスがストリーム形式で送信され、部分的なメッセージを順次受信できる
4. **Given** reasoning effort をサポートしないモデルまたはプロバイダーが選択されている、**When** reasoning effort を指定してチャット実行、**Then** リクエスト送信前に明確な `NotSupportedException` または設定検証例外が返される

---

### User Story 3---

### User Story 3 - ベンダー固有拡張または高度セッション設定の指定 (Priority: P2)

開発者が、共通I/Fを維持しつつ、特定のプロバイダーでのみ必要な高度設定（例：Azure OpenAI の data_sources、GitHub Copilot SDK の BYOK provider override や infinite session 設定）を名前空間付きまたは型付きの拡張オプションとして指定する。

**Why this priority**: 最頻出シナリオは Copilot SDK ベースだが、すべての機能を完全に共通化できるわけではない。共通化できない差分を「明示的に」「失われずに」渡せる設計がないと、Copilot SDK を主用途にした時点で抽象化レイヤーを迂回する必要が生じる。

**Independent Test**: 共通I/Fの拡張オプション引数に、Copilot BYOK provider override または Azure OpenAI data_sources を指定してチャットを実行する。選択中プロバイダーに対応するオプションだけが検証・変換され、他プロバイダー向けオプションや型不一致は送信前に明確な検証エラーとなることを確認する。

**Acceptance Scenarios**:

1. **Given** プロバイダーがGitHubCopilotに設定されている、**When** BYOK provider override と tool allow/deny を指定してチャット実行、**Then** Copilot SDK の session/provider 設定に反映される
2. **Given** プロバイダーがAzure OpenAIに設定されている、**When** 拡張パラメータとして `azure.data_sources` を指定してチャット実行、**Then** Azure OpenAI API リクエストに data_sources が含まれ、RAG結果が返される
3. **Given** 現在のプロバイダーでは解釈できない拡張オプション、または型不一致の拡張オプションが指定された、**When** チャット実行、**Then** 実行前に検証エラーが返され、無視して続行しない

---

### User Story 4---

### User Story 4 - Embedding生成（将来拡張） (Priority: P3)

開発者が、チャット機能と同様に、IEmbeddingGeneratorインターフェースを通じて埋め込みベクトルを生成し、プロバイダーを切り替えられる。

**Why this priority**: チャット機能が安定動作すれば、まずはMVPとして価値提供できる。埋め込み生成は多くのLLM活用シナリオで必要になるが、まずはチャット対応を優先。

**Independent Test**: IEmbeddingGeneratorを解決し、テキストを渡して埋め込みベクトルを取得。プロバイダーを切り替えても同じインターフェースで動作することを確認。

**Acceptance Scenarios**:

1. **Given** プロバイダーがOpenAIに設定されている、**When** IEmbeddingGeneratorで「こんにちは」のテキストの埋め込みを取得、**Then** OpenAI embeddings APIから埋め込みベクトルが返される
2. **Given** プロバイダーがAzure OpenAIに設定されている、**When** 同じテキストで埋め込みを取得、**Then** Azure OpenAI embeddingsエンドポイントから埋め込みベクトルが返される

---

### Edge Cases

- **プロバイダー設定が不正**: プロバイダー名が未知または設定不備の場合、DI登録時またはIChatClient解決時に明確なエラーメッセージを含む例外をスローし、どの設定が不足しているかをログに出力する
- **認証失敗**: APIキーまたは認証情報が無効な場合、プロバイダー固有の401/403エラーを受け取り、例外メッセージとログに認証エラーである旨を明示する
- **ネットワークタイムアウト**: プロバイダーへのリクエストがタイムアウトした場合、TimeoutExceptionをスローし、再試行可能な一時エラーである旨をログに記録する
- **ストリーミング中の接続断**: ストリーミングレスポンス受信中に接続が切断された場合、既に受信済みの部分メッセージを保持し、接続エラーを例外として通知する
- **キャンセル済みトークン**: チャット開始前にキャンセルトークンが既にキャンセル済みの場合、即座にOperationCanceledExceptionをスローし、プロバイダーへのリクエストを送信しない
- **プロバイダー機能非対応**: 特定プロバイダーで未対応の機能（例：GitHub Copilot SDKで埋め込み生成）を呼び出した場合、NotSupportedExceptionをスローし、ログに機能名とプロバイダー名を記録する
- **拡張パラメータの型不一致**: 拡張パラメータが期待する型と異なる場合、送信前に検証エラーを返し、ログへ型情報と失敗理由を記録する
- **reasoning effort 不整合**: 指定した reasoning effort が選択モデルの対応範囲外の場合、モデル能力情報を参照して送信前に失敗させる
- **レート制限**: プロバイダーがレート制限（429）を返した場合、例外に含まれるリトライ推奨時刻をログに記録し、呼び出し側が再試行を判断できるようにする
- **レスポンスフォーマット異常**: プロバイダーから予期しないJSON形式が返された場合、パースエラーをログに記録し、レスポンス本文を含む例外をスローする

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: ライブラリは業界標準のチャットクライアントインターフェース（Microsoft.Extensions.AI準拠）を公開I/Fとして提供しなければならない
- **FR-002**: ライブラリは OpenAI API、Azure OpenAI、OpenAI互換エンドポイント、GitHub Copilot SDK の4つのプロバイダーをサポートしなければならない
- **FR-003**: プロバイダーの選択は、アプリケーション起動時に構成情報を元に決定できなければならない
- **FR-004**: ライブラリはプロバイダー切り替え時にアプリケーションコードの変更を必要としてはならない（チャットクライアントインターフェースの利用法が変わらない）
- **FR-005**: ライブラリは少なくとも以下の共通パラメータをサポートしなければならない：モデルID、reasoning effort、システムメッセージ、システムメッセージ適用モード、ユーザーメッセージ、アシスタントメッセージ、tool allow/deny、温度、最大トークン数、ストップシーケンス、ストリーミング有効化、キャンセルトークン
- **FR-006**: ライブラリはベンダー固有の拡張パラメータまたは高度セッション設定を渡す仕組みを提供しなければならず、その際に共通I/Fの引数構造を破壊してはならない。未対応または不正な拡張オプションを黙って破棄してはならない
- **FR-007**: OpenAI互換エンドポイントのサポートは、ベースURLの設定を変更することで実現しなければならない（専用プロバイダー実装は作らない）
- **FR-008**: ライブラリは GitHub Copilot SDK を主用途として扱い、少なくとも SDK の主要な session 設定（model、reasoning effort、system message、tool allow/deny、streaming、working directory、認証選択、BYOK provider override）を表現できなければならない
- **FR-009**: ライブラリは既存の標準準拠実装が存在する場合、それを再利用しなければならない
- **FR-010**: ライブラリは、自作実装が必要なプロバイダーについてのみ、新規にチャットクライアント実装を提供しなければならない
- **FR-011**: ライブラリはストリーミングレスポンスをサポートし、非同期ストリームとして部分的なメッセージを返せなければならない
- **FR-012**: ライブラリはキャンセルトークンによるリクエストキャンセルをサポートしなければならない
- **FR-013**: ライブラリは、チャット実行時のエラー（HTTP 4xx/5xx、ネットワークエラー、タイムアウト等）を適切な例外として通知しなければならない
- **FR-014**: ライブラリは、各例外にHTTPステータスコード、エラーレスポンス内容、トレース情報を含めなければならない
- **FR-015**: ライブラリは、機密情報（APIキー、認証トークン、ユーザー入力の個人情報）をログに出力してはならない
- **FR-016**: ライブラリは、リクエスト・レスポンスのトレースログを出力し、トレース相関IDを各ログに含めなければならない
- **FR-017**: ライブラリは、プロバイダーごとの機能サポート状況を示す機能マトリクス情報を提供しなければならない（ドキュメントまたはランタイム問合せ可能な形で）。特にモデル選択可否と reasoning effort 対応状況を識別できなければならない
- **FR-018**: ライブラリは、未対応機能が呼び出された場合、明確なエラーを返し、ログに未対応である旨を記録しなければならない
- **FR-019**: ライブラリは現行の長期サポート版ランタイムおよび最新ランタイムをサポートしなければならない
- **FR-020**: ライブラリは、破壊的変更を導入する際にメジャーバージョンを上げるセマンティックバージョニングに従わなければならない
- **FR-021**: ライブラリは、モデル能力情報を問い合わせる仕組み、または選択モデルに対する事前検証機構を提供し、reasoning effort 指定の妥当性を実行前に確認できなければならない

### Key Entities

- **ChatRequest**: チャット実行のために必要な情報（メッセージ履歴、温度、最大トークン数、モデルID、reasoning effort、拡張パラメータ等）を表現する。プロバイダー非依存の共通構造。
- **ConversationExecutionOptions**: GitHub Copilot SDK の session 設定を起点に、他プロバイダーでも共通化できる実行オプション（model、reasoning effort、tool policy、working directory 等）を保持する。公開I/Fでは `ChatOptions.AdditionalProperties["meai.execution"]` に搭載して受け渡す。
- **ChatResponse**: チャット実行結果を表現する。レスポンスメッセージ、使用トークン数、終了理由などの共通情報を保持。
- **ProviderConfiguration**: プロバイダーごとの接続情報（エンドポイントURL、APIキー、デプロイメント名等）を保持する構成エンティティ。
- **ExtensionParameters**: ベンダー固有の拡張パラメータを格納する辞書型エンティティ。プロバイダー実装が必要に応じて解釈。
- **TelemetryContext**: トレース相関ID、リクエストID、タイムスタンプ等のテレメトリ情報を保持し、ログとトレースに含める。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 開発者は、構成ファイルのプロバイダー設定を変更するだけで、アプリケーションコードを変更せずに異なるLLMプロバイダーに切り替えられる
- **SC-002**: 同一のチャットクライアントインターフェースを使用して、4つのプロバイダー（OpenAI、Azure OpenAI、OpenAI互換、GitHub Copilot SDK）すべてでチャット呼び出しが成功する
- **SC-003**: 共通パラメータ（モデルID、reasoning effort、システムメッセージ、温度、最大トークン等）を指定したチャット呼び出しが、すべてのプロバイダーで期待通りに動作する、または未対応時に送信前に明確なエラーとなる
- **SC-004**: ベンダー固有の拡張パラメータを指定したチャット呼び出しが、対応プロバイダーで正しく適用され、非対応/不正な拡張は黙って無視されず検証エラーになる
- **SC-005**: プロバイダー間の機能差（対応・非対応）が、機能マトリクスまたはランタイムエラーメッセージで明示され、開発者が reasoning effort を含めて事前に把握できる
- **SC-006**: チャット実行に失敗した場合、HTTPステータス、エラー詳細、トレースIDを含む例外がスローされ、開発者がトラブルシューティングできる
- **SC-007**: APIキーや認証トークン、ユーザー入力の個人情報がログに含まれない（機密情報の漏洩がない）
- **SC-008**: 既存の標準準拠実装が利用可能なプロバイダーでは、その実装が使用され、ライブラリ独自の重複実装が行われない
- **SC-009**: ストリーミング呼び出しでレスポンスが部分的に返され、キャンセルトークンで実行を中断できる

## Non-Goals / Out of Scope

本機能では以下を**提供しない**：

- **RAGフレームワーク**: 文書検索やベクトルDB統合は別レイヤーで実装
- **プロンプト管理**: プロンプトテンプレートやバージョン管理機能
- **エージェント・ワークフロー**: 複数LLM呼び出しの連鎖や意思決定ロジック
- **画像・音声等のメディア機能**: チャット以外のモダリティは将来拡張として検討
- **GitHub Copilot SDK の全イベント面を MEAI 表面へ完全再現すること**: Session hook や ask_user 等の専用デリゲートを、すべてのプロバイダーで同一抽象へ押し込めることは対象外。ただし必要な設定を失わずに渡せることは対象内
- **自動リトライ・サーキットブレーカー**: 呼び出し側またはミドルウェアで実装

## Assumptions

- **前提001**: 開発者は依存性注入パターンに精通しており、サービス登録方法を理解している
- **前提002**: 各プロバイダーの認証情報（APIキー、エンドポイントURL等）は、構成ファイルまたはシークレット管理サービスから安全に取得される
- **前提003**: GitHub Copilot SDK はTechnical Preview段階であり、将来的にAPIが変更される可能性を利用者が理解している
- **前提004**: OpenAI互換エンドポイントは、OpenAI APIの主要エンドポイント（/v1/chat/completions等）に準拠していることを前提とする。大きく逸脱したエンドポイントは対象外
- **前提005**: 非同期ストリーム処理が利用可能なランタイム環境で動作する
- **前提006**: プロバイダー間の細かなレスポンス差異（例：トークンカウント精度、終了理由の文言）は完全に統一しない。各プロバイダーの返す情報をそのまま伝達する方針

## Risks & Mitigations

### Risk 001: GitHub Copilot SDK のAPI安定性

**リスク**: GitHub Copilot SDK がTechnical Previewであり、破壊的変更が発生する可能性がある

**影響**: Copilot SDK対応実装が突然動作しなくなり、ライブラリ全体のバージョンアップが必要になる

**緩和策**: 
- Copilot SDK対応を独立したパッケージとして分離し、他プロバイダーへの影響を最小化
- Copilot SDK依存パッケージをオプショナル依存とし、インストールしない場合でも他プロバイダーは動作するようにする
- SDKバージョンアップ時の互換性テストを自動化

### Risk 002: OpenAI互換エンドポイントの挙動差異

**リスク**: OpenAI互換を謳うエンドポイントでも、細かなリクエスト・レスポンス形式が異なる場合がある

**影響**: 一部のOpenAI互換エンドポイントで予期しないエラーやパースエラーが発生する

**緩和策**: 
- 代表的なOpenAI互換実装（Foundry Local、Ollama等）でテストを実施
- レスポンスパース時の寛容なエラーハンドリング（未知フィールドは無視）
- 互換性マトリクスをドキュメント化し、動作確認済みエンドポイントを明示

### Risk 003: プロバイダー間の機能差による混乱

**リスク**: あるプロバイダーでは動作する機能が、別のプロバイダーでは未対応の場合、開発者が混乱する

**影響**: 実行時エラーが発生し、開発者が原因を特定できず、ライブラリの信頼性が低下する

**緩和策**: 
- 機能サポートマトリクスをドキュメントとして提供
- 未対応機能呼び出し時に明確なエラーメッセージ（「プロバイダーXは機能Yをサポートしていません」）を表示
- 可能であれば、コンパイル時またはDI登録時に機能互換性を検証する仕組みを提供

### Risk 004: 認証情報の誤露出

**リスク**: APIキーやトークンがログに出力され、機密情報が漏洩する

**影響**: セキュリティインシデント、コンプライアンス違反

**緩和策**: 
- ログ出力時にAPIキーなどの機密情報をマスキングする専用ロギングフィルターを実装
- 構成検証時に警告を表示（例：APIキーが環境変数ではなくプレーンテキストで設定されている場合）
- セキュリティレビューとペネトレーションテストの実施

## Dependencies

- **Microsoft.Extensions.AI標準インターフェース**: 業界標準のチャットクライアントインターフェース定義に準拠
- **既存プロバイダー実装**: OpenAI、Azure OpenAI等で標準準拠実装が存在する場合は再利用
- **GitHub Copilot SDK**: GitHub Copilotプロバイダー対応のため（オプショナル依存。Technical Preview段階のため、将来的な変更の可能性あり）

## References

- Microsoft.Extensions.AI ドキュメント: 標準チャットクライアントインターフェースの公式仕様
- OpenAI API リファレンス: OpenAI APIの公式仕様
- Azure OpenAI Service ドキュメント: Azure OpenAIの仕様と差異
- GitHub Copilot SDK: GitHub Copilot SDKのドキュメント（Technical Preview段階）

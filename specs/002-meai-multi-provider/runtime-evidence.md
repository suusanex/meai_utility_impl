# Runtime Evidence: MEAI マルチプロバイダー抽象化ライブラリ

## Scenario Sections

### Scenario S-010: 構成ベースのプロバイダー解決

#### Sequence (PlantUML)

```plantuml
@startuml
title S-010 構成ベースのプロバイダー解決

actor "Application" as Application
participant "C_Config\nConfigurationBinder" as C_Config
participant "C_Registry\nProviderRegistry" as C_Registry
participant "C_Factory\nProviderFactory" as C_Factory
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
Application -> C_Config : [E1] Load MultiProvider settings
C_Config -> C_Registry : [E2] Validate provider key
C_Registry -> C_Factory : [E3] Resolve provider implementation
C_Factory --> C_Registry : [E4] Return IChatClient binding
C_Registry -> T_Logger : [E5] Log provider resolved(traceId)
C_Registry --> Application : [E6] Return configured IChatClient

== Variations ==
alt [E2] provider key is unknown
  C_Registry -> T_Logger : [E7] Log validation error(Exception.ToString)
  C_Registry --> Application : [E8] Throw InvalidOperationException
else success
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| Application | E1, E6, E8 | DI解決時の成功/失敗 |
| C_Config | E1, E2 | 設定値バインドと基本検証 |
| C_Registry | E2, E3, E5, E7, E8 | プロバイダー選択と例外化 |
| C_Factory | E3, E4 | 実装インスタンス解決 |
| T_Logger | E5, E7 | トレース相関付きログ |

### Scenario S-020: OpenAI で共通チャットを実行

#### Sequence (PlantUML)

```plantuml
@startuml
title S-020 OpenAI で共通チャットを実行

actor "User" as User
participant "U_Client\nApplication Service" as U_Client
participant "P_OpenAI\nOpenAI Adapter" as P_OpenAI
participant "X_OpenAI\nOpenAI API" as X_OpenAI
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Send chat input
U_Client -> P_OpenAI : [E2] ChatAsync(common options)
P_OpenAI -> X_OpenAI : [E3] POST /v1/chat/completions
X_OpenAI --> P_OpenAI : [E4] 200 + response payload
P_OpenAI -> T_Logger : [E5] Log usage/trace without secrets
P_OpenAI --> U_Client : [E6] Return normalized ChatResponse
U_Client --> User : [E7] Render answer

== Variations ==
alt [E3] 401/403 authentication error
  P_OpenAI -> T_Logger : [E8] Log error response body(Exception.ToString)
  P_OpenAI --> U_Client : [E9] Throw AuthenticationException
else [E3] 429 rate-limited
  P_OpenAI -> T_Logger : [E10] Log retry-after and response body
  P_OpenAI --> U_Client : [E11] Throw RateLimitException
else success
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1, E7 | 入出力の利用者観点 |
| U_Client | E1, E2, E6, E7, E9, E11 | IChatClient呼び出し面 |
| P_OpenAI | E2, E3, E5, E6, E8, E9, E10, E11 | 共通パラメータ変換と例外化 |
| X_OpenAI | E3, E4 | 外部API応答 |
| T_Logger | E5, E8, E10 | 成功/失敗ログ |

### Scenario S-030: Azure OpenAI で拡張パラメータを適用

#### Sequence (PlantUML)

```plantuml
@startuml
title S-030 Azure OpenAI で拡張パラメータを適用

actor "User" as User
participant "U_Client\nApplication Service" as U_Client
participant "P_AzureOpenAI\nAzure OpenAI Adapter" as P_AzureOpenAI
participant "X_AzureOpenAI\nAzure OpenAI API" as X_AzureOpenAI
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Send prompt + extension parameters
U_Client -> P_AzureOpenAI : [E2] ChatAsync(common + azure.data_sources)
P_AzureOpenAI -> P_AzureOpenAI : [E3] Validate/transform extension parameters
P_AzureOpenAI -> X_AzureOpenAI : [E4] POST /chat/completions with data_sources
X_AzureOpenAI --> P_AzureOpenAI : [E5] 200 + grounded response
P_AzureOpenAI -> T_Logger : [E6] Log extension applied(traceId)
P_AzureOpenAI --> U_Client : [E7] Return normalized ChatResponse

== Variations ==
alt [E3] extension parameter type mismatch
  P_AzureOpenAI -> T_Logger : [E8] Log validation failure(Exception.ToString)
  P_AzureOpenAI --> U_Client : [E9] Throw InvalidRequestException
else [E4] 400 invalid request
  P_AzureOpenAI -> T_Logger : [E10] Log error response body(Exception.ToString)
  P_AzureOpenAI --> U_Client : [E11] Throw InvalidRequestException
else success
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1 | 拡張要求入力 |
| U_Client | E1, E2, E7, E9, E11 | 共通I/F呼び出しと受領 |
| P_AzureOpenAI | E2, E3, E4, E6, E7, E8, E9, E10, E11 | 拡張解釈とAOAI呼び出し |
| X_AzureOpenAI | E4, E5 | data_sources適用有無 |
| T_Logger | E6, E8, E10 | 検証/エラーログ |

### Scenario S-040: OpenAI互換エンドポイントへベースURL差し替えで送信

#### Sequence (PlantUML)

```plantuml
@startuml
title S-040 OpenAI互換エンドポイントへベースURL差し替えで送信

actor "User" as User
participant "U_Client\nApplication Service" as U_Client
participant "P_OpenAICompatible\nOpenAI Compatible Adapter" as P_OpenAICompatible
participant "X_LocalCompat\nOpenAI-Compatible Endpoint" as X_LocalCompat
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Send chat input
U_Client -> P_OpenAICompatible : [E2] ChatAsync(common options)
P_OpenAICompatible -> P_OpenAICompatible : [E3] Build request with configured BaseUrl
P_OpenAICompatible -> X_LocalCompat : [E4] POST /v1/chat/completions
X_LocalCompat --> P_OpenAICompatible : [E5] 200 + compatible payload
P_OpenAICompatible -> T_Logger : [E6] Log target base URL (masked)
P_OpenAICompatible --> U_Client : [E7] Return normalized ChatResponse

== Variations ==
alt [E5] incompatible payload format
  P_OpenAICompatible -> T_Logger : [E8] Log parse failure + response body(Exception.ToString)
  P_OpenAICompatible --> U_Client : [E9] Throw ProviderException
else success
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1 | 利用開始 |
| U_Client | E1, E2, E7, E9 | 共通I/F維持 |
| P_OpenAICompatible | E2, E3, E4, E6, E7, E8, E9 | BaseUrl差し替えと互換吸収 |
| X_LocalCompat | E4, E5 | OpenAI互換応答 |
| T_Logger | E6, E8 | 接続先/エラー記録 |

### Scenario S-050: GitHub Copilot SDK でモデル能力を確認してセッション開始

#### Sequence (PlantUML)

```plantuml
@startuml
title S-050 GitHub Copilot SDK でモデル能力を確認してセッション開始

actor "User" as User
participant "U_Client
Application Service" as U_Client
participant "P_GitHubCopilot
Copilot Adapter" as P_GitHubCopilot
participant "X_CopilotRuntime
Copilot SDK Runtime" as X_CopilotRuntime
participant "T_Logger
TraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Send chat input + common session options
U_Client -> P_GitHubCopilot : [E2] ChatAsync(model, reasoning effort, tool policy)
P_GitHubCopilot -> X_CopilotRuntime : [E3] List available models
X_CopilotRuntime --> P_GitHubCopilot : [E4] Return model capabilities
P_GitHubCopilot -> P_GitHubCopilot : [E5] Validate reasoning effort support
P_GitHubCopilot -> X_CopilotRuntime : [E6] Create session(Model, ReasoningEffort, AvailableTools, ExcludedTools, Streaming)
X_CopilotRuntime --> P_GitHubCopilot : [E7] Session created
P_GitHubCopilot -> X_CopilotRuntime : [E8] Send prompt
X_CopilotRuntime --> P_GitHubCopilot : [E9] Return assistant message
P_GitHubCopilot -> T_Logger : [E10] Log selected model/reasoning effort(traceId)
P_GitHubCopilot --> U_Client : [E11] Return normalized ChatResponse

== Variations ==
alt [E4] model is unknown or reasoning effort unsupported
  P_GitHubCopilot -> T_Logger : [E12] Log capability mismatch(Exception.ToString)
  P_GitHubCopilot --> U_Client : [E13] Throw NotSupportedException
else [E6] runtime startup or session creation failed
  P_GitHubCopilot -> T_Logger : [E14] Log runtime startup failure(Exception.ToString)
  P_GitHubCopilot --> U_Client : [E15] Throw CopilotRuntimeException
else success
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1 | 入力送信 |
| U_Client | E1, E2, E11, E13, E15 | 呼び出しと例外受領 |
| P_GitHubCopilot | E2, E3, E5, E6, E8, E10, E11, E12, E13, E14, E15 | SDKアダプタ層と capability 検証 |
| X_CopilotRuntime | E3, E4, E6, E7, E8, E9 | SDK runtime 応答 |
| T_Logger | E10, E12, E14 | 実行証跡 |

### Scenario S-060: ストリーミング受信とキャンセル

#### Sequence (PlantUML)

```plantuml
@startuml
title S-060 ストリーミング受信とキャンセル

actor "User" as User
participant "U_Client\nApplication Service" as U_Client
participant "P_Provider\nSelected Provider Adapter" as P_Provider
participant "X_ModelAPI\nProvider Model API" as X_ModelAPI
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Start streaming chat
U_Client -> P_Provider : [E2] StreamAsync(request, cancellationToken)
P_Provider -> X_ModelAPI : [E3] Open stream request
X_ModelAPI --> P_Provider : [E4] Chunk#1
P_Provider --> U_Client : [E5] Yield chunk#1
X_ModelAPI --> P_Provider : [E6] Chunk#2
P_Provider --> U_Client : [E7] Yield chunk#2

== Variations ==
alt [E2] cancellation requested during stream
  U_Client -> P_Provider : [E8] Cancel token signaled
  P_Provider -> X_ModelAPI : [E9] Abort upstream request
  P_Provider -> T_Logger : [E10] Log cancellation(Exception.ToString)
  P_Provider --> U_Client : [E11] Throw OperationCanceledException
else [E3] stream disconnected
  P_Provider -> T_Logger : [E12] Log disconnection and partial chunks
  P_Provider --> U_Client : [E13] Throw ProviderException
else stream completed
  P_Provider -> T_Logger : [E14] Log stream complete
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1 | ストリーミング開始 |
| U_Client | E1, E2, E5, E7, E8, E11, E13 | チャンク受信とキャンセル伝播 |
| P_Provider | E2, E3, E5, E7, E9, E10, E11, E12, E13, E14 | ストリーム制御 |
| X_ModelAPI | E3, E4, E6, E9 | 上流ストリーム |
| T_Logger | E10, E12, E14 | 中断/切断/完了ログ |

### Scenario S-070: HTTPエラー時の例外化とエラーレスポンス記録

#### Sequence (PlantUML)

```plantuml
@startuml
title S-070 HTTPエラー時の例外化とエラーレスポンス記録

actor "User" as User
participant "U_Client\nApplication Service" as U_Client
participant "P_Provider\nProvider Adapter" as P_Provider
participant "X_ModelAPI\nProvider Model API" as X_ModelAPI
participant "T_Logger\nTraceLogger" as T_Logger

autonumber

== Main ==
User -> U_Client : [E1] Send request
U_Client -> P_Provider : [E2] ChatAsync()
P_Provider -> X_ModelAPI : [E3] Send HTTP request
X_ModelAPI --> P_Provider : [E4] 5xx + error body
P_Provider -> T_Logger : [E5] Log status + error body + traceId
P_Provider --> U_Client : [E6] Throw ProviderException

== Variations ==
alt [E4] timeout/no response
  P_Provider -> T_Logger : [E7] Log timeout(Exception.ToString)
  P_Provider --> U_Client : [E8] Throw TimeoutException
else [E4] 4xx client error
  P_Provider -> T_Logger : [E9] Log status + error body(Exception.ToString)
  P_Provider --> U_Client : [E10] Throw InvalidRequestException or AuthenticationException
else handled as server error
end

@enduml
```

#### Component–Step Map

| Component | Steps | Evidence |
|---|---|---|
| User | E1 | リクエスト起点 |
| U_Client | E1, E2, E6, E8, E10 | 例外受領 |
| P_Provider | E2, E3, E5, E6, E7, E8, E9, E10 | HTTPエラー→例外変換 |
| X_ModelAPI | E3, E4 | エラーレスポンス返却 |
| T_Logger | E5, E7, E9 | エラーレスポンス内容記録 |

## Scenario Ledger

| Scenario ID | 目的/価値 | Given | When | Then | 参加者（Vocabulary ID） | 入出力/メッセージ | 例外・タイムアウト・リトライ | 観測点（ログ/メトリクス） | セクション |
|---|---|---|---|---|---|---|---|---|---|
| S-010 | 構成変更のみでプロバイダーを切り替える | MultiProvider設定が存在 | DIで IChatClient を解決 | 対応プロバイダーが解決される | Application, C_Config, C_Registry, C_Factory, T_Logger | 設定読込/解決 | 不正Providerは即例外（フォールバックなし） | provider_resolved, provider_validation_error | [Scenario S-010](#scenario-s-010-構成ベースのプロバイダー解決) |
| S-020 | OpenAIで共通チャットを実行する | Provider=OpenAI | ChatAsync を実行 | 正規化レスポンスを返す | User, U_Client, P_OpenAI, X_OpenAI, T_Logger | Chat request/response | 401/403,429を例外化し記録 | openai_chat_success, openai_chat_error | [Scenario S-020](#scenario-s-020-openai-で共通チャットを実行) |
| S-030 | Azure OpenAI拡張パラメータを適用する | Provider=AzureOpenAI | data_sources付きで実行 | 拡張適用して応答を返す | User, U_Client, P_AzureOpenAI, X_AzureOpenAI, T_Logger | common + azure extensions | 型不一致/400は送信前または応答時に例外化 | azure_extension_applied, azure_extension_validation_error | [Scenario S-030](#scenario-s-030-azure-openai-で拡張パラメータを適用) |
| S-040 | OpenAI互換 endpoint へ送信する | Provider=OpenAICompatible | BaseUrl指定で実行 | 互換応答を正規化して返す | User, U_Client, P_OpenAICompatible, X_LocalCompat, T_Logger | BaseUrl差し替えHTTP | 互換性崩れはパース失敗として例外化 | compatible_target_selected, compatible_parse_error | [Scenario S-040](#scenario-s-040-openai互換エンドポイントへベースurl差し替えで送信) |
| S-050 | GitHub Copilotで model/reasoning effort を使ってチャットする | Provider=GitHubCopilot | ChatAsync を実行 | model capability を確認して session 作成・応答返却 | User, U_Client, P_GitHubCopilot, X_CopilotRuntime, T_Logger | SDK session create + capability query | capability mismatch / 起動失敗を例外化 | copilot_model_selected, copilot_reasoning_effort_validated | [Scenario S-050](#scenario-s-050-github-copilot-sdk-でモデル能力を確認してセッション開始) |
| S-060 | ストリーミングとキャンセルを扱う | ストリーミング有効 | StreamAsync 実行中にキャンセル/切断 | チャンク配信または明確な例外通知 | User, U_Client, P_Provider, X_ModelAPI, T_Logger | async stream chunks | キャンセルはOperationCanceledException、切断はProviderException | stream_chunk_count, stream_canceled, stream_disconnected | [Scenario S-060](#scenario-s-060-ストリーミング受信とキャンセル) |
| S-070 | HTTPエラー詳細を保持し例外化する | 外部APIがエラー返却 | ChatAsync 実行 | ステータスと本文をログ化して例外 | User, U_Client, P_Provider, X_ModelAPI, T_Logger | HTTP error response | 4xx/5xx/timeoutを種別別例外化 | provider_http_error, provider_timeout | [Scenario S-070](#scenario-s-070-httpエラー時の例外化とエラーレスポンス記録) |

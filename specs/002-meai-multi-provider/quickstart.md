# Quickstart: MEAI マルチプロバイダー抽象化ライブラリ

**所要時間**: 5分  
**前提条件**: .NET 8.0 LTS または .NET 10.0 SDK がインストール済み

本ガイドでは、最小構成でMEAIマルチプロバイダーライブラリを動作させる手順を示します。

---

## Step 1: プロジェクトの作成

```bash
# 新しいコンソールアプリを作成
dotnet new console -n MyAIChatApp
cd MyAIChatApp

# 必要なパッケージをインストール（NuGet公開後）
dotnet add package MeAiUtility.MultiProvider
dotnet add package MeAiUtility.MultiProvider.OpenAI
```

**注**: パッケージ未公開の場合は、ローカルビルドしたnupkgを参照してください。

---

## Step 2: GitHub Copilot CLI の準備

GitHub Copilot SDK を既定プロバイダーとして使うため、Copilot CLI をインストールし、認証を済ませます。

**検証スコープ**: ライブラリの自動テストは SDK ラッパーとスタブ化した host を対象にし、CLI の配布形態差異やローカル環境差は quickstart に基づく手動確認で扱います。

### 動作確認
```powershell
copilot --version
```

### 認証パターン
- **推奨**: 事前に `copilot` CLI でログインし、`UseLoggedInUser=true` を使う
- **明示トークンを使う場合**: `COPILOT_GITHUB_TOKEN` / `GH_TOKEN` / `GITHUB_TOKEN` を設定し、必要に応じて `GitHubToken` を構成へ指定する

**セキュリティ注意**: 本番環境では、トークンを appsettings.json に直書きせず、環境変数またはシークレット管理サービスを使用してください。

---

## Step 3: appsettings.json の作成

プロジェクトルートに `appsettings.json` を作成します。最頻出シナリオに合わせ、既定値は GitHub Copilot SDK を使用します。

```json
{
  "MultiProvider": {
    "Provider": "GitHubCopilot",
    "GitHubCopilot": {
      "UseLoggedInUser": true,
      "ModelId": "gpt-5",
      "ReasoningEffort": "high",
      "Streaming": true,
      "AvailableTools": ["view", "rg"],
      "TimeoutSeconds": 120
    },
    "Common": {
      "DefaultTemperature": 0.7,
      "EnableTelemetry": false
    }
  }
}
```

**補足**:
- `ReasoningEffort` は選択モデルが対応している場合のみ有効です。未対応モデルでは送信前に明確なエラーになります。
- `UseLoggedInUser=false` にする場合は、環境変数または `GitHubToken` のいずれかで認証情報を供給してください。

**.csprojへの追加**:
`appsettings.json` を出力ディレクトリにコピーするため、`.csproj` に以下を追加：

```xml
<ItemGroup>
  <Content Include="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

## Step 4: Program.cs の実装

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MeAiUtility.MultiProvider;

// アプリケーションホストを構築
var builder = Host.CreateApplicationBuilder(args);

// 構成を読み込み
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// MEAI マルチプロバイダーを登録
builder.Services.AddMultiProviderChat(builder.Configuration);

// 共通セッションオプション（モデル / reasoning effort 等）は実行時に上書き可能

// ホストをビルド
var app = builder.Build();

// IChatClient を取得
var chatClient = app.Services.GetRequiredService<IChatClient>();

// チャットを実行
Console.WriteLine("質問を入力してください:");
var userMessage = Console.ReadLine();

var response = await chatClient.GetResponseAsync(userMessage);

Console.WriteLine($"\n回答: {response.Message.Text}");
```

---

## Step 5: 実行

```bash
dotnet run
```

**実行例**:
```text
質問を入力してください:
こんにちは

回答: こんにちは！何かお手伝いできることはありますか？
```

---

## プロバイダーの切り替え

appsettings.json の `Provider` フィールドを変更するだけで、異なるLLMプロバイダーに切り替えられます。**呼び出しコードはそのまま**で、Copilot SDK を起点に他プロバイダーへ段階的に移行できます。

### Azure OpenAI に切り替え

```json
{
  "MultiProvider": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://myresource.openai.azure.com",
      "DeploymentName": "gpt-5-deployment",
      "ApiVersion": "2024-02-15-preview",
      "Authentication": {
        "Type": "ApiKey",
        "ApiKey": "${AZURE_OPENAI_API_KEY}"
      }
    }
  }
}
```

**ポイント**:
- 共通I/Fで指定した `ModelId` / `ReasoningEffort` は、Azure 側で表現可能な場合のみ正規化されます
- 正規化できない組み合わせは警告で黙殺せず、実行前にエラーとなります

### OpenAI互換（Ollama）に切り替え

```json
{
  "MultiProvider": {
    "Provider": "OpenAICompatible",
    "OpenAICompatible": {
      "BaseUrl": "http://localhost:11434/v1",
      "ModelName": "llama2"
    }
  }
}
```

**互換保証範囲**:
- `BaseUrl` 差し替え、`/v1/chat/completions` 系のチャット実行、ストリーミング、モデル名マッピング、stop sequences、基本生成パラメータを対象とします
- structured output や tool replay など互換差異が大きい機能は初期リリース対象外とし、解釈できない場合は警告で継続せず明確な例外になります

---

## ストリーミングレスポンスの使用

リアルタイムでレスポンスを受信したい場合：

```csharp
Console.WriteLine("質問を入力してください:");
var userMessage = Console.ReadLine();

Console.Write("\n回答: ");

await foreach (var update in chatClient.GetStreamingResponseAsync(userMessage))
{
    Console.Write(update.Text);
}

Console.WriteLine();
```

---

## 高度なオプションの指定

GitHub Copilot SDK を基準にした共通セッションオプションを使うと、モデルと reasoning effort を明示できます。

```csharp
var options = new ChatOptions
{
    Temperature = 0.2f,
    MaxOutputTokens = 500,
    AdditionalProperties = new AdditionalPropertiesDictionary
    {
        ["meai.execution"] = new ConversationExecutionOptions
        {
            ModelId = "gpt-5",
            ReasoningEffort = ReasoningEffortLevel.High,
            Streaming = true,
            AllowedTools = ["view", "rg"]
        }
    }
};

var messages = new[]
{
    new ChatMessage(ChatRole.System, "You are a careful assistant."),
    new ChatMessage(ChatRole.User, userMessage),
};

var response = await chatClient.GetResponseAsync(messages, options);
```

**設計意図**:
- `ChatOptions` には生成パラメータを置く
- `ChatOptions.AdditionalProperties["meai.execution"]` に Copilot SDK 起点の session 設定を載せる
- ライブラリ内部では `ConversationExecutionOptions` へ正規化して各プロバイダーへ変換する
- reasoning effort 非対応のモデル/プロバイダーでは、送信前に失敗する

---

## 拡張パラメータの使用（Azure OpenAI RAG）

Azure OpenAI で Azure AI Search を使用する場合：

```csharp
var options = new ChatOptions
{
    AdditionalProperties = new AdditionalPropertiesDictionary
    {
        ["azure.data_sources"] = new[]
        {
            new
            {
                type = "azure_search",
                parameters = new
                {
                    endpoint = "https://mysearch.search.windows.net",
                    index_name = "my-index",
                    authentication = new
                    {
                        type = "api_key",
                        key = Environment.GetEnvironmentVariable("AZURE_SEARCH_KEY")
                    }
                }
            }
        }
    }
};

var response = await chatClient.GetResponseAsync("社内ドキュメントから情報を検索してください", options);
```

---

## トラブルシューティング

### 問題: "Provider-specific options must be configured" エラー

**原因**: appsettings.json で `Provider` に対応するセクションが設定されていない。

**解決**: Provider="OpenAI" の場合、`OpenAI` セクションも設定してください。

---

### 問題: "Authentication failed" エラー

**原因**: APIキーが無効、または環境変数が設定されていない。

**解決**: 
1. 環境変数が正しく設定されているか確認: `echo $env:OPENAI_API_KEY` (PowerShell)
2. APIキーが有効か、プロバイダーのダッシュボードで確認
3. appsettings.json の環境変数参照形式が正しいか確認: `"${OPENAI_API_KEY}"`

---

### 問題: ローカルの Ollama に接続できない

**原因**: Ollama が起動していない、またはポートが異なる。

**解決**:
1. Ollama を起動: `ollama serve`
2. エンドポイントを確認: `http://localhost:11434` （デフォルト）
3. appsettings.json の `BaseUrl` を確認

---

## 次のステップ

- **テレメトリの有効化**: `EnableTelemetry: true` で OpenTelemetry トレーシングを試す
- **複数プロバイダーの比較**: 同じプロンプトと `ConversationExecutionOptions` を使って異なるプロバイダーの応答を比較
- **サンプルアプリの確認**: `src/MeAiUtility.MultiProvider.Samples` に追加例あり
- **詳細ドキュメント**: [plan.md](plan.md)、[data-model.md](data-model.md)、[research.md](research.md) を参照

---

## 最小限のワンライナー実行

設定ファイル不要で、コードのみで動作させる場合：

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MeAiUtility.MultiProvider;

var services = new ServiceCollection();
services.AddMultiProviderChat(options =>
{
    options.Provider = "OpenAI";
    options.OpenAI = new()
    {
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
        ModelName = "gpt-4o-mini"
    };
});

var provider = services.BuildServiceProvider();
var chatClient = provider.GetRequiredService<IChatClient>();

var response = await chatClient.GetResponseAsync("Hello, AI!");
Console.WriteLine(response.Message.Text);
```

実行:
```bash
dotnet run
```

---

**おめでとうございます！** MEAIマルチプロバイダーライブラリが動作しました。構成変更だけで、他のプロバイダーに切り替えて試してみてください。

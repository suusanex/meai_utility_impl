# Quickstart: MEAI マルチプロバイダー抽象化ライブラリ

**所要時間**: 5分  
**前提条件**: .NET 10.0 SDKがインストール済み

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

## Step 2: 環境変数の設定

OpenAI APIキーを環境変数に設定します。

### Windows (PowerShell)
```powershell
$env:OPENAI_API_KEY = "your-api-key-here"
```

### Linux / macOS
```bash
export OPENAI_API_KEY="your-api-key-here"
```

**セキュリティ注意**: 本番環境では、Azure Key Vault や Secrets Manager 等の秘匿情報管理サービスを使用してください。

---

## Step 3: appsettings.json の作成

プロジェクトルートに `appsettings.json` を作成します。

```json
{
  "MultiProvider": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "${OPENAI_API_KEY}",
      "ModelName": "gpt-4o-mini",
      "TimeoutSeconds": 60
    },
    "Common": {
      "DefaultTemperature": 0.7,
      "EnableTelemetry": false
    }
  }
}
```

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

appsettings.json の `Provider` フィールドを変更するだけで、異なるLLMプロバイダーに切り替えられます。**コードの変更は不要**です。

### Azure OpenAI に切り替え

```json
{
  "MultiProvider": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://myresource.openai.azure.com",
      "DeploymentName": "gpt-4-deployment",
      "ApiVersion": "2024-02-15-preview",
      "Authentication": {
        "Type": "ApiKey",
        "ApiKey": "${AZURE_OPENAI_API_KEY}"
      }
    }
  }
}
```

環境変数を設定：
```powershell
$env:AZURE_OPENAI_API_KEY = "your-azure-api-key"
```

再実行：
```bash
dotnet run
```

### OpenAI互換（Ollama）に切り替え

Ollama をローカルで起動している場合：

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

環境変数は不要（ローカル実行のため）、そのまま実行：
```bash
dotnet run
```

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

```csharp
var options = new ChatOptions
{
    Temperature = 0.9f,      // より創造的な応答
    MaxOutputTokens = 500,   // 短めの応答
    StopSequences = new[] { "\n\n" }  // 段落区切りで停止
};

var response = await chatClient.GetResponseAsync(userMessage, options);
```

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
- **複数プロバイダーの比較**: 同じプロンプトで異なるプロバイダーの応答を比較
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

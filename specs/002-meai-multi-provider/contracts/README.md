# Contracts: API Schema Definitions

本ディレクトリには、MEAI マルチプロバイダー抽象化ライブラリの主要なデータ構造を定義するJSON Schemaファイルが含まれます。

## ファイル一覧

### provider-config.schema.json

**用途**: appsettings.json等の構成ファイルで使用する `MultiProvider` セクションのスキーマ定義。

**検証対象**: 
- プロバイダー選択（Provider フィールド）
- 各プロバイダー固有の設定（ApiKey、Endpoint、DeploymentName等）
- 共通設定（Common セクション）

**使用方法**:

1. **VS Code での IntelliSense**:
   ```json
   {
     "$schema": "./specs/002-meai-multi-provider/contracts/provider-config.schema.json",
     "MultiProvider": {
       // IntelliSense が有効化される
     }
   }
   ```

2. **自動検証**:
   - JSON Schema Validator を使用して、起動前に設定ファイルを検証
   - CI/CDパイプラインで構成ファイルの妥当性を自動チェック

**主要な検証ルール**:
- `Provider` は "OpenAI", "AzureOpenAI", "OpenAICompatible", "GitHubCopilot" のいずれか
- `Provider` に対応するプロバイダー固有設定が必須（例: Provider="OpenAI" なら `OpenAI` セクション必須）
- 必須フィールドの存在確認（ApiKey、Endpoint等）
- GitHub Copilot 向け model / reasoning effort / tool policy / BYOK provider override の構成妥当性確認
- 型チェック（文字列、整数、URL形式等）

---

## スキーマ設計方針

### 1. プロバイダー固有設定の分離

各プロバイダーの設定は独立したオブジェクトとして定義され、使用しないプロバイダーの設定は省略可能。

```json
{
  "MultiProvider": {
    "Provider": "OpenAI",
    "OpenAI": { ... },
    // AzureOpenAI, OpenAICompatible, GitHubCopilot セクションは不要
    "Common": { ... }
  }
}
```

### 2. 環境変数参照

機密情報（APIキー、トークン）は環境変数参照形式 `${VAR_NAME}` を推奨。

```json
{
  "OpenAI": {
    "ApiKey": "${OPENAI_API_KEY}"
  }
}
```

**注**: JSON Schema では環境変数展開の検証は行わない（実行時に解決）。

### 3. デフォルト値の明示

オプショナルなフィールドには `default` を明示し、省略時の動作を明確化。

```json
{
  "Common": {
    "DefaultTemperature": 0.7,  // デフォルト値
    "EnableTelemetry": true
  }
}
```

### 4. 条件付き必須フィールド

一部のフィールドは他のフィールドの値に応じて必須となる（例: Authentication.Type="ApiKey" の場合、ApiKey 必須）。

JSON Schema の `if-then` 構造で表現：
```json
{
  "if": { "properties": { "Type": { "const": "ApiKey" } } },
  "then": { "required": ["ApiKey"] }
}
```

---

## スキーマの検証方法

### コマンドラインツール

```bash
# npm の ajv-cli を使用
npm install -g ajv-cli
ajv validate -s provider-config.schema.json -d appsettings.json

# PowerShell から実行
ajv validate `
  -s .\specs\002-meai-multi-provider\contracts\provider-config.schema.json `
  -d .\appsettings.json
```

### .NET での検証

```csharp
using NJsonSchema;

var schema = await JsonSchema.FromFileAsync("provider-config.schema.json");
var errors = schema.Validate(jsonContent);

if (errors.Any())
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation error: {error.Path} - {error.Kind}");
    }
}
```

---

## 設定例

### OpenAI

```json
{
  "MultiProvider": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "${OPENAI_API_KEY}",
      "ModelName": "gpt-4",
      "TimeoutSeconds": 60
    },
    "Common": {
      "DefaultTemperature": 0.7,
      "EnableTelemetry": true
    }
  }
}
```

### Azure OpenAI

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

### OpenAI互換（Ollama）

```json
{
  "MultiProvider": {
    "Provider": "OpenAICompatible",
    "OpenAICompatible": {
      "BaseUrl": "http://localhost:11434/v1",
      "ModelName": "llama2",
      "ModelMapping": {
        "gpt-4": "llama2",
        "gpt-4o-mini": "mistral"
      },
      "StrictCompatibilityMode": true
    }
  }
}
```

### GitHub Copilot

```json
{
  "MultiProvider": {
    "Provider": "GitHubCopilot",
    "GitHubCopilot": {
      "UseLoggedInUser": true,
      "ModelId": "gpt-5",
      "ReasoningEffort": "high",
      "AvailableTools": ["view", "rg"],
      "TimeoutSeconds": 120,
      "ProviderOverride": {
        "Type": "openai",
        "BaseUrl": "https://api.openai.com/v1",
        "ApiKey": "${OPENAI_API_KEY}"
      }
    }
  }
}
```

---

## 関連ドキュメント

- [data-model.md](../data-model.md): エンティティの詳細定義（ConversationExecutionOptions / ReasoningEffortLevel を含む）
- [quickstart.md](../quickstart.md): 最小構成での動作確認手順
- [plan.md](../plan.md): 実装計画全体

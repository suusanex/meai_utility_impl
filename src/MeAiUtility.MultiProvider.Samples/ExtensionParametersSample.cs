using MeAiUtility.MultiProvider.Options;
using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Samples;

public static class ExtensionParametersSample
{
    public static ChatOptions CreateAzureOptions()
    {
        var ext = new ExtensionParameters();
        ext.Set("azure.data_sources", new[] { "search" });
        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        return options;
    }

    public static ChatOptions CreateCopilotOptions()
    {
        var exec = new ConversationExecutionOptions
        {
            ProviderOverride = new ProviderOverrideOptions { Type = "openai", BaseUrl = "https://api.openai.com/v1" },
        };

        var ext = new ExtensionParameters();
        ext.Set("copilot.mcp_servers", new { Name = "server" });

        var options = new ChatOptions();
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())[ConversationExecutionOptions.PropertyName] = exec;
        (options.AdditionalProperties ??= new Microsoft.Extensions.AI.AdditionalPropertiesDictionary())["meai.extensions"] = ext;
        return options;
    }
}



using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.AzureOpenAI.Configuration;

public static class AzureOpenAIServiceExtensions
{
    public static IServiceCollection AddAzureOpenAIProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var opts = configuration.GetSection("MultiProvider:AzureOpenAI").Get<AzureOpenAIProviderOptions>() ?? new AzureOpenAIProviderOptions();
        services.AddSingleton(opts);
        services.AddSingleton<AzureOpenAIChatClientAdapter>();
        services.AddSingleton<AzureOpenAIEmbeddingAdapter>();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp => sp.GetRequiredService<AzureOpenAIEmbeddingAdapter>());
        return services;
    }
}

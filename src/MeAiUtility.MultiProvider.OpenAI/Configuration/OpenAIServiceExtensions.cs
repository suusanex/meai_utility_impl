using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeAiUtility.MultiProvider.OpenAI.Configuration;

public static class OpenAIServiceExtensions
{
    public static IServiceCollection AddOpenAIProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var openAi = configuration.GetSection("MultiProvider:OpenAI").Get<OpenAIProviderOptions>() ?? new OpenAIProviderOptions();
        var compatible = configuration.GetSection("MultiProvider:OpenAICompatible").Get<OpenAICompatibleProviderOptions>() ?? new OpenAICompatibleProviderOptions();

        services.AddSingleton(openAi);
        services.AddSingleton(compatible);
        services.AddSingleton<OpenAIChatClientAdapter>();
        services.AddSingleton<OpenAICompatibleProvider>();
        services.AddSingleton<OpenAIEmbeddingAdapter>();
        services.AddSingleton<OpenAICompatibleEmbeddingAdapter>();

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var root = configuration.GetSection("MultiProvider");
            var provider = root.GetValue<string>("Provider");
            return provider == "OpenAICompatible"
                ? sp.GetRequiredService<OpenAICompatibleEmbeddingAdapter>()
                : sp.GetRequiredService<OpenAIEmbeddingAdapter>();
        });

        return services;
    }
}

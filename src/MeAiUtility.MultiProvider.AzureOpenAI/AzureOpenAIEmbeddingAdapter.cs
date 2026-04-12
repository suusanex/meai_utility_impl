extern alias OfficialMeAi;
extern alias OfficialMeAiOpenAI;

using Azure.AI.OpenAI;
using Azure.Identity;
using MeAiUtility.MultiProvider.AzureOpenAI.Options;
using MeAiUtility.MultiProvider.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OfficialEmbeddingGenerator = OfficialMeAi::Microsoft.Extensions.AI.IEmbeddingGenerator<string, OfficialMeAi::Microsoft.Extensions.AI.Embedding<float>>;
using System.ClientModel;

namespace MeAiUtility.MultiProvider.AzureOpenAI;

public sealed class AzureOpenAIEmbeddingAdapter(ILogger<AzureOpenAIEmbeddingAdapter> logger, AzureOpenAIProviderOptions options) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly ILogger<AzureOpenAIEmbeddingAdapter> _logger = logger;
    private readonly AzureOpenAIProviderOptions _options = options;
    private readonly Func<string, CancellationToken, Task<Embedding<float>>> _generateEmbeddingAsyncInvoker = CreateGenerateEmbeddingAsyncInvoker(CreateInnerGenerator(options));

    internal AzureOpenAIEmbeddingAdapter(
        ILogger<AzureOpenAIEmbeddingAdapter> logger,
        AzureOpenAIProviderOptions options,
        Func<string, CancellationToken, Task<Embedding<float>>> generateEmbeddingAsyncInvoker)
        : this(logger, options)
    {
        _generateEmbeddingAsyncInvoker = generateEmbeddingAsyncInvoker;
    }

    public async Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutCts = AzureOpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

        try
        {
            return await _generateEmbeddingAsyncInvoker(input, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw AzureOpenAIProviderExecution.CreateTimeout(_logger, _options.TimeoutSeconds, ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            throw AzureOpenAIProviderExecution.MapFailure(_logger, ex);
        }
    }

    private static OfficialEmbeddingGenerator CreateInnerGenerator(AzureOpenAIProviderOptions options)
    {
        options.Authentication.Validate();

        var endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        var clientOptions = AzureOpenAIOfficialBridge.CreateClientOptions(options.ApiVersion);
        AzureOpenAIClient client = options.Authentication.Type switch
        {
            AuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(options.Authentication.ApiKey!), clientOptions),
            AuthenticationType.EntraId => new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions),
            _ => throw new InvalidOperationException("Unsupported Azure OpenAI authentication type."),
        };

        var embeddingClient = client.GetEmbeddingClient(options.DeploymentName);
        return OfficialMeAiOpenAI::Microsoft.Extensions.AI.OpenAIClientExtensions.AsIEmbeddingGenerator(embeddingClient, null);
    }

    private static Func<string, CancellationToken, Task<Embedding<float>>> CreateGenerateEmbeddingAsyncInvoker(OfficialEmbeddingGenerator innerGenerator)
    {
        return async (input, cancellationToken) =>
        {
            var embedding = await OfficialMeAi::Microsoft.Extensions.AI.EmbeddingGeneratorExtensions.GenerateAsync(innerGenerator, input, null, cancellationToken);
            return new Embedding<float>(embedding.Vector.ToArray());
        };
    }
}

extern alias OfficialMeAi;
extern alias OfficialMeAiOpenAI;

using System.ClientModel;
using MeAiUtility.MultiProvider.Exceptions;
using MeAiUtility.MultiProvider.OpenAI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OfficialEmbeddingGenerator = OfficialMeAi::Microsoft.Extensions.AI.IEmbeddingGenerator<string, OfficialMeAi::Microsoft.Extensions.AI.Embedding<float>>;

namespace MeAiUtility.MultiProvider.OpenAI;

public sealed class OpenAIEmbeddingAdapter(ILogger<OpenAIEmbeddingAdapter> logger, OpenAIProviderOptions options) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly ILogger<OpenAIEmbeddingAdapter> _logger = logger;
    private readonly OpenAIProviderOptions _options = options;
    private readonly Func<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken, Task<GeneratedEmbeddings<Embedding<float>>>> _generateAsyncInvoker = CreateGenerateAsyncInvoker(CreateInnerGenerator(options));

    internal OpenAIEmbeddingAdapter(
        ILogger<OpenAIEmbeddingAdapter> logger,
        OpenAIProviderOptions options,
        Func<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken, Task<GeneratedEmbeddings<Embedding<float>>>> generateAsyncInvoker)
        : this(logger, options)
    {
        _generateAsyncInvoker = generateAsyncInvoker;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> inputs, EmbeddingGenerationOptions? optionsArg, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

        try
        {
            return await _generateAsyncInvoker(inputs, optionsArg, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw OpenAIProviderExecution.CreateTimeout(_logger, "OpenAI", _options.TimeoutSeconds, ex);
        }
        catch (Exception ex) when (ex is not MultiProviderException)
        {
            throw OpenAIProviderExecution.MapFailure(_logger, ex, "OpenAI");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static OfficialEmbeddingGenerator CreateInnerGenerator(OpenAIProviderOptions options)
    {
        options.Validate();

        var clientOptions = OpenAIOfficialBridge.CreateClientOptions(options.BaseUrl, options.OrganizationId, options.TimeoutSeconds);

        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        var embeddingClient = client.GetEmbeddingClient(options.ModelName);
        return OfficialMeAiOpenAI::Microsoft.Extensions.AI.OpenAIClientExtensions.AsIEmbeddingGenerator(embeddingClient, null);
    }

    private static Func<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken, Task<GeneratedEmbeddings<Embedding<float>>>> CreateGenerateAsyncInvoker(OfficialEmbeddingGenerator innerGenerator)
    {
        return async (inputs, optionsArg, cancellationToken) =>
        {
            var embeddings = await innerGenerator.GenerateAsync(inputs, optionsArg, cancellationToken);
            return new GeneratedEmbeddings<Embedding<float>>(embeddings.Select(static embedding => new Embedding<float>(embedding.Vector.ToArray())));
        };
    }
}

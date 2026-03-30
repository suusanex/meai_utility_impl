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
    private readonly Func<string, CancellationToken, Task<Embedding<float>>> _generateEmbeddingAsyncInvoker = CreateGenerateEmbeddingAsyncInvoker(CreateInnerGenerator(options));

    internal OpenAIEmbeddingAdapter(
        ILogger<OpenAIEmbeddingAdapter> logger,
        OpenAIProviderOptions options,
        Func<string, CancellationToken, Task<Embedding<float>>> generateEmbeddingAsyncInvoker)
        : this(logger, options)
    {
        _generateEmbeddingAsyncInvoker = generateEmbeddingAsyncInvoker;
    }

    public async Task<Embedding<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutCts = OpenAIProviderExecution.CreateTimeoutTokenSource(cancellationToken, _options.TimeoutSeconds);

        try
        {
            return await _generateEmbeddingAsyncInvoker(input, timeoutCts.Token);
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

    private static OfficialEmbeddingGenerator CreateInnerGenerator(OpenAIProviderOptions options)
    {
        options.Validate();

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(options.BaseUrl, UriKind.Absolute);
        }

        if (!string.IsNullOrWhiteSpace(options.OrganizationId))
        {
            clientOptions.OrganizationId = options.OrganizationId;
        }

        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        var embeddingClient = client.GetEmbeddingClient(options.ModelName);
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

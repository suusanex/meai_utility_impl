using System.Collections.Concurrent;
using System.Text.Json;
using MeAiUtility.MultiProvider.AzureOpenAI.Configuration;
using MeAiUtility.MultiProvider.Configuration;
using MeAiUtility.MultiProvider.OpenAI.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeAiUtility.MultiProvider.IntegrationTests.E2ETests;

public class OfficialSdkStubE2ETests
{
    [Test]
    [Category("StubE2E")]
    [NonParallelizable]
    public async Task OpenAI_UsesOfficialSdkThroughCommonInterfaces_AgainstStubServer()
    {
        await using var server = await OfficialSdkStubServer.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiProvider:Provider"] = "OpenAI",
                ["MultiProvider:OpenAI:ApiKey"] = "test-key",
                ["MultiProvider:OpenAI:BaseUrl"] = new Uri(server.BaseAddress, "/v1").ToString().TrimEnd('/'),
                ["MultiProvider:OpenAI:ModelName"] = "gpt-4o-mini",
                ["MultiProvider:OpenAI:TimeoutSeconds"] = "30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddOpenAIProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var embeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Say stub openai.")]);
        var embeddings = await embeddingGenerator.GenerateAsync(["embed openai"], new EmbeddingGenerationOptions(), CancellationToken.None);
        var embedding = embeddings[0];

        Assert.That(response.Text, Is.EqualTo("stub chat response"));
        Assert.That(embedding.Vector.ToArray(), Is.EqualTo(new[] { 0.25f, 0.5f, 0.75f }));
        Assert.That(server.Requests.Any(static request => request.Path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(server.Requests.Any(static request => request.Path.Contains("embeddings", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(
            server.Requests.First(static request => request.Path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase)).Body,
            Does.Contain("Say stub openai."));
    }

    [Test]
    [Category("StubE2E")]
    [NonParallelizable]
    public async Task AzureOpenAI_UsesOfficialSdkThroughCommonInterfaces_AgainstStubServer()
    {
        await using var server = await OfficialSdkStubServer.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiProvider:Provider"] = "AzureOpenAI",
                ["MultiProvider:AzureOpenAI:Endpoint"] = server.BaseAddress.ToString().TrimEnd('/'),
                ["MultiProvider:AzureOpenAI:DeploymentName"] = "test-deployment",
                ["MultiProvider:AzureOpenAI:ApiVersion"] = "2024-06-01",
                ["MultiProvider:AzureOpenAI:TimeoutSeconds"] = "30",
                ["MultiProvider:AzureOpenAI:Authentication:Type"] = "ApiKey",
                ["MultiProvider:AzureOpenAI:Authentication:ApiKey"] = "test-key",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMultiProviderChat(configuration);
        services.AddAzureOpenAIProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var chatClient = provider.GetRequiredService<IChatClient>();
        var embeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Say stub azure.")]);
        var embeddings = await embeddingGenerator.GenerateAsync(["embed azure"], new EmbeddingGenerationOptions(), CancellationToken.None);
        var embedding = embeddings[0];

        Assert.That(response.Text, Is.EqualTo("stub chat response"));
        Assert.That(embedding.Vector.ToArray(), Is.EqualTo(new[] { 0.25f, 0.5f, 0.75f }));
        Assert.That(server.Requests.Any(static request => request.Path.Contains("/openai/deployments/test-deployment/chat/completions", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(server.Requests.Any(static request => request.Path.Contains("/openai/deployments/test-deployment/embeddings", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(
            server.Requests.First(static request => request.Path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase)).Query,
            Does.Contain("api-version=2024-06-01"));
    }

    private sealed class OfficialSdkStubServer(WebApplication app, Uri baseAddress) : IAsyncDisposable
    {
        private readonly WebApplication _app = app;
        private readonly ConcurrentQueue<RecordedRequest> _requests = new();

        public Uri BaseAddress { get; } = baseAddress;
        public IReadOnlyCollection<RecordedRequest> Requests => _requests.ToArray();

        public static async Task<OfficialSdkStubServer> StartAsync(CancellationToken cancellationToken = default)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var app = builder.Build();
            OfficialSdkStubServer? server = null;

            app.MapPost("/{**path}", async context =>
            {
                if (server is null)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }

                await server.HandlePostAsync(context);
            });

            await app.StartAsync(cancellationToken);

            var address = app.Services.GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .Single();

            server = new OfficialSdkStubServer(app, new Uri(address, UriKind.Absolute));
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private async Task HandlePostAsync(HttpContext context)
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var path = context.Request.Path.Value ?? "/";
            var query = context.Request.QueryString.Value ?? string.Empty;

            _requests.Enqueue(new RecordedRequest(path, query, body));

            context.Response.ContentType = "application/json";

            if (path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                var model = TryReadString(body, "model") ?? "stub-model";
                await context.Response.WriteAsync($$"""
                {
                  "id": "chatcmpl-stub",
                  "object": "chat.completion",
                  "created": 1735689600,
                  "model": "{{model}}",
                  "choices": [
                    {
                      "index": 0,
                      "message": {
                        "role": "assistant",
                        "content": "stub chat response"
                      },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 1,
                    "completion_tokens": 1,
                    "total_tokens": 2
                  }
                }
                """);
                return;
            }

            if (path.Contains("embeddings", StringComparison.OrdinalIgnoreCase))
            {
                var model = TryReadString(body, "model") ?? "stub-model";
                await context.Response.WriteAsync($$"""
                {
                  "object": "list",
                  "data": [
                    {
                      "object": "embedding",
                      "index": 0,
                      "embedding": [0.25, 0.5, 0.75]
                    }
                  ],
                  "model": "{{model}}",
                  "usage": {
                    "prompt_tokens": 1,
                    "total_tokens": 1
                  }
                }
                """);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("""{"error":{"message":"Unknown stub route."}}""");
        }

        private static string? TryReadString(string body, string propertyName)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
    }

    private sealed record RecordedRequest(string Path, string Query, string Body);
}

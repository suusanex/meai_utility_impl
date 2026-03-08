using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Samples;

public static class BasicChatSample
{
    public static async Task<string> RunAsync(IChatClient chatClient, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")], cancellationToken: cancellationToken);
        return response.Message.Text;
    }
}

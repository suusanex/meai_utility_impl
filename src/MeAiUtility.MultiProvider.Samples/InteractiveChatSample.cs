using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Samples;

public static class InteractiveChatSample
{
    public static async Task RunAsync(IChatClient chatClient, TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var conversation = new List<ChatMessage>();

        await output.WriteLineAsync("Interactive chat started. Press Ctrl+C to exit.");
        await output.WriteLineAsync("Commands: /clear resets the conversation, /exit ends the sample.");
        await output.WriteLineAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            await output.WriteAsync("You: ");
            var line = await input.ReadLineAsync();
            if (line is null)
            {
                await output.WriteLineAsync();
                break;
            }

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (string.Equals(line, "/exit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "/quit", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Goodbye.");
                break;
            }

            if (string.Equals(line, "/clear", StringComparison.OrdinalIgnoreCase))
            {
                conversation.Clear();
                await output.WriteLineAsync("Conversation cleared.");
                await output.WriteLineAsync();
                continue;
            }

            conversation.Add(new ChatMessage(ChatRole.User, line));
            var response = await chatClient.GetResponseAsync(conversation, cancellationToken: cancellationToken);
            var responseText = response.Text;

            await output.WriteLineAsync();
            await output.WriteLineAsync($"Assistant: {responseText}");
            await output.WriteLineAsync();

            conversation.Add(new ChatMessage(ChatRole.Assistant, responseText));
        }
    }
}

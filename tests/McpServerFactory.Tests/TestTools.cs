using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServerFactory.Tests;

/// <summary>
/// Shared tool types and test doubles used across the test suite (avoids duplicating EchoTools per file).
/// </summary>
[McpServerToolType]
internal sealed class EchoTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message)
    {
        return message;
    }
}

internal interface IMessageProvider
{
    string GetMessage();
}

internal sealed class FixedMessageProvider(string message) : IMessageProvider
{
    public string GetMessage()
    {
        return message;
    }
}

[McpServerToolType]
internal sealed class GreetingTools(IMessageProvider messageProvider)
{
    [McpServerTool(Name = "greet")]
    public string Greet()
    {
        return messageProvider.GetMessage();
    }
}

[McpServerToolType]
internal sealed class ThrowingTools
{
    [McpServerTool(Name = "boom")]
    public string Boom()
    {
        throw new InvalidOperationException("boom failed");
    }
}

[McpServerToolType]
internal sealed class SamplingTools
{
    [McpServerTool(Name = "ask")]
    public static async Task<string> AskAsync(McpServer server, string question, CancellationToken cancellationToken)
    {
        CreateMessageResult result = await server.SampleAsync(
            new CreateMessageRequestParams
            {
                Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = question }] }],
                MaxTokens = 64,
            },
            cancellationToken);

        return result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "(no text)";
    }
}

internal sealed record PointDto(int X, int Y);

[McpServerToolType]
internal sealed class StructuredTools
{
    [McpServerTool(Name = "point")]
    public PointDto Point(int x, int y)
    {
        return new PointDto(x, y);
    }
}
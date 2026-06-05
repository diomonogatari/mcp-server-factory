using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerFactory.Tests;

public class ServerInitiatedFeatureTests
{
    [Fact]
    public async Task ServerInitiatedSampling_IsSatisfiedByFakeSamplingHandler()
    {
        FakeSamplingHandler sampling = FakeSamplingHandler.Returning("42");

        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<SamplingTools>(),
            options: new McpServerFactoryOptions
            {
                ConfigureClient = client => client.UseSamplingHandler(sampling),
            });

        McpTestClient client = await factory.CreateTestClientAsync();

        string answer = await client.CallToolForTextAsync(
            "ask",
            new Dictionary<string, object?> { ["question"] = "what is the answer?" });

        Assert.Equal("42", answer);
        Assert.Single(sampling.ReceivedRequests);
    }

    [Fact]
    public async Task ConfigureHost_RunsRealCompositionRoot_ServiceIsResolvable()
    {
        FixedMessageProvider provider = new("from-configure-host");

        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<GreetingTools>(),
            options: new McpServerFactoryOptions
            {
                ConfigureHost = builder => builder.Services.AddSingleton<IMessageProvider>(provider),
            });

        McpTestClient client = await factory.CreateTestClientAsync();

        Assert.Equal("from-configure-host", await client.CallToolForTextAsync("greet"));
        Assert.Same(provider, factory.Services.GetRequiredService<IMessageProvider>());
    }
}
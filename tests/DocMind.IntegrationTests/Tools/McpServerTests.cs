using System.IO.Pipelines;
using System.Text.Json;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.McpServer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DocMind.IntegrationTests.Tools;

[Collection(PostgresFixture.CollectionName)]
public sealed class McpServerTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    private const string VacationText = "Employees receive twenty five vacation days per year and may carry over five days.";

    private IHost? _mcpHost;
    private McpClient? _client;

    public override async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        if (_mcpHost is not null)
        {
            await _mcpHost.StopAsync(TestContext.Current.CancellationToken);
            _mcpHost.Dispose();
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task<McpClient> ConnectAsync(Guid userId)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:docmind"] = ConnectionString,
            ["McpServer:UserId"] = userId.ToString(),
            ["Storage:Local:RootPath"] = StorageRoot,
            ["Summary:MaxCharacters"] = "1000",
        });

        builder.Services
            .AddDocMindMcpServer(builder.Configuration)
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());

        builder.Services.RemoveAll<IChatClient>();
        builder.Services.AddSingleton<IChatClient>(Chat);
        builder.Services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);

        _mcpHost = builder.Build();
        await _mcpHost.StartAsync(TestContext.Current.CancellationToken);

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: TestContext.Current.CancellationToken);

        return _client;
    }

    private static JsonElement ResultJson(CallToolResult result)
    {
        result.IsError.Should().NotBe(true);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        return JsonDocument.Parse(text).RootElement;
    }

    [Fact]
    public async Task Server_ListsTheSameTwoToolsAsTheChat()
    {
        var session = await Client.RegisterAndLoginAsync();
        var mcp = await ConnectAsync(session.UserId);

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        tools.Select(t => t.Name).Should().BeEquivalentTo("search_documents", "summarize_document");
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description));
        tools.Single(t => t.Name == "search_documents").JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("query", "maxResults");
    }

    [Fact]
    public async Task SearchTool_ReturnsTheConfiguredUsersPassagesOnly()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var other = await Client.RegisterAndLoginAsync();
        var ownerDocument = await UploadPdfAsync(owner, "vacation.pdf", VacationText);
        await UploadPdfAsync(other, "foreign.pdf", VacationText);
        await DrainQueueAsync();
        var mcp = await ConnectAsync(owner.UserId);

        var result = await mcp.CallToolAsync(
            "search_documents",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["query"] = "vacation days employees receive" },
            cancellationToken: TestContext.Current.CancellationToken);

        var hits = ResultJson(result).EnumerateArray().ToList();
        hits.Should().ContainSingle();
        hits[0].GetProperty("documentId").GetGuid().Should().Be(ownerDocument);
        hits[0].GetProperty("documentName").GetString().Should().Be("vacation.pdf");
    }

    [Fact]
    public async Task SummarizeTool_SummarizesTheConfiguredUsersDocument()
    {
        var session = await Client.RegisterAndLoginAsync();
        var document = await UploadPdfAsync(session, "policy.pdf", VacationText);
        await DrainQueueAsync();
        Chat.Behavior = (_, _, _) => FakeChatClient.Sequence(FakeChatClient.Token("An MCP summary."));
        var mcp = await ConnectAsync(session.UserId);

        var result = await mcp.CallToolAsync(
            "summarize_document",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["documentId"] = document.ToString() },
            cancellationToken: TestContext.Current.CancellationToken);

        var json = ResultJson(result);
        json.GetProperty("summary").GetString().Should().Be("An MCP summary.");
        json.GetProperty("documentId").GetGuid().Should().Be(document);
    }

    [Fact]
    public async Task SummarizeTool_CannotReachDocumentsOfOtherUsers()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var caller = await Client.RegisterAndLoginAsync();
        var foreign = await UploadPdfAsync(owner, "secret.pdf", VacationText);
        await DrainQueueAsync();
        var mcp = await ConnectAsync(caller.UserId);

        var result = await mcp.CallToolAsync(
            "summarize_document",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["documentId"] = foreign.ToString() },
            cancellationToken: TestContext.Current.CancellationToken);

        var json = ResultJson(result);
        json.TryGetProperty("summary", out _).Should().BeFalse();
        json.GetProperty("error").GetString().Should().Contain("was not found");
        Chat.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Server_RefusesToStartWithoutAUserIdentifier()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:docmind"] = ConnectionString,
            ["Storage:Local:RootPath"] = StorageRoot,
        });
        builder.Services.AddDocMindMcpServer(builder.Configuration).WithStreamServerTransport(new MemoryStream(), new MemoryStream());
        using var host = builder.Build();

        var start = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain("McpServer:UserId");
    }
}

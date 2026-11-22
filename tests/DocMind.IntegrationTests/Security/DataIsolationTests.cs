using System.Net;
using DocMind.Application.Features.Documents;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DocMind.IntegrationTests.Security;

[Collection(PostgresFixture.CollectionName)]
public sealed class DataIsolationTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    private const string Secret = "The merger with Contoso will be announced on the fourteenth of March.";
    private const string SecretQuestion = "When will the merger with Contoso be announced?";

    private sealed record World(TestSession Alice, TestSession Mallory, Guid AliceConversation, Guid AliceDocument);

    private async Task<World> ArrangeAsync()
    {
        var alice = await Client.RegisterAndLoginAsync(displayName: "Alice");
        var mallory = await Client.RegisterAndLoginAsync(displayName: "Mallory");
        var document = await UploadPdfAsync(alice, "confidential.pdf", Secret);
        await DrainQueueAsync();
        var conversation = await Client.CreateConversationAsync(alice, "Alice private chat");
        using var response = await Client.SendChatAsync(alice, conversation, SecretQuestion);
        await SseReader.ReadAsync(response);
        Chat.Reset();

        return new World(alice, mallory, conversation, document);
    }

    private static (HttpStatusCode Status, string? Code) Outcome(HttpResponseMessage response, System.Text.Json.JsonElement? body) =>
        (response.StatusCode, body?.TryGetProperty("code", out var code) == true ? code.GetString() : null);

    private async Task<(HttpStatusCode, string?)> SendAsync(TestSession session, HttpMethod method, string uri, object? body = null)
    {
        using var response = await Client.SendAsync(session, method, uri, body is null ? null : JsonContent.Create(body));
        var json = response.Content.Headers.ContentType?.MediaType == "application/problem+json" ? await response.ReadJsonAsync() : (System.Text.Json.JsonElement?)null;
        return Outcome(response, json);
    }

    [Fact]
    public async Task ForeignConversations_AreIndistinguishableFromMissingOnes()
    {
        var world = await ArrangeAsync();
        var missing = Guid.NewGuid();

        foreach (var (method, suffix, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Get, string.Empty, null),
            (HttpMethod.Patch, string.Empty, new { title = "hijack" }),
            (HttpMethod.Delete, string.Empty, null),
            (HttpMethod.Post, "/messages", new { content = "hello" }),
        })
        {
            var foreign = await SendAsync(world.Mallory, method, $"/api/v1/conversations/{world.AliceConversation}{suffix}", body);
            var absent = await SendAsync(world.Mallory, method, $"/api/v1/conversations/{missing}{suffix}", body);

            foreign.Should().Be((HttpStatusCode.NotFound, "conversations.not_found"), $"{method}{suffix}");
            foreign.Should().Be(absent, "a foreign resource must look exactly like a missing one");
        }

        Chat.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task ForeignDocuments_AreIndistinguishableFromMissingOnes()
    {
        var world = await ArrangeAsync();
        var missing = Guid.NewGuid();

        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Delete })
        {
            var foreign = await SendAsync(world.Mallory, method, $"/api/v1/documents/{world.AliceDocument}");
            var absent = await SendAsync(world.Mallory, method, $"/api/v1/documents/{missing}");

            foreign.Should().Be((HttpStatusCode.NotFound, "documents.not_found"));
            foreign.Should().Be(absent);
        }

        (await GetDocumentAsync(world.Alice, world.AliceDocument)).GetProperty("status").GetString().Should().Be("Ready");
    }

    [Fact]
    public async Task Listings_NeverExposeForeignItems()
    {
        var world = await ArrangeAsync();
        var malloryDocument = await UploadPdfAsync(world.Mallory, "mine.pdf", "Mallory's own notes.");
        var malloryConversation = await Client.CreateConversationAsync(world.Mallory, "Mallory chat");

        var documents = await (await Client.GetAsync(world.Mallory, "/api/v1/documents")).ReadJsonAsync();
        var conversations = await (await Client.GetAsync(world.Mallory, "/api/v1/conversations")).ReadJsonAsync();

        documents.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(malloryDocument);
        documents.GetProperty("totalCount").GetInt64().Should().Be(1);
        conversations.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Equal(malloryConversation);
    }

    [Fact]
    public async Task TheAssistantNeverRevealsAnotherUsersDocumentsEvenWhenAskedDirectly()
    {
        var world = await ArrangeAsync();
        var conversation = await Client.CreateConversationAsync(world.Mallory);

        using var response = await Client.SendChatAsync(world.Mallory, conversation, SecretQuestion);
        var transcript = await SseReader.ReadAsync(response);

        Chat.Calls.Should().ContainSingle();
        Chat.Calls[0].Messages.Select(m => m.Text).Should().NotContain(text => text.Contains("fourteenth", StringComparison.Ordinal));
        transcript.Named("citation").Should().BeEmpty();
        transcript.Text.Should().NotContain("fourteenth");
    }

    [Fact]
    public async Task SelectingAnotherUsersDocumentForAChatIsRefusedWithoutCallingTheModel()
    {
        var world = await ArrangeAsync();
        var conversation = await Client.CreateConversationAsync(world.Mallory);

        var embeddedBefore = Embeddings.Inputs.Count;

        using var response = await Client.SendChatAsync(world.Mallory, conversation, SecretQuestion, [world.AliceDocument]);

        response.ShouldBeProblem(HttpStatusCode.NotFound);
        Chat.Calls.Should().BeEmpty();
        Embeddings.Inputs.Should().HaveCount(embeddedBefore);
    }

    [Fact]
    public async Task CachedSummariesAreNeverServedToOtherUsers()
    {
        var world = await ArrangeAsync();
        Chat.Behavior = (_, _, _) => FakeChatClient.Sequence(FakeChatClient.Token("A confidential summary."));

        await using (var aliceScope = Factory.Services.CreateAsyncScope())
        {
            var warm = await aliceScope.ServiceProvider.GetRequiredService<SummarizeDocumentHandler>()
                .HandleAsync(world.Alice.UserId, world.AliceDocument, TestContext.Current.CancellationToken);
            warm.IsSuccess.Should().BeTrue();
        }

        await using var malloryScope = Factory.Services.CreateAsyncScope();
        var stolen = await malloryScope.ServiceProvider.GetRequiredService<SummarizeDocumentHandler>()
            .HandleAsync(world.Mallory.UserId, world.AliceDocument, TestContext.Current.CancellationToken);

        stolen.IsFailure.Should().BeTrue();
        stolen.Error.Code.Should().Be("documents.not_found");
    }

    [Fact]
    public async Task UploadsAreStoredInPerUserFoldersAndDeletionsStayInTheirOwnerSpace()
    {
        var alice = await Client.RegisterAndLoginAsync();
        var mallory = await Client.RegisterAndLoginAsync();
        var aliceDocument = await UploadPdfAsync(alice, "same-name.pdf", "alice content");
        var malloryDocument = await UploadPdfAsync(mallory, "same-name.pdf", "mallory content");

        var aliceKey = (await LoadDocumentAsync(aliceDocument)).StorageKey;
        var malloryKey = (await LoadDocumentAsync(malloryDocument)).StorageKey;

        aliceKey.Should().Contain(alice.UserId.ToString("N")).And.NotContain(mallory.UserId.ToString("N"));
        malloryKey.Should().Contain(mallory.UserId.ToString("N")).And.NotContain(alice.UserId.ToString("N"));

        (await Client.SendAsync(mallory, HttpMethod.Delete, $"/api/v1/documents/{malloryDocument}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(Path.Combine(StorageRoot, aliceKey.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
    }

    [Fact]
    public async Task ConversationHistoryNeverLeaksIntoAnotherUsersPrompt()
    {
        var world = await ArrangeAsync();
        var conversation = await Client.CreateConversationAsync(world.Mallory);

        using var response = await Client.SendChatAsync(world.Mallory, conversation, "What did we discuss before?");
        await SseReader.ReadAsync(response);

        Chat.Calls.Single().Messages.Select(m => m.Text).Should().NotContain(text => text.Contains("merger", StringComparison.OrdinalIgnoreCase));
        Chat.Calls.Single().Messages.Where(m => m.Role == ChatRole.User).Should().ContainSingle();
    }
}

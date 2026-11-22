using System.Net;
using DocMind.IntegrationTests.Infrastructure;

namespace DocMind.IntegrationTests.Conversations;

[Collection(PostgresFixture.CollectionName)]
public sealed class ConversationsEndpointsTests(PostgresFixture postgres) : ChatApiTestBase(postgres)
{
    [Fact]
    public async Task Create_ReturnsTheConversationWithALocation()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.PostJsonAsync(session, "/api/v1/conversations", new { title = "Budget review" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().Be($"/api/v1/conversations/{id}");
        body.GetProperty("title").GetString().Should().Be("Budget review");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().Be(Time.GetUtcNow());
    }

    [Fact]
    public async Task Create_WithoutABodyUsesTheDefaultTitle()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.SendAsync(session, HttpMethod.Post, "/api/v1/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("title").GetString().Should().Be("New conversation");
    }

    [Fact]
    public async Task Create_RejectsAnOverlongTitle()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.PostJsonAsync(session, "/api/v1/conversations", new { title = new string('t', 201) });

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("title", out _).Should().BeTrue();
    }

    [Fact]
    public async Task List_ReturnsOnlyMyConversationsMostRecentlyUpdatedFirst()
    {
        var me = await Client.RegisterAndLoginAsync();
        var other = await Client.RegisterAndLoginAsync();

        var first = await Client.CreateConversationAsync(me, "first");
        Time.Advance(TimeSpan.FromMinutes(1));
        var second = await Client.CreateConversationAsync(me, "second");
        await Client.CreateConversationAsync(other, "not mine");

        var response = await Client.GetAsync(me, "/api/v1/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.ReadJsonAsync()).EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        ids.Should().Equal(second, first);
    }

    [Fact]
    public async Task Get_ReturnsTheConversationWithItsMessagesInOrder()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);
        using var response = await Client.SendChatAsync(session, id, "hello there");
        await SseReader.ReadAsync(response);

        var detail = await Client.GetAsync(session, $"/api/v1/conversations/{id}");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = (await detail.ReadJsonAsync()).GetProperty("messages").EnumerateArray().ToList();
        messages.Select(m => m.GetProperty("role").GetString()).Should().Equal("User", "Assistant");
        messages[0].GetProperty("content").GetString().Should().Be("hello there");
        messages[1].GetProperty("content").GetString().Should().StartWith("Echo: hello there");
        messages[1].GetProperty("citations").GetArrayLength().Should().Be(0);
        messages[1].GetProperty("usage").GetProperty("inputTokens").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task Rename_UpdatesTheTitle()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session, "old");

        var response = await Client.SendAsync(session, HttpMethod.Patch, $"/api/v1/conversations/{id}", JsonContent.Create(new { title = "new title" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("title").GetString().Should().Be("new title");
    }

    [Fact]
    public async Task Rename_RejectsAnEmptyTitle()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        var response = await Client.SendAsync(session, HttpMethod.Patch, $"/api/v1/conversations/{id}", JsonContent.Create(new { title = " " }));

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesTheConversationAndItsMessages()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);
        using var chat = await Client.SendChatAsync(session, id, "to be deleted");
        await SseReader.ReadAsync(chat);

        var response = await Client.SendAsync(session, HttpMethod.Delete, $"/api/v1/conversations/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync(session, $"/api/v1/conversations/{id}")).ShouldBeProblem(HttpStatusCode.NotFound);

        await using var db = TestDatabase.CreateContext(ConnectionString);
        (await db.Messages.CountAsync(m => m.ConversationId == id, TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Conversations_AreInvisibleToOtherUsers()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var intruder = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(owner, "private");

        var get = await Client.GetAsync(intruder, $"/api/v1/conversations/{id}");
        var rename = await Client.SendAsync(intruder, HttpMethod.Patch, $"/api/v1/conversations/{id}", JsonContent.Create(new { title = "hijacked" }));
        var delete = await Client.SendAsync(intruder, HttpMethod.Delete, $"/api/v1/conversations/{id}");
        var chat = await Client.SendChatAsync(intruder, id, "let me in");

        get.ShouldBeProblem(HttpStatusCode.NotFound);
        rename.ShouldBeProblem(HttpStatusCode.NotFound);
        delete.ShouldBeProblem(HttpStatusCode.NotFound);
        chat.ShouldBeProblem(HttpStatusCode.NotFound);

        var untouched = await Client.GetAsync(owner, $"/api/v1/conversations/{id}");
        (await untouched.ReadJsonAsync()).GetProperty("title").GetString().Should().Be("private");
        Chat.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var id = Guid.NewGuid();

        var list = await Client.GetAsync("/api/v1/conversations", TestContext.Current.CancellationToken);
        var send = await Client.PostAsJsonAsync($"/api/v1/conversations/{id}/messages", new { content = "hi" }, TestContext.Current.CancellationToken);

        list.ShouldBeProblem(HttpStatusCode.Unauthorized);
        send.ShouldBeProblem(HttpStatusCode.Unauthorized);
    }
}

using System.Net.Http.Json;
using System.Text.Json;

namespace DocMind.IntegrationTests.Infrastructure;

public sealed record SseEvent(string Name, string Data)
{
    public JsonElement Json => JsonDocument.Parse(Data).RootElement;
}

public sealed record SseTranscript(IReadOnlyList<SseEvent> Events, IReadOnlyList<string> Comments)
{
    public IEnumerable<SseEvent> Named(string name) => Events.Where(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public string Text => string.Concat(Named("token").Select(e => e.Json.GetProperty("text").GetString()));
}

public static class SseReader
{
    public static async Task<SseTranscript> ReadAsync(HttpResponseMessage response)
    {
        var token = TestContext.Current.CancellationToken;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);

        var events = new List<SseEvent>();
        var comments = new List<string>();
        string? name = null;
        var data = new List<string>();

        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (line.Length == 0)
            {
                if (name is not null)
                {
                    events.Add(new SseEvent(name, string.Join('\n', data)));
                }

                name = null;
                data.Clear();
            }
            else if (line.StartsWith(':'))
            {
                comments.Add(line);
            }
            else if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data.Add(line["data: ".Length..]);
            }
        }

        return new SseTranscript(events, comments);
    }

    public static Task<HttpResponseMessage> SendChatAsync(
        this HttpClient client,
        TestSession session,
        Guid conversationId,
        string content,
        IEnumerable<Guid>? documentIds = null) =>
        client.SendChatAsync(session, conversationId, content, documentIds, HttpCompletionOption.ResponseContentRead, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SendChatAsync(
        this HttpClient client,
        TestSession session,
        Guid conversationId,
        string content,
        IEnumerable<Guid>? documentIds,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        var request = session.Authorized(
            HttpMethod.Post,
            $"/api/v1/conversations/{conversationId}/messages",
            JsonContent.Create(new { content, documentIds }));

        return client.SendAsync(request, completion, cancellationToken);
    }

    public static async Task<Guid> CreateConversationAsync(this HttpClient client, TestSession session, string? title = null)
    {
        var response = await client.PostJsonAsync(session, "/api/v1/conversations", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }
}

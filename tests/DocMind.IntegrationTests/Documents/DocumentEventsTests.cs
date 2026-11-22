using System.Net;
using System.Text.Json;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;

namespace DocMind.IntegrationTests.Documents;

[Collection(PostgresFixture.CollectionName)]
public sealed class DocumentEventsTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Events_StreamEveryStatusChangeOfMyDocuments()
    {
        var session = await Client.RegisterAndLoginAsync();
        await using var stream = await OpenEventsAsync(session);

        var id = await UploadPdfAsync(session, "live.pdf", TestDocuments.Sentences("streaming", 3));
        await DrainQueueAsync();

        var events = await stream.ReadAsync(3);

        events.Select(e => e.GetProperty("status").GetString()).Should().Equal("Pending", "Processing", "Ready");
        events.Should().OnlyContain(e => e.GetProperty("documentId").GetGuid() == id);
        events.Select(e => e.GetProperty("updatedAt").GetDateTimeOffset()).Should().OnlyContain(d => d.Year == 2026);
    }

    [Fact]
    public async Task Events_ReportFailuresWithTheirMessage()
    {
        var session = await Client.RegisterAndLoginAsync();
        await using var stream = await OpenEventsAsync(session);

        await UploadAsync(session, "scan.pdf", TestDocuments.BlankPdf(), TestDocuments.PdfContentType);
        await DrainQueueAsync();

        var events = await stream.ReadAsync(3);

        events.Select(e => e.GetProperty("status").GetString()).Should().Equal("Pending", "Processing", "Failed");
        events[2].GetProperty("errorMessage").GetString().Should().Contain("no extractable text");
    }

    [Fact]
    public async Task Events_NeverLeakOtherUsersDocuments()
    {
        var me = await Client.RegisterAndLoginAsync();
        var other = await Client.RegisterAndLoginAsync();
        await using var stream = await OpenEventsAsync(me);

        await UploadPdfAsync(other, "theirs.pdf", TestDocuments.Sentences("private", 2));
        var mine = await UploadPdfAsync(me, "mine.pdf", TestDocuments.Sentences("public", 2));
        await DrainQueueAsync();

        var events = await stream.ReadAsync(3);

        events.Should().OnlyContain(e => e.GetProperty("documentId").GetGuid() == mine);
    }

    [Fact]
    public async Task Events_UseTheEventStreamContentTypeAndKeepAlive()
    {
        var session = await Client.RegisterAndLoginAsync();
        await using var stream = await OpenEventsAsync(session);

        stream.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        stream.Response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        (await stream.WaitForCommentAsync(": keep-alive")).Should().BeTrue();
    }

    private async Task<EventStream> OpenEventsAsync(TestSession session)
    {
        var request = session.Authorized(HttpMethod.Get, "/api/v1/documents/events");
        var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return new EventStream(response, await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
    }

    private sealed class EventStream(HttpResponseMessage response, Stream stream) : IAsyncDisposable
    {
        private readonly StreamReader _reader = new(stream);

        public HttpResponseMessage Response => response;

        public async Task<List<JsonElement>> ReadAsync(int count)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(Timeout);

            var events = new List<JsonElement>();
            string? name = null;

            while (events.Count < count && await _reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal) && string.Equals(name, "status", StringComparison.Ordinal))
                {
                    events.Add(JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone());
                }
            }

            return events;
        }

        public async Task<bool> WaitForCommentAsync(string comment)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(Timeout);

            while (await _reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (string.Equals(line, comment, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public ValueTask DisposeAsync()
        {
            _reader.Dispose();
            response.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

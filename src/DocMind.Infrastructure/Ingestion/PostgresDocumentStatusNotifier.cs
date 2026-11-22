using System.Text.Json;
using DocMind.Application.Abstractions;
using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Infrastructure.Ingestion;

internal sealed class PostgresDocumentStatusNotifier(AppDbContext db) : IDocumentStatusNotifier
{
    public const string ChannelName = "document_status";
    public const int MaxErrorLength = 500;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task NotifyAsync(DocumentStatusChanged change, CancellationToken cancellationToken)
    {
        var trimmed = change.ErrorMessage is { Length: > MaxErrorLength } error ? error[..MaxErrorLength] : change.ErrorMessage;
        var payload = JsonSerializer.Serialize(change with { ErrorMessage = trimmed }, JsonOptions);

        await db.Database.ExecuteSqlAsync($"SELECT pg_notify({ChannelName}, {payload})", cancellationToken);
    }
}

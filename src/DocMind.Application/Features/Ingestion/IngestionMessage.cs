using System.Text.Json;

namespace DocMind.Application.Features.Ingestion;

public sealed record IngestionMessage(Guid DocumentId, Guid UserId)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public static IngestionMessage? TryParse(string body)
    {
        try
        {
            var message = JsonSerializer.Deserialize<IngestionMessage>(body, Options);
            return message is not null && message.DocumentId != Guid.Empty && message.UserId != Guid.Empty ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

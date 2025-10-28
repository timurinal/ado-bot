using LiteDB;

namespace AdoBot.Data;

public record StrikeRecord
{
    [BsonId]                           // Upsert will match on this
    public string Id => $"{GuildId}:{UserId}";

    public ulong GuildId { get; init; }
    public ulong UserId  { get; init; }
    public int   Count   { get; init; }
    public string? Notes { get; init; }
    public DateTime UpdatedAt { get; init; }
}
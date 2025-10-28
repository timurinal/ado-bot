using LiteDB;

namespace AdoBot.Data;

/// <summary>
/// Manages the bot’s main database and provides typed access to collections.
/// </summary>
public sealed class BotDatabase : IDisposable
{
    public static BotDatabase Instance { get; private set; }

    public static void Init() => Instance = new BotDatabase();
    public static void Destroy() => Instance?.Dispose();
    
    private readonly LiteDatabase _db;

    public ILiteCollection<StrikeRecord> Strikes { get; }
    public BotDatabase(string path = "data/bot.db")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _db = new LiteDatabase(path);

        Strikes = _db.GetCollection<StrikeRecord>("strikes");
        Strikes.EnsureIndex(x => new { x.GuildId, x.UserId }); // not unique
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Utility accessor if you need a custom collection not predefined above.
    /// </summary>
    public ILiteCollection<T> GetCollection<T>(string name) where T : new()
        => _db.GetCollection<T>(name);
}
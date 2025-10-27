using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetCord;

namespace AdoBot;

public class RssWatcher
{
    public static bool ThreadAlive;
    
    private static readonly HttpClient _http = new HttpClient();
    private readonly string _statePath;
    private WatchState _state;

    public RssWatcher(string stateFilePath = "yt_rss_state.json")
    {
        _statePath = stateFilePath;
        _state = File.Exists(_statePath)
            ? JsonSerializer.Deserialize<WatchState>(File.ReadAllText(_statePath)) ?? new WatchState()
            : new WatchState();
    }

    public static async Task StartThread()
    {
        RssWatcher watcher = new RssWatcher();
        
        while (ThreadAlive)
        {
            await watcher.CheckAsync(Config.Id);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
        
        Console.WriteLine("Exiting thread.");
    }

    public async Task CheckAsync(string channelId, CancellationToken ct = default)
    {
        var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
        Console.WriteLine($"Checking {channelId} at {feedUrl}");

        // Optional: send conditional headers if we’ve got them.
        if (_state.ETags.TryGetValue(channelId, out var etag))
        {
            _http.DefaultRequestHeaders.IfNoneMatch.Clear();
            _http.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        }

        if (_state.LastModified.TryGetValue(channelId, out var lastMod))
            _http.DefaultRequestHeaders.IfModifiedSince = lastMod;

        using var resp = await _http.GetAsync(feedUrl, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
            return; // Nothing changed.

        resp.EnsureSuccessStatusCode();

        // Track fresh caching headers.
        if (resp.Headers.ETag != null)
            _state.ETags[channelId] = resp.Headers.ETag.Tag!;
        if (resp.Content.Headers.LastModified.HasValue)
            _state.LastModified[channelId] = resp.Content.Headers.LastModified.Value;

        var xml = await resp.Content.ReadAsStringAsync(ct);

        var doc = XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
        // Fallback older namespace sometimes appears:
        XNamespace media = "http://search.yahoo.com/mrss/";

        var entries = doc.Root!
            .Elements(atom + "entry")
            .Select(e => new FeedItem
            {
                VideoId = e.Element(yt + "videoId")?.Value ?? "",
                Title = e.Element(atom + "title")?.Value ?? "(untitled)",
                Link = e.Element(atom + "link")?.Attribute("href")?.Value
                       ?? e.Elements(atom + "link").FirstOrDefault()?.Attribute("href")?.Value
                       ?? "",
                Published = ParseDate(e.Element(atom + "published")?.Value),
                Updated = ParseDate(e.Element(atom + "updated")?.Value)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.VideoId))
            // Sort so you post in chronological order if multiple are new
            .OrderBy(i => i.Published ?? i.Updated ?? DateTimeOffset.MinValue)
            .ToList();

        var seen = _state.SeenIds.GetValueOrDefault(channelId) ?? new HashSet<string>();

        var newOnes = entries.Where(i => !seen.Contains(i.VideoId)).ToList();
        foreach (var item in newOnes)
        {
            Console.WriteLine($"New upload: **{item.Title}**\n{item.Link}");
            await Program.QueueMessageAsync(893159656459997235, item);
            seen.Add(item.VideoId);
        }

        // Keep the set from growing forever (optional).
        Trim(seen, maxItems: 500);

        _state.SeenIds[channelId] = seen;
        SaveState();
    }

    private static DateTimeOffset? ParseDate(string? s) =>
        DateTimeOffset.TryParse(s, out var dt) ? dt : null;

    private static void Trim(HashSet<string> set, int maxItems)
    {
        // No timestamps in the set, so only prune if it’s huge.
        // Alternative: store a queue next to the set to evict oldest IDs.
        if (set.Count <= maxItems) return;
        // Cheap prune: take first N (order not guaranteed). Fine for RSS use.
        foreach (var id in set.Skip(maxItems).ToList())
            set.Remove(id);
    }

    private void SaveState()
    {
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statePath, json);
    }

    // ---- State & models ----
    public class FeedItem
    {
        public string VideoId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Link { get; set; } = "";
        public DateTimeOffset? Published { get; set; }
        public DateTimeOffset? Updated { get; set; }
    }

    private class WatchState
    {
        public Dictionary<string, HashSet<string>> SeenIds { get; set; } = new();
        public Dictionary<string, string> ETags { get; set; } = new();
        public Dictionary<string, DateTimeOffset?> LastModified { get; set; } = new();
    }
}

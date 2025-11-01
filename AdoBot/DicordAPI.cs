using System.Diagnostics;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;

namespace AdoBot;

public static class DiscordAPI
{
    public static async Task PlayAsync(GatewayClient client)
    {
        string url = Config.DefaultRadioStream; // TODO: idk if I want to keep this hardcoded, but since bots cannot be in multiple voice channels, you'd constantly have the bot fighting for what radio station to play if choice was given.
                                                // I've at least made it not a compile time constant, so it _can_ be changed on the server. Eh maybe I'll make an admin command to change the stored URL but I am too lazy to do that right now.
           
        var voiceClient = await client.JoinVoiceChannelAsync(
            Config.Guild,
            Config.RadioChannel,
            new VoiceClientConfiguration { Logger = new ConsoleLogger() });

        await voiceClient.StartAsync();
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

        // Station name (falls back to host if absent)
        // var station = await GetStationNameAsync(url) ?? GetRadioDisplayName(url);

        var outStream = voiceClient.CreateOutputStream();
        OpusEncodeStream stream = new(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

        ProcessStartInfo startInfo = new("ffmpeg") { RedirectStandardOutput = true, };
        var arguments = startInfo.ArgumentList;
        List<string> args =
        [
            "-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5", "-vn", "-sn", "-dn", "-fflags",
            "nobuffer", "-flags", "low_delay", "-rw_timeout", "15000000", "-loglevel", "warning", "-i", url, "-ac", "2",
            "-f", "s16le", "-ar", "48000", "pipe:1"
        ];
        foreach (var a in args) arguments.Add(a);
        var ffmpeg = Process.Start(startInfo)!;
        
        // Start ICY watcher to update presence with now playing
        var cts = new CancellationTokenSource();
        _ = WatchIcyNowPlayingAsync(url, async title =>
        {
            Console.WriteLine($"Now playing: {title}");
            
            // Update bot presence: Listening to <title>
            await client.UpdatePresenceAsync(new PresenceProperties(UserStatusType.Online)
            {
                Activities =
                [
                    new UserActivityProperties(title, UserActivityType.Listening)
                    {
                        CreatedAt = DateTime.UtcNow,
                    }
                ],
                Since = DateTime.UtcNow,
            });
        }, cts.Token);
        
        await ffmpeg.StandardOutput.BaseStream
            .CopyToAsync(
                stream);
        await stream.FlushAsync();

        // TODO: store cts to cancel when user stops playback/leaves channel
    }
    
        private static async Task<string?> GetStationNameAsync(string url)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Icy-MetaData", "1"); // request ICY headers
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode) return null;

        if (resp.Headers.TryGetValues("icy-name", out var vals))
            return vals.FirstOrDefault();

        return null;
    }

    private static async Task WatchIcyNowPlayingAsync(string url, Func<string, Task> onTitleChanged,
        CancellationToken ct)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Icy-MetaData", "1");
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        if (!resp.Headers.TryGetValues("icy-metaint", out var metaintVals) ||
            !int.TryParse(metaintVals.FirstOrDefault(), out int metaint))
        {
            return; // no metadata available
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var audioBuffer = new byte[metaint];
        string? last = null;

        while (!ct.IsCancellationRequested)
        {
            // skip audio bytes
            int read = await stream.ReadAsync(audioBuffer, 0, metaint, ct);
            if (read <= 0) break;

            // read metadata length (in 16-byte blocks)
            int metaLenBlocks = stream.ReadByte();
            if (metaLenBlocks <= 0) continue;
            int metaLen = metaLenBlocks * 16;

            var metaBuf = new byte[metaLen];
            int got = 0;
            while (got < metaLen)
            {
                int r = await stream.ReadAsync(metaBuf, got, metaLen - got, ct);
                if (r <= 0) break;
                got += r;
            }

            var meta = System.Text.Encoding.ASCII.GetString(metaBuf);
            const string key = "StreamTitle='";
            int i = meta.IndexOf(key, StringComparison.Ordinal);
            if (i >= 0)
            {
                int start = i + key.Length;
                int end = meta.IndexOf("';", start, StringComparison.Ordinal);
                if (end > start)
                {
                    var title = meta[start..end].Trim();
                    if (!string.IsNullOrEmpty(title) && !string.Equals(title, last, StringComparison.Ordinal))
                    {
                        last = title;
                        await onTitleChanged(title);
                    }
                }
            }
        }
    }

    private static string GetRadioDisplayName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            // If there is a filename, use it as a fallback label
            var lastSegment = uri.Segments.Length > 0 ? uri.Segments[^1] : string.Empty;

            string nameFromHost = string.Join(" ",
                host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(p => !int.TryParse(p, out _)) // drop numeric parts
                    .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

            if (!string.IsNullOrWhiteSpace(nameFromHost))
                return nameFromHost.Trim();

            // Fallback to last segment without query/extension
            var clean = lastSegment.Split('?', '#')[0].Trim('/');
            clean = clean.Replace("_", " ").Replace("-", " ");
            return string.IsNullOrWhiteSpace(clean) ? uri.Host : clean;
        }
        catch
        {
            return url;
        }
    }
}
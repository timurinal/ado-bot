using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using NetCord;
using NetCord.Gateway;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Channel = System.Threading.Channels.Channel;

namespace AdoBot;

public static class Program
{
    // Queue of outbound messages produced from other threads (e.g., RSS watcher)
    // Tuple: (ChannelId, Content)
    private static readonly Channel<(ulong ChannelId, RssWatcher.FeedItem item)> Outbox = Channel.CreateUnbounded<(ulong, RssWatcher.FeedItem)>();

    private static ConcurrentQueue<Action> MainThreadActions { get; } = new();

    public static async Task Main(string[] args)
    {
        GatewayClient client = new(new BotToken(Config.Token), new GatewayClientConfiguration
        {
            Logger = new ConsoleLogger()
        });

        ApplicationCommandService<ApplicationCommandContext> applicationCommandService = new();

        // Add commands from modules
        applicationCommandService.AddModules(typeof(Program).Assembly);

        client.InteractionCreate += async interaction =>
        {
            if (interaction is not ApplicationCommandInteraction applicationCommandInteraction)
                return;

            var result =
                await applicationCommandService.ExecuteAsync(
                    new ApplicationCommandContext(applicationCommandInteraction, client));

            if (result is not IFailResult failResult)
                return;

            try
            {
                await interaction.SendResponseAsync(InteractionCallback.Message(failResult.Message));
            }
            catch
            {
            }
        };

        await applicationCommandService.RegisterCommandsAsync(client.Rest, client.Id);

        await client.StartAsync();

        // Start a single consumer that executes queued sends in order
        _ = Task.Run(async () =>
        {
            var reader = Outbox.Reader;
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var msg))
                {
                    try
                    {
                        var embed = new EmbedProperties
                        {
                            Title = msg.item.Title,
                            Description = msg.item.Link,
                            Url = msg.item.Link,
                            Color = new Color(4, 9, 48), // optional red accent
                            Image = new EmbedImageProperties($"https://img.youtube.com/vi/{TryGetYouTubeId(msg.item.Link)}/0.jpg")
                        };
                        
                        await client.Rest.SendMessageAsync(
                            msg.ChannelId,
                            new MessageProperties
                            {
                                Embeds = [embed]
                            });
                    }
                    catch (RestRateLimitedException rl)
                    {
                        Console.WriteLine($"Ratelimited for {rl.ResetAfter}."); // You could delay and retry here.
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send message: {ex}");
                    }
                }
            }
        });
        
        ThreadDispatcher.Start();

        await Task.Delay(-1);
    }

    public static void RunOnMainThread(Action action) => MainThreadActions.Enqueue(action);

    // Call this from your RSS watcher thread to enqueue a message for sending
    public static ValueTask QueueMessageAsync(ulong channelId, RssWatcher.FeedItem item) =>
        Outbox.Writer.WriteAsync((channelId, item));

    public static string? TryGetYouTubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // 1) Try common patterns in one shot
        var m = Regex.Match(url, @"(?:v=|/shorts/|/embed/|youtu\.be/)([A-Za-z0-9_-]{11})",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return m.Groups[1].Value;

        // 2) Fallback: look for v= param anywhere
        var v = Regex.Match(url, @"[?&]v=([A-Za-z0-9_-]{11})(?:&|$)", RegexOptions.IgnoreCase);
        if (v.Success)
            return v.Groups[1].Value;

        return null;
    }
}
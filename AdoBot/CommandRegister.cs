using System.Text.RegularExpressions;
using AdoBot.Data;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace AdoBot;

public class CommandRegister : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("strike", "Strikes the given user.",
        DefaultGuildPermissions = Permissions.KickUsers | Permissions.BanUsers,
        Contexts = [InteractionContextType.Guild])]
    public async Task Slash_Strike(GuildUser user, string reason = "No reason")
    {
        try
        {
            // We’re in a guild-only command; GuildUser carries the guild id
            var guildId = user.GuildId;

            var current = BotDatabase.Instance.Strikes
                .FindOne(x => x.GuildId == guildId && x.UserId == user.Id)?.Count ?? 0;

            var updated = current + 1;

            BotDatabase.Instance.Strikes.Upsert(new StrikeRecord
            {
                GuildId = guildId,
                UserId = user.Id,
                Count = updated,
                Notes = reason,
                UpdatedAt = DateTime.UtcNow
            });

            var embed = new EmbedProperties
            {
                Title = "⚠️ User Struck",
                Description = $"**User:** {user.Username}\n**Reason:** {reason}\n**Total Strikes:** {updated}/3",
                Color = new Color(255, 180, 0),
                Timestamp = DateTimeOffset.UtcNow,
                Footer = new EmbedFooterProperties() { Text = $"Issued by {Context.User.Username}" }
            };

            await Context.Interaction.SendResponseAsync(
                InteractionCallback.Message(new InteractionMessageProperties
                {
                    Embeds = [embed],
                    // Flags = MessageFlags.Ephemeral
                }));

            await Context.Client.Rest.SendMessageAsync(
                Config.LogChannelId,
                new MessageProperties
                {
                    Embeds = [embed]
                });
            
            // TODO: notify the striked user? genuinely no idea how the FUCK i would do that though, since the API doesn't allow DMs

            if (updated > 3)
            {
                RestRequestProperties props = new()
                {
                    AuditLogReason = "Three strikes"
                };
                await user.BanAsync(properties:props);

                var banEmbed = new EmbedProperties
                {
                    Title = "\u274C User Banned (Automatic)",
                    Description = $"**User:** {user.Username}\n**Reason:** Three strikes",
                    Color = new Color(255, 0, 0),
                    Timestamp = DateTimeOffset.UtcNow,
                    Footer = new EmbedFooterProperties() { Text = $"Issued by AdoBot" }
                };

                await Context.Client.Rest.SendMessageAsync(
                    Config.LogChannelId,
                    new MessageProperties
                    {
                        Embeds = [banEmbed]
                    });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    [SlashCommand("query", "Queries the strike count for the given user.",
        DefaultGuildPermissions = Permissions.KickUsers | Permissions.BanUsers,
        Contexts = [InteractionContextType.Guild])]
    public async Task Slash_QueryStrikes(GuildUser user)
    {
        var guildId = user.GuildId;
        
        var count = BotDatabase.Instance.Strikes
            .FindOne(x => x.GuildId == guildId && x.UserId == user.Id)?.Count ?? 0;
        
        var embed = new EmbedProperties
        {
            Title = $"Strike count for {user.Username}",
            Description = $"**Total Strikes:** {count}/3",
            Color = new Color(255, 180, 0),
            Timestamp = DateTimeOffset.UtcNow
        };

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds = [embed],
                Flags = MessageFlags.Ephemeral
            }));
    }
    
    [SlashCommand("queryself", "Queries your strike count.",
        Contexts = [InteractionContextType.Guild])]
    public async Task Slash_QueryStrikesSelf()
    {
        var user = Context.User;
        
        var count = BotDatabase.Instance.Strikes
            .FindOne(x => x.GuildId == Context.Guild.Id && x.UserId == user.Id)?.Count ?? 0;
        
        var embed = new EmbedProperties
        {
            Title = $"Your strike count:",
            Description = $"**Total Strikes:** {count}/3",
            Color = new Color(255, 180, 0),
            Timestamp = DateTimeOffset.UtcNow
        };

        await Context.Interaction.SendResponseAsync(
            InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds = [embed],
                Flags = MessageFlags.Ephemeral
            }));
    }
    
    [SlashCommand("kick", "Kicks the given user.",
        DefaultGuildPermissions = Permissions.KickUsers | Permissions.BanUsers,
        Contexts = [InteractionContextType.Guild])]
    public async Task Slash_Kick(GuildUser user, string reason = "No reason")
    {
        try
        {
            RestRequestProperties props = new()
            {
                AuditLogReason = reason
            };
            await user.KickAsync(properties:props);

            var banEmbed = new EmbedProperties
            {
                Title = "\u274C User Kicked",
                Description = $"**User:** {user.Username}\n**Reason:** {reason}",
                Color = new Color(255, 180, 0),
                Timestamp = DateTimeOffset.UtcNow,
                Footer = new EmbedFooterProperties() { Text = $"Issued by {Context.User.Username}" }
            };

            await Context.Client.Rest.SendMessageAsync(
                Config.LogChannelId,
                new MessageProperties
                {
                    Embeds = [banEmbed]
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    
    [SlashCommand("ban", "Bans the given user.",
        DefaultGuildPermissions = Permissions.KickUsers | Permissions.BanUsers,
        Contexts = [InteractionContextType.Guild])]
    public async Task Slash_Ban(GuildUser user, string reason = "No reason") // TODO: implement banning for a duration
    {                                                                        // Not gonna be fun to implement this, since I need to make a database table for it AND constantly check for expired bans.
        try
        {
            // if (!TryParseDuration(duration, out var timespan))
            // {
            //     await Context.Interaction.SendResponseAsync(
            //         InteractionCallback.Message(new InteractionMessageProperties
            //         {
            //             Content = "Invalid duration. Try formats like `1h`, `30m`, or `2d`.",
            //             Flags = MessageFlags.Ephemeral
            //         }));
            //     return;
            // }

            RestRequestProperties props = new()
            {
                AuditLogReason = reason
            };
            await user.BanAsync(properties:props);

            var banEmbed = new EmbedProperties
            {
                Title = "\u274C User Banned",
                Description = $"**User:** {user.Username}\n**Reason:** {reason}",
                Color = new Color(255, 180, 0),
                Timestamp = DateTimeOffset.UtcNow,
                Footer = new EmbedFooterProperties() { Text = $"Issued by {Context.User.Username}" }
            };

            await Context.Client.Rest.SendMessageAsync(
                Config.LogChannelId,
                new MessageProperties
                {
                    Embeds = [banEmbed]
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    
    // TODO: implement this (not doing it yet since I have zero clue how to find a user's ID from the ban menu)
    // [SlashCommand("unban", "Unbans a user by their user ID.",
    //     DefaultGuildPermissions = Permissions.KickUsers | Permissions.BanUsers,
    //     Contexts = [InteractionContextType.Guild])]
    // public async Task Slash_Unban(GuildUser user, string reason = "No reason")
    // {
    //     try
    //     {
    //         // if (!TryParseDuration(duration, out var timespan))
    //         // {
    //         //     await Context.Interaction.SendResponseAsync(
    //         //         InteractionCallback.Message(new InteractionMessageProperties
    //         //         {
    //         //             Content = "Invalid duration. Try formats like `1h`, `30m`, or `2d`.",
    //         //             Flags = MessageFlags.Ephemeral
    //         //         }));
    //         //     return;
    //         // }
    //         
    //         await user.BanAsync();
    //
    //         var banEmbed = new EmbedProperties
    //         {
    //             Title = "\u274C User Banned",
    //             Description = $"**User:** {user.Username}\n**Reason:** {reason}",
    //             Color = new Color(255, 180, 0),
    //             Timestamp = DateTimeOffset.UtcNow,
    //             Footer = new EmbedFooterProperties() { Text = $"Issued by {Context.User.Username}" }
    //         };
    //
    //         await Context.Client.Rest.SendMessageAsync(
    //             Config.LogChannelId,
    //             new MessageProperties
    //             {
    //                 Embeds = [banEmbed]
    //             });
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine(ex);
    //     }
    // }

    [SlashCommand("help", "Shows a help message.", Contexts = [InteractionContextType.Guild])]
    public async Task Slash_Help()
    {
        var embed = new EmbedProperties()
        {
            Title = "Test",
            Description = @"
`/querySelf` - Queries your strike count.
",
            Footer = new EmbedFooterProperties() { Text = $"AdoBot Version {Config.Version}" }
        };
        
        await Context.Interaction.SendResponseAsync(
        InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral
        }));
    }
    
    private static bool TryParseDuration(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)([smhd])", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        double value = double.Parse(match.Groups[1].Value);
        return match.Groups[2].Value.ToLower() switch
        {
            "s" => (result = TimeSpan.FromSeconds(value)) != TimeSpan.Zero,
            "m" => (result = TimeSpan.FromMinutes(value)) != TimeSpan.Zero,
            "h" => (result = TimeSpan.FromHours(value)) != TimeSpan.Zero,
            "d" => (result = TimeSpan.FromDays(value)) != TimeSpan.Zero,
            _ => false
        };
    }
}
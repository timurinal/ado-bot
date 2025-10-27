using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace AdoBot;

public class CommandRegister : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("pong", "Pong!")]
    public static string Pong() => "Ping!";

    [SlashCommand("username", "Gets the username of a user.")]
    public static string Username(User? user)
    {
        return user?.Username ?? "Unknown";
    }

    [UserCommand("ID")]
    public static string Id(User user) => user.Id.ToString();

    [MessageCommand("Timestamp")]
    public static string Timestamp(RestMessage message) => message.CreatedAt.ToString();
}
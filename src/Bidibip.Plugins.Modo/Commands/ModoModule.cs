using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Modo.Commands;

public sealed class ModoModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    public ModoModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
    }

    [SlashCommand("modo", "Ouvrir un ticket de support")]
    public async Task ModoAsync()
    {
        var config = await LoadConfigAsync();

        if (config.ModoChannel == 0)
        {
            await FollowupAsync("Le systeme de tickets n'est pas configure.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var modoChannel = guild.GetTextChannel(config.ModoChannel);
        if (modoChannel is null)
        {
            await FollowupAsync("Le canal de moderation est introuvable.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id.ToString();

        // Check if user already has an open ticket
        if (config.Tickets.TryGetValue(userId, out var existingThreadId))
        {
            var existingThread = guild.GetThreadChannel(existingThreadId);
            if (existingThread is not null && !existingThread.IsArchived)
            {
                await FollowupAsync($"Tu as deja un ticket ouvert : <#{existingThread.Id}>", ephemeral: true);
                return;
            }

            // Thread no longer exists or is archived, remove from tracking
            config.Tickets.Remove(userId);
        }

        // Create private thread
        var thread = await modoChannel.CreateThreadAsync(
            name: Context.User.Username,
            type: ThreadType.PrivateThread,
            autoArchiveDuration: ThreadArchiveDuration.OneWeek
        );

        // Add the user to the thread
        await thread.AddUserAsync(Context.User as IGuildUser ?? (IGuildUser)Context.User);

        // Build close button
        var components = new ComponentBuilder()
            .WithButton("Fermer le ticket", "modo::close", ButtonStyle.Danger)
            .Build();

        var adminRoleId = _botConfig.Roles.Administrator;
        await thread.SendMessageAsync(
            $"Bienvenue dans ton ticket de support, {Context.User.Mention} !\n" +
            $"<@&{adminRoleId}> sera avec toi sous peu.",
            components: components
        );

        // Save ticket
        config.Tickets[userId] = thread.Id;
        await SaveConfigAsync(config);

        await FollowupAsync($"Ton ticket a ete cree : <#{thread.Id}>", ephemeral: true);
    }

    [ComponentInteraction("modo::close")]
    public async Task CloseTicketAsync()
    {
        await DeferAsync(ephemeral: true);
        var config = await LoadConfigAsync();

        if (Context.Channel is not SocketThreadChannel thread)
        {
            await FollowupAsync("Cette action ne peut etre effectuee que dans un ticket.", ephemeral: true);
            return;
        }

        await FollowupAsync("Ce ticket va etre ferme. Merci !");

        // Remove ticket from tracking
        var ticketEntry = config.Tickets.FirstOrDefault(kv => kv.Value == thread.Id);
        if (ticketEntry.Key is not null)
        {
            config.Tickets.Remove(ticketEntry.Key);
            await SaveConfigAsync(config);
        }

        // Archive and lock the thread
        await thread.ModifyAsync(props =>
        {
            props.Archived = true;
            props.Locked = true;
        });
    }

    private async Task<ModoConfig> LoadConfigAsync()
    {
        var configPath = Path.Combine(ModoPlugin.DataPath, "config.json");
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath);
            return JsonSerializer.Deserialize<ModoConfig>(json, PluginJsonOptions.Default) ?? new ModoConfig();
        }

        var defaultConfig = new ModoConfig();
        Directory.CreateDirectory(ModoPlugin.DataPath);
        var defaultJson = JsonSerializer.Serialize(defaultConfig, PluginJsonOptions.Default);
        await File.WriteAllTextAsync(configPath, defaultJson);
        return defaultConfig;
    }

    private async Task SaveConfigAsync(ModoConfig config)
    {
        var configPath = Path.Combine(ModoPlugin.DataPath, "config.json");
        Directory.CreateDirectory(ModoPlugin.DataPath);
        var json = JsonSerializer.Serialize(config, PluginJsonOptions.Default);
        await File.WriteAllTextAsync(configPath, json);
    }
}

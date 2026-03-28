using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Modo.Commands;

public sealed class ModoModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BotConfig _botConfig;

    public ModoModule(BotConfig botConfig)
    {
        _botConfig = botConfig;
    }

    [SlashCommand("modo", "ouvre un canal direct avec la modération")]
    [AllowedBotRole(BotRole.Everyone)]
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
        SocketThreadChannel? thread = null;

        // Check if user already has an existing ticket thread
        if (config.Tickets.TryGetValue(userId, out var existingThreadId))
        {
            var existingThread = guild.GetThreadChannel(existingThreadId);
            if (existingThread is not null)
            {
                thread = existingThread;
            }
            else
            {
                config.Tickets.Remove(userId);
            }
        }

        // Create new thread if none found
        if (thread is null)
        {
            var restThread = await modoChannel.CreateThreadAsync(
                name: Context.User.Username,
                type: ThreadType.PrivateThread,
                invitable: false
            );
            thread = guild.GetThreadChannel(restThread.Id) ?? (SocketThreadChannel)(IThreadChannel)restThread;

            config.Tickets[userId] = thread.Id;
            await SaveConfigAsync(config);
        }

        // Add the user to the thread
        await thread.AddUserAsync(Context.User as IGuildUser ?? (IGuildUser)Context.User);

        var adminRoleMention = $"<@&{_botConfig.Roles.Administrator}>";

        // Build embed
        var embed = new EmbedBuilder();

        var avatarUrl = Context.User.GetAvatarUrl();
        if (avatarUrl is not null)
            embed.WithAuthor($"{Context.User.Username} < A l'aide ! \ud83d\udd90", avatarUrl);
        else
            embed.WithTitle($"{Context.User.Username} < A l'aide ! \ud83d\udd90");

        embed.AddField("Canal de communication ouvert :robot:",
            $"Tu es maintenant en communication directe avec les {adminRoleMention}.\nA toi de nous dire ce qui ne va pas.",
            inline: false);

        // Build close button
        var components = new ComponentBuilder()
            .WithButton("Fermer la discussion", "modo_close_thread", ButtonStyle.Secondary)
            .Build();

        await thread.SendMessageAsync(
            $"{Context.User.Mention} {adminRoleMention}",
            embed: embed.Build(),
            components: components
        );

        // Unarchive thread if needed
        await thread.ModifyAsync(props =>
        {
            props.Archived = false;
            props.Locked = false;
        });

        // Ephemeral response
        var responseEmbed = new EmbedBuilder()
            .WithTitle("Canal de communication ouvert")
            .WithDescription($"Parle avec la modération ici : <#{thread.Id}>")
            .Build();

        await FollowupAsync(embed: responseEmbed, ephemeral: true);
    }

    [ComponentInteraction("modo_close_thread")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task CloseTicketAsync()
    {
        if (Context.Channel is not SocketThreadChannel thread)
            return;

        var config = await LoadConfigAsync();

        // Find and remove user from thread
        var ticketEntry = config.Tickets.FirstOrDefault(kv => kv.Value == thread.Id);
        if (ticketEntry.Key is not null)
        {
            if (ulong.TryParse(ticketEntry.Key, out var ticketUserId))
                await thread.RemoveUserAsync(Context.Guild.GetUser(ticketUserId));

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

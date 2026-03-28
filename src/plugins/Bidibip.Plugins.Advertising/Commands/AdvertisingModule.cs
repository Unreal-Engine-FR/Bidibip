using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Advertising.Commands;

public sealed class AdvertisingModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    public AdvertisingModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
    }

    [SlashCommand("annonce", "Cr\u00e9er une annonce d'offre ou de recherche d'emploi")]
    [AllowedBotRole(BotRole.Member)]
    public async Task AnnonceAsync()
    {
        var config = await AdvertisingPlugin.LoadConfigAsync();

        if (config.InProgressChannel == 0)
        {
            await FollowupAsync("Le syst\u00e8me d'annonces n'est pas configur\u00e9.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id.ToString();

        if (config.StoredAds.TryGetValue(userId, out var userAds) && userAds.Count > 0)
        {
            if (userAds.Count >= config.MaxAdPerUser)
            {
                await FollowupAsync(
                    "# :warning: Tu as d\u00e9j\u00e0 des annonces ouvertes !\n> Note : Tu as atteint le nombre maximal d'annonces simultan\u00e9es",
                    ephemeral: true);
            }
            else
            {
                var components = new ComponentBuilder()
                    .WithButton("Cr\u00e9er une nouvelle annonce", "ad-create-new", ButtonStyle.Primary)
                    .Build();

                await FollowupAsync(
                    "# :warning: Tu as d\u00e9j\u00e0 des annonces ouvertes !",
                    ephemeral: true,
                    components: components);
            }

            foreach (var (channelId, data) in userAds)
            {
                var title = data.Description.Title ?? "Annonce sans titre";
                var adComponents = new ComponentBuilder()
                    .WithButton("Modifier", $"ad-edit-ad-{channelId}", ButtonStyle.Secondary)
                    .WithButton("Supprimer", $"ad-delete-ad-{channelId}", ButtonStyle.Danger)
                    .Build();

                await FollowupAsync(
                    $"**{AdPreview.Truncate(title, 300)}** : https://discord.com/channels/{_botConfig.GuildId}/{channelId}",
                    ephemeral: true,
                    components: adComponents);
            }
            return;
        }

        var err = await CreateAdThreadAsync(config, new AdInProgress(), Context.Guild, Context.User, msg => FollowupAsync(msg, ephemeral: true));
        if (err is not null) await FollowupAsync(err, ephemeral: true);
    }

    [ComponentInteraction("ad-create-new")]
    [AllowedBotRole(BotRole.Member)]
    public async Task CreateNewAdAsync()
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var err = await CreateAdThreadAsync(config, new AdInProgress(), Context.Guild, Context.User, msg => FollowupAsync(msg, ephemeral: true));
        if (err is not null) await FollowupAsync(err, ephemeral: true);
    }

    internal static async Task<string?> CreateAdThreadAsync(
        AdConfig config, AdInProgress ad, SocketGuild guild, IUser user, Func<string, Task> followup)
    {
        var inProgressChannel = guild.GetTextChannel(config.InProgressChannel);
        if (inProgressChannel is null)
            return "Le canal d'annonces en cours est introuvable.";

        var userId = user.Id;

        // Delete existing in-progress thread for this user if any
        var existingEntry = config.InProgress.FirstOrDefault(kv => kv.Value.UserId == userId);
        var removedOld = false;
        if (existingEntry.Key is not null)
        {
            try
            {
                var oldThread = guild.GetThreadChannel(existingEntry.Value.ThreadId);
                if (oldThread is not null)
                    await oldThread.DeleteAsync();
            }
            catch { /* ignore */ }

            config.InProgress.Remove(existingEntry.Key);
            removedOld = true;
        }

        var thread = await inProgressChannel.CreateThreadAsync(
            name: $"Annonce de {user.Username}",
            type: ThreadType.PrivateThread,
            autoArchiveDuration: ThreadArchiveDuration.OneWeek);

        await thread.AddUserAsync((IGuildUser)user);

        var isEditing = ad.EditedPostChannel.HasValue && ad.Title is not null;

        await thread.SendMessageAsync(isEditing
            ? $"# Bienvenue dans le formulaire de modification d'annonce {user.Username} !\n> Tu peux modifier les champs individuellement en cliquant sur les boutons ci-dessous."
            : $"# Bienvenue dans le formulaire de cr\u00e9ation d'annonce {user.Username} !");

        ad.UserId = userId;
        ad.ThreadId = thread.Id;

        config.InProgress[thread.Id.ToString()] = ad;

        if (isEditing)
            await AdQuestions.DisplayPrefilledAsync(thread, ad, config, user);
        else
            await AdQuestions.AdvanceAsync(thread, ad, config, user);
        await AdvertisingPlugin.SaveConfigAsync(config);

        var note = removedOld
            ? "\n> Note : ta pr\u00e9c\u00e9dente annonce en cours de cr\u00e9ation a \u00e9t\u00e9 supprim\u00e9e"
            : "";

        await followup($"Bien re\u00e7u, la suite se passe ici :arrow_right: <#{thread.Id}>{note}");
        return null;
    }

    internal static AdInProgress CloneAdForEdit(StoredAd stored)
    {
        var json = JsonSerializer.Serialize(stored.Description, PluginJsonOptions.Default);
        return JsonSerializer.Deserialize<AdInProgress>(json, PluginJsonOptions.Default) ?? new AdInProgress();
    }

    internal static void StoreAd(AdConfig config, AdInProgress ad, ulong channelId, ulong messageId)
    {
        var userId = ad.UserId.ToString();
        if (!config.StoredAds.TryGetValue(userId, out var userAds))
        {
            userAds = new Dictionary<string, StoredAd>();
            config.StoredAds[userId] = userAds;
        }

        userAds[channelId.ToString()] = new StoredAd
        {
            MessageChannel = channelId,
            MessageId = messageId,
            Description = ad
        };
    }
}

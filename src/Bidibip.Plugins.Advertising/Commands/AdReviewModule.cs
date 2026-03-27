using Bidibip.Plugin.Sdk;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Advertising.Commands;

public sealed class AdReviewModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    public AdReviewModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
    }

    // ── Pre-publish (user submits for review) ────────────────────────

    [ComponentInteraction("ad-pre-publish")]
    public async Task PrePublishAsync()
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        if (ad.UserId != Context.User.Id)
        {
            await FollowupAsync("Tu n'es pas l'auteur de ce post !", ephemeral: true);
            return;
        }

        await FollowupAsync("Bien re\u00e7u, nous allons passer en revue ton annonce", ephemeral: true);

        var reviewerMentions = string.Join("", config.ReviewerRoles.Select(r => $"<@&{r}>"));
        if (string.IsNullOrEmpty(reviewerMentions))
            reviewerMentions = $"<@&{_botConfig.Roles.Administrator}>";

        await Context.Channel.SendMessageAsync(
            $"{Context.User.Mention} a termin\u00e9 son annonce, nous allons proc\u00e9der \u00e0 quelques v\u00e9rifications avant de la publier.\n{reviewerMentions}");

        if (ad.PreviewMessageId.HasValue)
        {
            try
            {
                var previewMsg = await Context.Channel.GetMessageAsync(ad.PreviewMessageId.Value);
                if (previewMsg is IUserMessage userMsg)
                {
                    var components = new ComponentBuilder()
                        .WithButton("Publier", "ad-validate", ButtonStyle.Success)
                        .WithButton("R\u00e9voquer", "ad-deny", ButtonStyle.Danger)
                        .Build();

                    await userMsg.ModifyAsync(props => props.Components = components);
                }
            }
            catch { /* ignore */ }
        }

        ad.Step = "review";
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Validate (reviewer approves) ─────────────────────────────────

    [ComponentInteraction("ad-validate")]
    public async Task ValidateAsync()
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();

        var threadId = Context.Channel.Id.ToString();
        if (!config.InProgress.TryGetValue(threadId, out var ad))
        {
            await FollowupAsync("Cette annonce n'existe plus.", ephemeral: true);
            return;
        }

        var guildUser = Context.User as SocketGuildUser;
        if (guildUser is null)
        {
            await FollowupAsync("Tu n'as pas l'autorisation requise pour faire ceci !", ephemeral: true);
            return;
        }

        var isReviewer = guildUser.Roles.Any(r =>
            config.ReviewerRoles.Contains(r.Id) || r.Id == _botConfig.Roles.Administrator);

        if (!isReviewer)
        {
            await FollowupAsync("Tu n'as pas l'autorisation requise pour faire ceci !", ephemeral: true);
            return;
        }

        if (Context.User.Id == ad.UserId)
        {
            await FollowupAsync("Tu ne peux pas approuver toi m\u00eame ton annonce !", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var adUser = await _client.GetUserAsync(ad.UserId);

        if (ad.EditedPostChannel.HasValue && ad.EditedPostMessage.HasValue)
        {
            try
            {
                var existingChannel = guild.GetThreadChannel(ad.EditedPostChannel.Value);
                if (existingChannel is not null)
                {
                    var existingMsg = await existingChannel.GetMessageAsync(ad.EditedPostMessage.Value);
                    if (existingMsg is IUserMessage existingUserMsg)
                    {
                        var embeds = AdPreview.BuildPreviewEmbeds(ad, adUser ?? Context.User);
                        await existingUserMsg.ModifyAsync(props => props.Embeds = embeds);
                    }
                }

                AdvertisingModule.StoreAd(config, ad, ad.EditedPostChannel.Value, ad.EditedPostMessage.Value);
            }
            catch { /* ignore edit failures */ }
        }
        else
        {
            if (config.AdForum == 0)
            {
                await FollowupAsync("Le forum d'annonces n'est pas configur\u00e9.", ephemeral: true);
                return;
            }

            var forumChannel = guild.GetForumChannel(config.AdForum);
            if (forumChannel is null)
            {
                await FollowupAsync("Le forum d'annonces est introuvable.", ephemeral: true);
                return;
            }

            try
            {
                var emoji = AdPreview.GetContractEmoji(ad.ContractType);
                var postTitle = string.IsNullOrWhiteSpace(emoji)
                    ? (ad.Title ?? "Annonce")
                    : $"{emoji} {AdPreview.Truncate(ad.Title ?? "Annonce", 100)}";

                var embeds = AdPreview.BuildPreviewEmbeds(ad, adUser ?? Context.User);

                var forumThread = await forumChannel.CreatePostAsync(
                    title: postTitle,
                    text: " ",
                    embeds: embeds);

                var messages = await forumThread.GetMessagesAsync(1).FlattenAsync();
                var firstMsg = messages.FirstOrDefault();

                AdvertisingModule.StoreAd(config, ad, forumThread.Id, firstMsg?.Id ?? 0);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Erreur lors de la publication : {ex.Message}", ephemeral: true);
                return;
            }
        }

        config.InProgress.Remove(threadId);
        await AdvertisingPlugin.SaveConfigAsync(config);

        await FollowupAsync("Annonce valid\u00e9e et publi\u00e9e !", ephemeral: true);

        try
        {
            if (Context.Channel is SocketThreadChannel threadChannel)
                await threadChannel.DeleteAsync();
        }
        catch { /* ignore */ }
    }

    // ── Deny button (opens modal) ────────────────────────────────────

    [ComponentInteraction("ad-deny")]
    public async Task DenyAsync()
    {
        await Context.Interaction.RespondWithModalAsync<DenyModal>("ad-deny-modal");
    }

    [ModalInteraction("ad-deny-modal")]
    public async Task DenyModalAsync(DenyModal modal)
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();

        var threadId = Context.Channel.Id.ToString();
        if (!config.InProgress.TryGetValue(threadId, out var ad))
        {
            await FollowupAsync("Cette annonce n'existe plus.", ephemeral: true);
            return;
        }

        await FollowupAsync(
            $"<@{ad.UserId}>, ton annonce n'a pas \u00e9t\u00e9 valid\u00e9e pour la raison suivante :\n{modal.Reason}\n\nTu peux encore modifier ton annonce avant de la ressoumettre pour qu'elle soit conforme aux pr\u00e9requis.");

        if (ad.PreviewMessageId.HasValue)
        {
            try
            {
                var previewMsg = await Context.Channel.GetMessageAsync(ad.PreviewMessageId.Value);
                if (previewMsg is IUserMessage userMsg)
                    await userMsg.ModifyAsync(props => props.Components = new ComponentBuilder().Build());
            }
            catch { /* ignore */ }
        }

        ad.Step = "preview";
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Edit existing ad ─────────────────────────────────────────────

    [ComponentInteraction("ad-edit-ad-*")]
    public async Task EditAdAsync(string channelId)
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var userId = Context.User.Id.ToString();

        if (!config.StoredAds.TryGetValue(userId, out var userAds) ||
            !userAds.TryGetValue(channelId, out var storedAd))
        {
            await FollowupAsync("Cette annonce n'existe plus.", ephemeral: true);
            return;
        }

        var editAd = AdvertisingModule.CloneAdForEdit(storedAd);
        editAd.EditedPostChannel = storedAd.MessageChannel;
        editAd.EditedPostMessage = storedAd.MessageId;

        var err = await AdvertisingModule.CreateAdThreadAsync(
            config, editAd, Context.Guild, Context.User, msg => FollowupAsync(msg, ephemeral: true));
        if (err is not null) await FollowupAsync(err, ephemeral: true);
    }

    // ── Delete existing ad ───────────────────────────────────────────

    [ComponentInteraction("ad-delete-ad-*")]
    public async Task DeleteAdAsync(string channelId)
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var userId = Context.User.Id.ToString();

        if (!config.StoredAds.TryGetValue(userId, out var userAds) ||
            !userAds.ContainsKey(channelId))
        {
            await FollowupAsync("Cette annonce n'existe plus.", ephemeral: true);
            return;
        }

        if (ulong.TryParse(channelId, out var threadIdVal))
        {
            try
            {
                var thread = Context.Guild.GetThreadChannel(threadIdVal);
                if (thread is not null)
                    await thread.DeleteAsync();
            }
            catch { /* ignore */ }
        }

        userAds.Remove(channelId);
        if (userAds.Count == 0)
            config.StoredAds.Remove(userId);

        await AdvertisingPlugin.SaveConfigAsync(config);
        await FollowupAsync("Ton annonce a bien \u00e9t\u00e9 supprim\u00e9e !", ephemeral: true);
    }

    private AdInProgress? FindAd(AdConfig config)
    {
        var threadId = Context.Channel.Id.ToString();
        return config.InProgress.TryGetValue(threadId, out var ad) ? ad : null;
    }

}

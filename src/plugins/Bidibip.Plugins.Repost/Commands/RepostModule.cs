using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Repost.Commands;

public sealed class RepostModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("set-forum-link", "Lie un forum à un channel de repost")]
    [AllowedBotRole(BotRole.Administrator)]
    public async Task SetForumLinkAsync(
        [Summary("forum", "Forum où seront suivis les nouveaux posts")] IChannel forum,
        [Summary("repost-channel", "Canal où seront repostés les évenements du forum")] IChannel repostChannel,
        [Summary("vote", "Active les fonctionnalités de vote")] bool vote,
        [Summary("enabled", "Active ou désactive le lien")] bool enabled)
    {
        var config = await RepostPlugin.LoadConfigAsync();
        var forumKey = forum.Id.ToString();

        if (enabled)
        {
            if (!config.Forums.TryGetValue(forumKey, out var forumConfig))
            {
                forumConfig = new ForumConfig();
                config.Forums[forumKey] = forumConfig;
            }
            forumConfig.RepostChannels.Add(repostChannel.Id);
            forumConfig.VoteEnabled = vote;

            await RepostPlugin.SaveConfigAsync(config);
            await FollowupAsync($"Forum <#{forum.Id}> connecté au channel <#{repostChannel.Id}> !", ephemeral: true);
        }
        else
        {
            config.Forums.Remove(forumKey);
            await RepostPlugin.SaveConfigAsync(config);
            await FollowupAsync($"Forum <#{forum.Id}> déconnecté du channel <#{repostChannel.Id}> !", ephemeral: true);
        }
    }

    [SlashCommand("reposte", "Promeut le message donné dans le salon de repost")]
    [AllowedBotRole(BotRole.Member)]
    public async Task ReposteAsync(
        [Summary("message", "lien du message à promouvoir")] string message)
    {
        if (Context.Channel is not SocketThreadChannel thread)
        {
            await FollowupAsync("La commande doit être exécutée depuis un fil qui t'appartient", ephemeral: true);
            return;
        }

        var parentId = thread.CategoryId;
        if (parentId is null)
        {
            await FollowupAsync("La commande doit être exécutée depuis un fil qui t'appartient", ephemeral: true);
            return;
        }

        // Parse message ID from URL or raw ID
        var lastPart = message.Split('/').Last();
        if (!ulong.TryParse(lastPart, out var messageId))
        {
            await FollowupAsync("L'option message doit être un identifiant de message ou le lien vers le message", ephemeral: true);
            return;
        }

        IMessage? sourceMessage;
        try
        {
            sourceMessage = await thread.GetMessageAsync(messageId);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Le message fourni n'est pas valid : {ex.Message}", ephemeral: true);
            return;
        }

        if (sourceMessage == null)
        {
            await FollowupAsync("Le message fourni n'est pas valid : message introuvable", ephemeral: true);
            return;
        }

        var config = await RepostPlugin.LoadConfigAsync();
        var forumKey = parentId.Value.ToString();

        if (!config.Forums.TryGetValue(forumKey, out var forumConfig))
        {
            await FollowupAsync("La fonctionnalité de reposte n'est pas disponible dans ce contexte", ephemeral: true);
            return;
        }

        var member = Context.Guild.GetUser(Context.User.Id);
        if (member == null)
        {
            await FollowupAsync("Impossible de résoudre l'utilisateur.", ephemeral: true);
            return;
        }

        var forumName = Context.Guild.GetChannel(parentId.Value)?.Name ?? "Unknown";
        var messageUrl = $"https://discord.com/channels/{Context.Guild.Id}/{thread.Id}/{sourceMessage.Id}";
        var threadKey = thread.Id.ToString();

        foreach (var repostChannelId in forumConfig.RepostChannels)
        {
            var destChannel = Context.Guild.GetTextChannel(repostChannelId);
            if (destChannel == null) continue;

            var sentMessages = await RepostPlugin.SendRepostMessagesAsync(
                destChannel, sourceMessage, messageUrl, thread.Name, forumName,
                member.DisplayName, member.GetAvatarUrl() ?? member.GetDefaultAvatarUrl());

            // Track ALL sent messages for vote updates (matching Rust reposte behavior)
            if (config.Votes.TryGetValue(threadKey, out var votes))
            {
                foreach (var msg in sentMessages)
                {
                    votes.RepostedMessages.Add(new MessageRef
                    {
                        ChannelId = repostChannelId,
                        MessageId = msg.Id
                    });
                }
            }
        }

        if (forumConfig.VoteEnabled)
        {
            await RepostPlugin.SaveConfigAsync(config);
            await RepostPlugin.UpdateVoteMessagesAsync(Context.Guild, thread.Id, config);
        }

        await FollowupAsync("Message reposté !", ephemeral: true);
    }

    [ComponentInteraction("repost:vote-yes:*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task VoteYesAsync()
    {
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var threadId = ulong.Parse(customId.Split(':').Last());
        await HandleVoteAsync(threadId, isYes: true);
    }

    [ComponentInteraction("repost:vote-no:*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task VoteNoAsync()
    {
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var threadId = ulong.Parse(customId.Split(':').Last());
        await HandleVoteAsync(threadId, isYes: false);
    }

    [ComponentInteraction("repost:see-votes:*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task SeeVotesAsync()
    {
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var threadId = ulong.Parse(customId.Split(':').Last());

        var config = await RepostPlugin.LoadConfigAsync();
        var threadKey = threadId.ToString();

        if (!config.Votes.TryGetValue(threadKey, out var voteData))
            return;

        var yStr = string.Join("\n", voteData.Yes.Values);
        var nStr = string.Join("\n", voteData.No.Values);

        var embed = new EmbedBuilder()
            .WithTitle("Votes actuels")
            .WithDescription($"Nombre de votes : {voteData.Yes.Count + voteData.No.Count}")
            .AddField("Pour \u2705", RepostPlugin.Truncate(string.IsNullOrEmpty(yStr) ? "-" : yStr, 1024), inline: true)
            .AddField("Contre \u274c", RepostPlugin.Truncate(string.IsNullOrEmpty(nStr) ? "-" : nStr, 1024), inline: true)
            .Build();

        await Context.Interaction.RespondAsync(embed: embed, ephemeral: true);
    }

    private async Task HandleVoteAsync(ulong threadId, bool isYes)
    {
        var config = await RepostPlugin.LoadConfigAsync();
        var threadKey = threadId.ToString();

        if (!config.Votes.TryGetValue(threadKey, out var voteData))
            return;

        await DeferAsync(ephemeral: true);

        // Check if source thread is archived
        var sourceThread = Context.Guild.GetChannel(voteData.SourceThread) as SocketThreadChannel;
        if (sourceThread?.IsArchived == true)
        {
            await FollowupAsync(
                "Ce thread a été archivé. tu ne peux plus voter.", ephemeral: true);
            return;
        }

        var userKey = Context.User.Id.ToString();
        var displayName = (Context.User as IGuildUser)?.DisplayName ?? Context.User.Username;

        if (isYes)
        {
            voteData.No.Remove(userKey);
            if (voteData.Yes.ContainsKey(userKey))
                voteData.Yes.Remove(userKey);
            else
                voteData.Yes[userKey] = displayName;
        }
        else
        {
            voteData.Yes.Remove(userKey);
            if (voteData.No.ContainsKey(userKey))
                voteData.No.Remove(userKey);
            else
                voteData.No[userKey] = displayName;
        }

        await RepostPlugin.SaveConfigAsync(config);
        await RepostPlugin.UpdateVoteMessagesAsync(Context.Guild, threadId, config);

        await FollowupAsync(
            "Ton vote a bien été pris en compte !", ephemeral: true);
    }
}

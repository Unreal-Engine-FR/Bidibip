using System.Text.RegularExpressions;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Repost.Commands;

public sealed partial class RepostModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;

    public RepostModule(DiscordSocketClient client)
    {
        _client = client;
    }

    [SlashCommand("set-forum-link", "Lier un forum a un canal de destination pour le repost")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task SetForumLinkAsync(
        [Summary("forum", "Le canal forum source")] IChannel forum,
        [Summary("destination", "Le canal de destination pour les reposts")] IChannel destination,
        [Summary("vote", "Activer les boutons de vote sur les reposts")] bool vote = false,
        [Summary("enable", "Activer ou desactiver le lien")] bool enable = true)
    {
        var config = await RepostPlugin.LoadConfigAsync();

        var existing = config.Links.FirstOrDefault(l => l.ForumId == forum.Id);
        if (existing is not null)
        {
            existing.DestinationId = destination.Id;
            existing.VotingEnabled = vote;
            existing.Enabled = enable;
        }
        else
        {
            config.Links.Add(new ForumLink
            {
                ForumId = forum.Id,
                DestinationId = destination.Id,
                VotingEnabled = vote,
                Enabled = enable
            });
        }

        await RepostPlugin.SaveConfigAsync(config);

        var status = enable ? "active" : "desactive";
        var voteStatus = vote ? "avec votes" : "sans votes";
        await FollowupAsync($"Lien forum configure : <#{forum.Id}> -> <#{destination.Id}> ({status}, {voteStatus})", ephemeral: true);
    }

    [SlashCommand("repost", "Reposter manuellement un message depuis un fil de forum")]
    [AllowedBotRole(BotRole.Member)]
    public async Task RepostAsync(
        [Summary("message", "URL ou ID du message a reposter")] string messageRef)
    {
        // Check that we're in a thread
        if (Context.Channel is not IThreadChannel thread)
        {
            await FollowupAsync("Cette commande doit etre utilisee dans un fil de forum.", ephemeral: true);
            return;
        }

        var parentId = thread.CategoryId;
        if (parentId is null)
        {
            await FollowupAsync("Impossible de determiner le canal parent de ce fil.", ephemeral: true);
            return;
        }

        var config = await RepostPlugin.LoadConfigAsync();
        var link = config.Links.FirstOrDefault(l => l.Enabled && l.ForumId == parentId.Value);
        if (link is null)
        {
            await FollowupAsync("Aucun lien de forum configure pour ce canal.", ephemeral: true);
            return;
        }

        // Parse message ID from URL or raw ID
        ulong messageId;
        var urlMatch = MessageUrlRegex().Match(messageRef);
        if (urlMatch.Success)
        {
            messageId = ulong.Parse(urlMatch.Groups[1].Value);
        }
        else if (ulong.TryParse(messageRef, out var parsedId))
        {
            messageId = parsedId;
        }
        else
        {
            await FollowupAsync("Format de message invalide. Fournissez une URL ou un ID de message.", ephemeral: true);
            return;
        }

        var message = await thread.GetMessageAsync(messageId);
        if (message is null)
        {
            await FollowupAsync("Message introuvable dans ce fil.", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var destinationChannel = guild.GetTextChannel(link.DestinationId);
        if (destinationChannel is null)
        {
            await FollowupAsync("Le canal de destination est introuvable.", ephemeral: true);
            return;
        }

        // Build the repost embed
        var embedBuilder = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithAuthor(message.Author.Username, message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl())
            .WithTitle(thread.Name)
            .WithTimestamp(message.Timestamp);

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            var content = message.Content.Length > 2048
                ? message.Content[..2045] + "..."
                : message.Content;
            embedBuilder.WithDescription(content);
        }

        var imageAttachment = message.Attachments
            .FirstOrDefault(a => a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);
        if (imageAttachment is not null)
        {
            embedBuilder.WithImageUrl(imageAttachment.Url);
        }

        var threadUrl = $"https://discord.com/channels/{guild.Id}/{thread.Id}";
        var components = new ComponentBuilder()
            .WithButton("Voir le fil", style: ButtonStyle.Link, url: threadUrl)
            .Build();

        var repostMessage = await destinationChannel.SendMessageAsync(embed: embedBuilder.Build(), components: components);

        if (link.VotingEnabled)
        {
            var threadIdStr = thread.Id.ToString();

            var voteComponents = new ComponentBuilder()
                .WithButton($"Oui (0)", $"repost::vote_yes::{thread.Id}", ButtonStyle.Success)
                .WithButton($"Non (0)", $"repost::vote_no::{thread.Id}", ButtonStyle.Danger)
                .Build();

            var voteMessage = await destinationChannel.SendMessageAsync("Votez :", components: voteComponents);

            config.Votes[threadIdStr] = new ThreadVoteData
            {
                RepostMessageId = repostMessage.Id,
                VoteMessageId = voteMessage.Id
            };
            await RepostPlugin.SaveConfigAsync(config);
        }

        await FollowupAsync($"Message reposte dans <#{link.DestinationId}>.", ephemeral: true);
    }

    [SlashCommand("repost-votes", "Voir la liste des votants pour ce fil")]
    [AllowedBotRole(BotRole.Member)]
    public async Task RepostVotesAsync()
    {
        if (Context.Channel is not IThreadChannel thread)
        {
            await FollowupAsync("Cette commande doit etre utilisee dans un fil de forum.", ephemeral: true);
            return;
        }

        var config = await RepostPlugin.LoadConfigAsync();
        var threadIdStr = thread.Id.ToString();

        if (!config.Votes.TryGetValue(threadIdStr, out var voteData))
        {
            await FollowupAsync("Aucun vote enregistre pour ce fil.", ephemeral: true);
            return;
        }

        var yesVoters = voteData.Yes.Count > 0
            ? string.Join("\n", voteData.Yes.Select(id => $"<@{id}>"))
            : "Aucun";

        var noVoters = voteData.No.Count > 0
            ? string.Join("\n", voteData.No.Select(id => $"<@{id}>"))
            : "Aucun";

        var embed = new EmbedBuilder()
            .WithColor(Color.Gold)
            .WithTitle($"Votes - {thread.Name}")
            .AddField($"Oui ({voteData.Yes.Count})", yesVoters, inline: true)
            .AddField($"Non ({voteData.No.Count})", noVoters, inline: true)
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [ComponentInteraction("repost::vote_yes::*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task VoteYesAsync()
    {
        await DeferAsync(ephemeral: true);
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var parts = customId.Split("::");
        if (parts.Length < 3 || !ulong.TryParse(parts[2], out var threadId))
        {
            await FollowupAsync("Identifiant de fil invalide.", ephemeral: true);
            return;
        }

        await HandleVoteAsync(threadId, isYes: true);
    }

    [ComponentInteraction("repost::vote_no::*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task VoteNoAsync()
    {
        await DeferAsync(ephemeral: true);
        var customId = ((IComponentInteraction)Context.Interaction).Data.CustomId;
        var parts = customId.Split("::");
        if (parts.Length < 3 || !ulong.TryParse(parts[2], out var threadId))
        {
            await FollowupAsync("Identifiant de fil invalide.", ephemeral: true);
            return;
        }

        await HandleVoteAsync(threadId, isYes: false);
    }

    private async Task HandleVoteAsync(ulong threadId, bool isYes)
    {
        var config = await RepostPlugin.LoadConfigAsync();
        var threadIdStr = threadId.ToString();

        if (!config.Votes.TryGetValue(threadIdStr, out var voteData))
        {
            await FollowupAsync("Aucun vote enregistre pour ce fil.", ephemeral: true);
            return;
        }

        var userIdStr = Context.User.Id.ToString();

        // Toggle: remove from opposite list if present, then add/remove from target list
        if (isYes)
        {
            voteData.No.Remove(userIdStr);
            if (voteData.Yes.Contains(userIdStr))
            {
                voteData.Yes.Remove(userIdStr);
                await FollowupAsync("Vote retire.", ephemeral: true);
            }
            else
            {
                voteData.Yes.Add(userIdStr);
                await FollowupAsync("Vote enregistre : Oui", ephemeral: true);
            }
        }
        else
        {
            voteData.Yes.Remove(userIdStr);
            if (voteData.No.Contains(userIdStr))
            {
                voteData.No.Remove(userIdStr);
                await FollowupAsync("Vote retire.", ephemeral: true);
            }
            else
            {
                voteData.No.Add(userIdStr);
                await FollowupAsync("Vote enregistre : Non", ephemeral: true);
            }
        }

        await RepostPlugin.SaveConfigAsync(config);

        // Update the vote message buttons with new counts
        await UpdateVoteButtonsAsync(voteData, threadId);
    }

    private async Task UpdateVoteButtonsAsync(ThreadVoteData voteData, ulong threadId)
    {
        if (voteData.VoteMessageId == 0)
            return;

        try
        {
            // Find the channel containing the vote message
            // The vote message is in the destination channel for the forum link
            var config = await RepostPlugin.LoadConfigAsync();

            // Find the thread's parent forum to get the destination channel
            var thread = Context.Guild.GetChannel(threadId);
            ulong? parentId = null;
            if (thread is IThreadChannel threadChannel)
            {
                parentId = threadChannel.CategoryId;
            }

            if (parentId is null)
                return;

            var link = config.Links.FirstOrDefault(l => l.ForumId == parentId.Value);
            if (link is null)
                return;

            var destChannel = Context.Guild.GetTextChannel(link.DestinationId);
            if (destChannel is null)
                return;

            var voteMessage = await destChannel.GetMessageAsync(voteData.VoteMessageId);
            if (voteMessage is not IUserMessage userMessage)
                return;

            var updatedComponents = new ComponentBuilder()
                .WithButton($"Oui ({voteData.Yes.Count})", $"repost::vote_yes::{threadId}", ButtonStyle.Success)
                .WithButton($"Non ({voteData.No.Count})", $"repost::vote_no::{threadId}", ButtonStyle.Danger)
                .Build();

            await userMessage.ModifyAsync(m => m.Components = updatedComponents);
        }
        catch
        {
            // Best effort - button update failure is not critical
        }
    }

    [GeneratedRegex(@"/(\d+)$")]
    private static partial Regex MessageUrlRegex();
}

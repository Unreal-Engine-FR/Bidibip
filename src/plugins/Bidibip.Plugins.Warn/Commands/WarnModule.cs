using System.Collections.Concurrent;
using System.Globalization;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Warn.Commands;

public sealed class WarnModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;

    // Key: moderator user ID → (target user ID, action)
    private static readonly ConcurrentDictionary<ulong, (ulong TargetId, string Action)> PendingModals = new();

    public WarnModule(DiscordSocketClient client)
    {
        _client = client;
    }

    // ── Slash commands ──────────────────────────────────────────────

    [SlashCommand("sanction", "Sanctionner un utilisateur")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task SanctionAsync(
        [Summary("cible", "utilisateur à sanctionner")] IUser target,
        [Summary("action", "sanction à appliquer")]
        [Choice("warn", "warn")]
        [Choice("ban du vocal", "ban_vocal")]
        [Choice("exclusion une heure", "mute_1h")]
        [Choice("exclusion un jour", "mute_1d")]
        [Choice("exclusion une semaine", "mute_1w")]
        [Choice("kick", "kick")]
        [Choice("ban", "ban")]
        string action)
    {
        await OpenWarnModalAsync(target, action);
    }

    [SlashCommand("historique", "Voir l'historique des sanctions d'un utilisateur")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task HistoriqueAsync(
        [Summary("utilisateur", "Utilisateur dont on veut voir l'historique de sanctions")] IUser target)
    {
        var data = await WarnPlugin.LoadDataAsync();
        var userId = target.Id.ToString();

        if (!data.Warns.TryGetValue(userId, out var records) || records.Count == 0)
        {
            await FollowupAsync(
                embed: new EmbedBuilder()
                    .WithTitle($"Historique de {target.Username}")
                    .WithDescription("Aucune sanction enregistrée.")
                    .WithColor(Color.Green)
                    .Build(),
                ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Historique de {target.Username} ({records.Count} sanction(s))")
            .WithColor(Color.Orange);

        foreach (var record in records.OrderByDescending(r => r.Date))
        {
            var fieldTitle = $"{WarnPlugin.FormatAction(record.Action)} - {record.Date:dd/MM/yyyy HH:mm}";
            var fieldValue = WarnPlugin.Truncate(record.Reason, 800);

            if (!string.IsNullOrEmpty(record.FullMessageLink))
                fieldValue += $"\n{record.FullMessageLink}";

            embed.AddField(fieldTitle, fieldValue, false);

            if (embed.Fields.Count >= 25)
                break;
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    // ── User context menu commands ──────────────────────────────────

    [UserCommand("warn")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task WarnContextAsync(IUser user)
        => await OpenWarnModalAsync(user, "warn");

    [UserCommand("ban du vocal")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task BanVocalContextAsync(IUser user)
        => await OpenWarnModalAsync(user, "ban_vocal");

    [UserCommand("kick")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task KickContextAsync(IUser user)
        => await OpenWarnModalAsync(user, "kick");

    [UserCommand("exclusion 1h")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task Mute1hContextAsync(IUser user)
        => await OpenWarnModalAsync(user, "mute_1h");

    [UserCommand("ban")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task BanContextAsync(IUser user)
        => await OpenWarnModalAsync(user, "ban");

    // ── Modal ───────────────────────────────────────────────────────

    private async Task OpenWarnModalAsync(IUser target, string action)
    {
        PendingModals[Context.User.Id] = (target.Id, action);

        var modalTitle = $"{WarnPlugin.FormatAction(action)} de {target.Username}";
        if (modalTitle.Length > 45) modalTitle = modalTitle[..45];

        var modal = new ModalBuilder()
            .WithTitle(modalTitle)
            .WithCustomId("warn-modal")
            .AddTextInput(new TextInputBuilder()
                .WithLabel("Raison")
                .WithCustomId("reason")
                .WithStyle(TextInputStyle.Short)
                .WithRequired(true)
                .WithPlaceholder("Ce message sera transmis à la personne concernée"))
            .AddTextInput(new TextInputBuilder()
                .WithLabel("Autres informations")
                .WithCustomId("other")
                .WithStyle(TextInputStyle.Paragraph)
                .WithRequired(false)
                .WithPlaceholder("Autres informations (ne sera pas transmis)"))
            .AddTextInput(new TextInputBuilder()
                .WithLabel("Url")
                .WithCustomId("url")
                .WithStyle(TextInputStyle.Short)
                .WithRequired(false)
                .WithPlaceholder("Lien vers le message contextuel"))
            .Build();

        await Context.Interaction.RespondWithModalAsync(modal);
    }

    [ModalInteraction("warn-modal")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task HandleWarnModalAsync(WarnModal modal)
    {
        await DeferAsync(ephemeral: true);

        if (!PendingModals.TryRemove(Context.User.Id, out var pending))
        {
            await FollowupAsync("Erreur : formulaire expiré.", ephemeral: true);
            return;
        }

        var target = await _client.GetUserAsync(pending.TargetId);
        if (target is null)
        {
            await FollowupAsync("Erreur : utilisateur introuvable.", ephemeral: true);
            return;
        }

        var details = string.IsNullOrWhiteSpace(modal.Other) ? null : modal.Other;
        var url = string.IsNullOrWhiteSpace(modal.Url) ? null : modal.Url;

        var record = new WarnRecord
        {
            Date = DateTime.UtcNow,
            FromUser = Context.User.Username,
            FromId = Context.User.Id,
            ToUser = target.Username,
            ToId = target.Id,
            Reason = modal.Reason,
            Details = details,
            Link = url,
            Action = pending.Action
        };

        // Send mod message, store, public message, DM (shared code)
        await WarnPlugin.ProcessWarnRecordAsync(Context.Guild, target, record);

        // Apply the sanction (only for bot-initiated actions)
        await ApplySanctionAsync(Context.Guild, target, record);

        await FollowupAsync("Sanction appliquée.", ephemeral: true);
    }

    // ── History button ──────────────────────────────────────────────

    [ComponentInteraction("warn_update_message")]
    [AllowedBotRole(BotRole.Helper)]
    public async Task HistoryButtonAsync()
    {
        var data = await WarnPlugin.LoadDataAsync();
        var messageLink = ((IComponentInteraction)Context.Interaction).Message.GetJumpUrl();

        foreach (var (_, records) in data.Warns)
        {
            if (!records.Any(r => r.FullMessageLink == messageLink))
                continue;

            var embed = new EmbedBuilder()
                .WithTitle($"{records.Count} warns");

            foreach (var warn in records)
            {
                var date = warn.Date.ToString("dd MMMM yyyy", new CultureInfo("fr-FR"));
                embed.AddField(
                    $"{WarnPlugin.FormatAction(warn.Action)} ({date})",
                    $"{WarnPlugin.Truncate(warn.Reason, 800)}\n{warn.FullMessageLink}",
                    false);

                if (embed.Fields.Count >= 25) break;
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);
            return;
        }
    }

    // ── Sanction application ────────────────────────────────────────

    private static async Task ApplySanctionAsync(SocketGuild guild, IUser target, WarnRecord record)
    {
        try
        {
            switch (record.Action)
            {
                case "kick":
                    var kickMember = guild.GetUser(target.Id);
                    if (kickMember is not null)
                    {
                        try { await kickMember.ModifyAsync(p => p.Channel = null); }
                        catch { /* not in voice */ }
                        await kickMember.KickAsync(record.Reason);
                    }
                    break;

                case "ban":
                    await guild.AddBanAsync(target.Id, 0, record.Reason);
                    break;

                case "ban_vocal":
                    var warnData = await WarnPlugin.LoadDataAsync();
                    if (warnData.BanVocal != 0)
                    {
                        var banVocalMember = guild.GetUser(target.Id);
                        if (banVocalMember is not null)
                            await banVocalMember.AddRoleAsync(warnData.BanVocal);
                    }
                    break;

                case "mute_1h":
                    var mute1h = guild.GetUser(target.Id);
                    if (mute1h is not null)
                        await mute1h.SetTimeOutAsync(TimeSpan.FromHours(1));
                    break;

                case "mute_1d":
                    var mute1d = guild.GetUser(target.Id);
                    if (mute1d is not null)
                        await mute1d.SetTimeOutAsync(TimeSpan.FromDays(1));
                    break;

                case "mute_1w":
                    var mute1w = guild.GetUser(target.Id);
                    if (mute1w is not null)
                        await mute1w.SetTimeOutAsync(TimeSpan.FromDays(7));
                    break;
            }
        }
        catch { /* Insufficient permissions or user already gone */ }
    }
}

public class WarnModal : IModal
{
    public string Title => "Sanction";

    [InputLabel("Raison")]
    [ModalTextInput("reason", TextInputStyle.Short, "Ce message sera transmis à la personne concernée")]
    public string Reason { get; set; } = "";

    [InputLabel("Autres informations")]
    [ModalTextInput("other", TextInputStyle.Paragraph, "Autres informations (ne sera pas transmis)")]
    [RequiredInput(false)]
    public string? Other { get; set; }

    [InputLabel("Url")]
    [ModalTextInput("url", TextInputStyle.Short, "Lien vers le message contextuel")]
    [RequiredInput(false)]
    public string? Url { get; set; }
}

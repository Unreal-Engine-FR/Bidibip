using System.Collections.Concurrent;
using System.Globalization;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Warn.Commands;

[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
public sealed class WarnModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    // Key: moderator user ID → (target user ID, action)
    // A user can only have one modal open at a time.
    private static readonly ConcurrentDictionary<ulong, (ulong TargetId, string Action)> PendingModals = new();

    public WarnModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
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
            var fieldTitle = $"{FormatAction(record.Action)} - {record.Date:dd/MM/yyyy HH:mm}";
            var fieldValue = Truncate(record.Reason, 800);

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

        var modalTitle = $"{FormatAction(action)} de {target.Username}";
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

        await ProcessSanctionAsync(target, pending.Action, modal.Reason, details, url);
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
                    $"{FormatAction(warn.Action)} ({date})",
                    $"{Truncate(warn.Reason, 800)}\n{warn.FullMessageLink}",
                    false);

                if (embed.Fields.Count >= 25) break;
            }

            await RespondAsync(embed: embed.Build(), ephemeral: true);
            return;
        }
    }

    // ── Core processing ─────────────────────────────────────────────

    private async Task ProcessSanctionAsync(
        IUser target, string action, string reason, string? details, string? link)
    {
        var guild = Context.Guild;
        if (guild is null)
        {
            await FollowupAsync("Cette commande ne peut être utilisée que dans un serveur.", ephemeral: true);
            return;
        }

        var moderator = Context.User;

        var record = new WarnRecord
        {
            Date = DateTime.UtcNow,
            FromUser = moderator.Username,
            FromId = moderator.Id,
            ToUser = target.Username,
            ToId = target.Id,
            Reason = reason,
            Details = details,
            Link = link,
            Action = action
        };

        // 1. Send moderation message (with "Historique" button)
        var modMessage = await SendModerationMessageAsync(guild, record);
        if (modMessage is not null)
            record.FullMessageLink = modMessage.GetJumpUrl();

        // 2. Store the record
        await WarnPlugin.StoreWarnAsync(record);

        // 3. Send public message
        await SendPublicMessageAsync(guild, record);

        // 4. DM the user
        await SendPrivateMessageAsync(target, guild.Name, record);

        // 5. Apply the sanction
        await ApplySanctionAsync(guild, target, record);
    }

    // ── Moderation message ──────────────────────────────────────────

    private async Task<IUserMessage?> SendModerationMessageAsync(SocketGuild guild, WarnRecord record)
    {
        var data = await WarnPlugin.LoadDataAsync();
        var channelId = data.ModerationChannel != 0
            ? data.ModerationChannel
            : _botConfig.Channels.StaffChannel;

        var channel = guild.GetTextChannel(channelId);
        if (channel is null)
            return null;

        var existingCount = 0;
        if (data.Warns.TryGetValue(record.ToId.ToString(), out var existing))
            existingCount = existing.Count;

        var embed = new EmbedBuilder()
            .WithTitle(FormatAction(record.Action))
            .WithDescription(Truncate(record.Reason, 4000))
            .WithColor(GetActionColor(record.Action))
            .WithTimestamp(record.Date);

        if (!string.IsNullOrEmpty(record.Details))
            embed.AddField("Details", Truncate(record.Details, 1024), false);

        if (!string.IsNullOrEmpty(record.Link))
            embed.AddField("Url", Truncate(record.Link, 1024), true);

        if (existingCount > 0)
            embed.AddField("Encore lui !", $"Déjà {existingCount} warn(s)", true);

        var adminRoleId = _botConfig.Roles.Administrator;
        var content = $"Sanction de {record.ToUser} (<@{record.ToId}>) par {record.FromUser} (<@{record.FromId}>) <@&{adminRoleId}>";

        var components = new ComponentBuilder()
            .WithButton("Historique", "warn_update_message", ButtonStyle.Secondary)
            .Build();

        try
        {
            return await channel.SendMessageAsync(text: content, embed: embed.Build(), components: components);
        }
        catch
        {
            return null;
        }
    }

    // ── Public message ──────────────────────────────────────────────

    private async Task SendPublicMessageAsync(SocketGuild guild, WarnRecord record)
    {
        var data = await WarnPlugin.LoadDataAsync();
        if (data.PublicWarnChannel == 0) return;

        var channel = guild.GetTextChannel(data.PublicWarnChannel);
        if (channel is null) return;

        EmbedBuilder? embed = record.Action switch
        {
            "ban" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été banni par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000)),
            "kick" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été kick par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000)),
            "ban_vocal" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été exclu du vocal par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000)),
            "mute_1h" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000))
                .AddField("durée", "une heure", true),
            "mute_1d" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000))
                .AddField("durée", "une journée", true),
            "mute_1w" => new EmbedBuilder()
                .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                .WithDescription(Truncate(record.Reason, 4000))
                .AddField("durée", "une semaine", true),
            _ => null // "warn" → no public message
        };

        if (embed is null) return;

        try
        {
            await channel.SendMessageAsync(embed: embed.Build());
        }
        catch { /* ignore */ }
    }

    // ── Private DM ──────────────────────────────────────────────────

    private async Task SendPrivateMessageAsync(IUser target, string serverName, WarnRecord record)
    {
        try
        {
            var dmChannel = await target.CreateDMChannelAsync();
            var reason = Truncate(record.Reason, 1000);

            var message = record.Action switch
            {
                "ban" =>
                    $"## Hello :wave:\nTu as été banni de **{serverName}** pour raison :\n\n> `{reason}`\n\nBonne continuation à toi ! :wave:",
                "kick" =>
                    $"## Hello :wave:\nTu as été exclu de **{serverName}** pour raison :\n\n> `{reason}`\n\nNous tolérerons ton retour à la seule condition que tu sois en mesure de respecter notre communauté. :point_up:\nBien à toi.",
                "ban_vocal" =>
                    $"## Hello :wave:\nTu as été banni des salons vocaux de **{serverName}**.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:",
                "mute_1h" =>
                    $"## Hello :wave:\nTu as été exclu de **{serverName}** pour une heure.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:",
                "mute_1d" =>
                    $"## Hello :wave:\nTu as été exclu de **{serverName}** pour un jour.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:",
                "mute_1w" =>
                    $"## Hello :wave:\nTu as été exclu de **{serverName}** pour une semaine.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:",
                _ => // warn (default)
                    $"## Hello :wave:\nJe suis le robot de **{serverName}**.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:"
            };

            await dmChannel.SendMessageAsync(message);
        }
        catch { /* User may have DMs disabled or has already left */ }
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

                // "warn" => no further action
            }
        }
        catch { /* Insufficient permissions or user already gone */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    internal static string FormatAction(string action) => action switch
    {
        "warn" => "warn",
        "ban_vocal" => "exclusion du vocal",
        "mute_1h" => "exclusion du serveur (1h)",
        "mute_1d" => "exclusion du serveur (1 jour)",
        "mute_1w" => "exclusion du serveur (une semaine)",
        "kick" => "kick",
        "ban" => "ban",
        _ => action
    };

    private static Color GetActionColor(string action) => action switch
    {
        "warn" => Color.Gold,
        "kick" => Color.Orange,
        "ban" => Color.DarkRed,
        "mute_1h" or "mute_1d" or "mute_1w" => Color.LightOrange,
        "ban_vocal" => Color.LightOrange,
        _ => Color.Red
    };

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;
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

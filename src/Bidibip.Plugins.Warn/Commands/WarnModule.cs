using System.Text;
using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Warn.Commands;

[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
public sealed class WarnModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _botConfig;

    private static readonly JsonSerializerOptions JsonOptions = PluginJsonOptions.Default;

    public WarnModule(DiscordSocketClient client, BotConfig botConfig)
    {
        _client = client;
        _botConfig = botConfig;
    }

    // ── Slash commands ──────────────────────────────────────────────

    [SlashCommand("sanction", "Appliquer une sanction a un utilisateur")]
    public async Task SanctionAsync(
        [Summary("utilisateur", "Utilisateur a sanctionner")] IUser target,
        [Summary("action", "Sanction a appliquer")]
        [Choice("Avertissement", "warn")]
        [Choice("Kick", "kick")]
        [Choice("Ban", "ban")]
        [Choice("Mute 1h", "mute_1h")]
        [Choice("Mute 1 jour", "mute_1d")]
        [Choice("Mute 1 semaine", "mute_1w")]
        string action,
        [Summary("raison", "Raison de la sanction")] string reason,
        [Summary("details", "Details supplementaires")] string? details = null,
        [Summary("lien", "Lien du message contextuel")] string? link = null)
    {
        await ProcessSanctionAsync(target, action, reason, details, link);
    }

    [SlashCommand("casier", "Voir l'historique des sanctions d'un utilisateur")]
    public async Task CasierAsync(
        [Summary("utilisateur", "Utilisateur dont on veut voir le casier")] IUser target)
    {
        var data = await LoadDataAsync();
        var userId = target.Id.ToString();

        if (!data.Warns.TryGetValue(userId, out var records) || records.Count == 0)
        {
            await FollowupAsync(
                embed: new EmbedBuilder()
                    .WithTitle($"Casier de {target.Username}")
                    .WithDescription("Aucune sanction enregistree.")
                    .WithColor(Color.Green)
                    .Build(),
                ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Casier de {target.Username} ({records.Count} sanction(s))")
            .WithColor(Color.Orange);

        foreach (var record in records.OrderByDescending(r => r.Date))
        {
            var fieldTitle = $"{FormatAction(record.Action)} - {record.Date:dd/MM/yyyy HH:mm}";
            var fieldValue = record.Reason.Length > 800
                ? record.Reason[..800] + "..."
                : record.Reason;

            if (!string.IsNullOrEmpty(record.FullMessageLink))
                fieldValue += $"\n{record.FullMessageLink}";

            embed.AddField(fieldTitle, fieldValue, false);

            // Discord embeds support max 25 fields
            if (embed.Fields.Count >= 25)
                break;
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    // ── User context menu commands ──────────────────────────────────

    [UserCommand("Avertir")]
    public async Task WarnContextAsync(IUser user)
    {
        await ProcessSanctionAsync(user, "warn", "Action rapide via menu contextuel", null, null);
    }

    [UserCommand("Kick")]
    public async Task KickContextAsync(IUser user)
    {
        await ProcessSanctionAsync(user, "kick", "Action rapide via menu contextuel", null, null);
    }

    [UserCommand("Ban")]
    public async Task BanContextAsync(IUser user)
    {
        await ProcessSanctionAsync(user, "ban", "Action rapide via menu contextuel", null, null);
    }

    // ── Core processing ─────────────────────────────────────────────

    private async Task ProcessSanctionAsync(
        IUser target, string action, string reason, string? details, string? link)
    {
        var guild = Context.Guild;
        if (guild is null)
        {
            await FollowupAsync("Cette commande ne peut etre utilisee que dans un serveur.", ephemeral: true);
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

        // 1. Send moderation embed and capture link
        var modMessage = await SendModerationMessageAsync(guild, record);
        if (modMessage is not null)
            record.FullMessageLink = modMessage.GetJumpUrl();

        // 2. Store the record
        await StoreWarnAsync(record);

        // 3. Try to DM the user before any destructive action
        await SendPrivateMessageAsync(target, guild.Name, record);

        // 4. Apply the sanction
        await ApplySanctionAsync(guild, target, record);

        // 5. Confirm to the moderator
        await FollowupAsync(
            embed: new EmbedBuilder()
                .WithTitle("Sanction appliquee")
                .WithDescription($"{FormatAction(action)} envers {target.Mention}")
                .WithColor(Color.Red)
                .Build(),
            ephemeral: true);
    }

    private async Task<IUserMessage?> SendModerationMessageAsync(SocketGuild guild, WarnRecord record)
    {
        var data = await LoadDataAsync();
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
            .WithDescription(record.Reason.Length > 4000 ? record.Reason[..4000] : record.Reason)
            .WithColor(GetActionColor(record.Action))
            .WithTimestamp(record.Date);

        if (!string.IsNullOrEmpty(record.Details))
            embed.AddField("Details", record.Details.Length > 1024 ? record.Details[..1024] : record.Details, false);

        if (!string.IsNullOrEmpty(record.Link))
            embed.AddField("Lien", record.Link.Length > 1024 ? record.Link[..1024] : record.Link, true);

        if (existingCount > 0)
            embed.AddField("Recidive", $"Deja {existingCount} sanction(s)", true);

        var adminRoleId = _botConfig.Roles.Administrator;
        var content = $"Sanction de **{record.ToUser}** (<@{record.ToId}>) par **{record.FromUser}** (<@{record.FromId}>) <@&{adminRoleId}>";

        try
        {
            return await channel.SendMessageAsync(text: content, embed: embed.Build());
        }
        catch
        {
            return null;
        }
    }

    private async Task SendPrivateMessageAsync(IUser target, string serverName, WarnRecord record)
    {
        try
        {
            var dmChannel = await target.CreateDMChannelAsync();
            var message = record.Action switch
            {
                "ban" => $"## Hello\nTu as ete banni de **{serverName}** pour raison :\n\n> `{Truncate(record.Reason, 1000)}`\n\nBonne continuation a toi !",
                "kick" => $"## Hello\nTu as ete exclu de **{serverName}** pour raison :\n\n> `{Truncate(record.Reason, 1000)}`\n\nNous tolererons ton retour a la seule condition que tu sois en mesure de respecter notre communaute.",
                "mute_1h" => $"## Hello\nTu as ete exclu de **{serverName}** pour une heure.\nJe tiens a te rappeler que certains comportements ne sont pas toleres sur notre communaute, a savoir :\n\n> `{Truncate(record.Reason, 1000)}`\n\nMerci de prendre cet avertissement en consideration.",
                "mute_1d" => $"## Hello\nTu as ete exclu de **{serverName}** pour un jour.\nJe tiens a te rappeler que certains comportements ne sont pas toleres sur notre communaute, a savoir :\n\n> `{Truncate(record.Reason, 1000)}`\n\nMerci de prendre cet avertissement en consideration.",
                "mute_1w" => $"## Hello\nTu as ete exclu de **{serverName}** pour une semaine.\nJe tiens a te rappeler que certains comportements ne sont pas toleres sur notre communaute, a savoir :\n\n> `{Truncate(record.Reason, 1000)}`\n\nMerci de prendre cet avertissement en consideration.",
                _ => $"## Hello\nJe suis le robot de **{serverName}**.\nJe tiens a te rappeler que certains comportements ne sont pas toleres sur notre communaute, a savoir :\n\n> `{Truncate(record.Reason, 1000)}`\n\nMerci de prendre cet avertissement en consideration."
            };

            await dmChannel.SendMessageAsync(message);
        }
        catch
        {
            // User may have DMs disabled or has already left
        }
    }

    private static async Task ApplySanctionAsync(SocketGuild guild, IUser target, WarnRecord record)
    {
        try
        {
            switch (record.Action)
            {
                case "kick":
                    var kickMember = guild.GetUser(target.Id);
                    if (kickMember is not null)
                        await kickMember.KickAsync(record.Reason);
                    break;

                case "ban":
                    await guild.AddBanAsync(target.Id, 0, record.Reason);
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

                // "warn" => no further action needed
            }
        }
        catch
        {
            // Insufficient permissions or user already gone
        }
    }

    // ── Persistence ─────────────────────────────────────────────────

    private async Task StoreWarnAsync(WarnRecord record)
    {
        var data = await LoadDataAsync();
        var key = record.ToId.ToString();

        if (!data.Warns.TryGetValue(key, out var list))
        {
            list = new List<WarnRecord>();
            data.Warns[key] = list;
        }

        list.Add(record);
        await SaveDataAsync(data);
    }

    private async Task<WarnData> LoadDataAsync()
    {
        var path = Path.Combine(WarnPlugin.DataPath, "warns.json");
        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<WarnData>(json, JsonOptions) ?? new WarnData();
        }

        var defaultData = new WarnData();
        Directory.CreateDirectory(WarnPlugin.DataPath);
        var defaultJson = JsonSerializer.Serialize(defaultData, JsonOptions);
        await File.WriteAllTextAsync(path, defaultJson);
        return defaultData;
    }

    private async Task SaveDataAsync(WarnData data)
    {
        var path = Path.Combine(WarnPlugin.DataPath, "warns.json");
        Directory.CreateDirectory(WarnPlugin.DataPath);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string FormatAction(string action) => action switch
    {
        "warn" => "Avertissement",
        "kick" => "Kick",
        "ban" => "Ban",
        "mute_1h" => "Exclusion (1 heure)",
        "mute_1d" => "Exclusion (1 jour)",
        "mute_1w" => "Exclusion (1 semaine)",
        _ => action
    };

    private static Color GetActionColor(string action) => action switch
    {
        "warn" => Color.Gold,
        "kick" => Color.Orange,
        "ban" => Color.DarkRed,
        "mute_1h" or "mute_1d" or "mute_1w" => Color.LightOrange,
        _ => Color.Red
    };

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;
}

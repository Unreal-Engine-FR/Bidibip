using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Warn;

[BidibipPlugin]
public sealed class WarnPlugin : IBidibipPlugin
{
    public string Name => "Warn";
    public string Description => "Sanctions & historique des remarques";

    internal static string DataPath { get; private set; } = "";
    internal static BotConfig BotConfig { get; private set; } = null!;
    internal static readonly JsonSerializerOptions JsonOptions = PluginJsonOptions.Default;

    private ILogger _logger = null!;

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        DataPath = context.DataPath;
        BotConfig = context.BotConfig;

        context.Events.OnUserJoined(HandleUserJoinedAsync);
        context.Events.OnAuditLogCreated(HandleAuditLogAsync);
        return Task.CompletedTask;
    }

    // ── Data persistence ─────────────────────────────────────────────

    internal static async Task<WarnData> LoadDataAsync()
    {
        var path = Path.Combine(DataPath, "warns.json");
        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<WarnData>(json, JsonOptions) ?? new WarnData();
        }
        return new WarnData();
    }

    internal static async Task SaveDataAsync(WarnData data)
    {
        var path = Path.Combine(DataPath, "warns.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    internal static async Task StoreWarnAsync(WarnRecord record)
    {
        var data = await LoadDataAsync();
        var key = record.ToId.ToString();

        if (!data.Warns.TryGetValue(key, out var list))
        {
            list = [];
            data.Warns[key] = list;
        }

        list.Add(record);
        await SaveDataAsync(data);
    }

    // ── Shared warn processing (used by both /sanction and audit log) ─

    internal static async Task ProcessWarnRecordAsync(IGuild guild, IUser? targetUser, WarnRecord record)
    {
        // 1. Send moderation message
        var modMessage = await SendModerationMessageAsync(guild, record);
        if (modMessage is not null)
            record.FullMessageLink = modMessage.GetJumpUrl();

        // 2. Store the record
        await StoreWarnAsync(record);

        // 3. Send public message
        await SendPublicMessageAsync(guild, record);

        // 4. DM the user (may fail if already kicked/banned)
        if (targetUser is not null)
            await SendPrivateMessageAsync(targetUser, guild.Name, record);
    }

    internal static async Task<IUserMessage?> SendModerationMessageAsync(IGuild guild, WarnRecord record)
    {
        var data = await LoadDataAsync();
        var channelId = data.ModerationChannel != 0
            ? data.ModerationChannel
            : BotConfig.Channels.StaffChannel;

        var channel = await guild.GetTextChannelAsync(channelId);
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

        var adminRoleId = BotConfig.Roles.Administrator;
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

    internal static async Task SendPublicMessageAsync(IGuild guild, WarnRecord record)
    {
        var data = await LoadDataAsync();
        if (data.PublicWarnChannel == 0) return;

        var channel = await guild.GetTextChannelAsync(data.PublicWarnChannel);
        if (channel is null) return;

        EmbedBuilder? embed;
        switch (record.Action)
        {
            case "ban":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été banni par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000));
                break;
            case "kick":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été kick par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000));
                break;
            case "ban_vocal":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été exclu du vocal par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000));
                break;
            case "mute_1h":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000))
                    .AddField("durée", "une heure", true);
                break;
            case "mute_1d":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000))
                    .AddField("durée", "une journée", true);
                break;
            case "mute_1w":
                embed = new EmbedBuilder()
                    .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                    .WithDescription(Truncate(record.Reason, 4000))
                    .AddField("durée", "une semaine", true);
                break;
            default:
                var dur = ExtractDuration(record.Action);
                if (dur is not null)
                {
                    embed = new EmbedBuilder()
                        .WithTitle($"{record.ToUser} a été exclu par {record.FromUser}")
                        .WithDescription(Truncate(record.Reason, 4000))
                        .AddField("durée", dur, true);
                }
                else
                {
                    return; // "warn" → no public message
                }
                break;
        }

        try { await channel.SendMessageAsync(embed: embed.Build()); }
        catch { /* ignore */ }
    }

    internal static async Task SendPrivateMessageAsync(IUser target, string serverName, WarnRecord record)
    {
        try
        {
            var dmChannel = await target.CreateDMChannelAsync();
            var reason = Truncate(record.Reason, 1000);

            string message;
            switch (record.Action)
            {
                case "ban":
                    message = $"## Hello :wave:\nTu as été banni de **{serverName}** pour raison :\n\n> `{reason}`\n\nBonne continuation à toi ! :wave:";
                    break;
                case "kick":
                    message = $"## Hello :wave:\nTu as été exclu de **{serverName}** pour raison :\n\n> `{reason}`\n\nNous tolérerons ton retour à la seule condition que tu sois en mesure de respecter notre communauté. :point_up:\nBien à toi.";
                    break;
                case "ban_vocal":
                    message = $"## Hello :wave:\nTu as été banni des salons vocaux de **{serverName}**.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    break;
                case "mute_1h":
                    message = $"## Hello :wave:\nTu as été exclu de **{serverName}** pour une heure.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    break;
                case "mute_1d":
                    message = $"## Hello :wave:\nTu as été exclu de **{serverName}** pour un jour.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    break;
                case "mute_1w":
                    message = $"## Hello :wave:\nTu as été exclu de **{serverName}** pour une semaine.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    break;
                default:
                    var dur = ExtractDuration(record.Action);
                    if (dur is not null)
                        message = $"## Hello :wave:\nTu as été exclu de **{serverName}** pour {dur}.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    else
                        message = $"## Hello :wave:\nJe suis le robot de **{serverName}**.\nJe tiens à te rappeler que certains comportements ne sont pas tolérés sur notre communauté, à savoir :\n\n> `{reason}`\n\nMerci de prendre cet avertissement en considération. :point_up:";
                    break;
            }

            await dmChannel.SendMessageAsync(message);
        }
        catch { /* User may have DMs disabled or has already left */ }
    }

    // ── Event handlers ───────────────────────────────────────────────

    private async Task HandleUserJoinedAsync(IGuildUser user)
    {
        try
        {
            var data = await LoadDataAsync();
            var userId = user.Id.ToString();

            if (!data.Warns.TryGetValue(userId, out var records) || records.Count == 0)
                return;

            var lastWarn = records.OrderByDescending(r => r.Date).First();

            var channel = await user.Guild.GetTextChannelAsync(BotConfig.Channels.StaffChannel);
            if (channel is null) return;

            await channel.SendMessageAsync(
                $"{user.Username} (<@{user.Id}>) vient de rejoindre le serveur avec {records.Count} warn(s) à son actif ! {lastWarn.FullMessageLink}");
        }
        catch { /* ignore */ }
    }

    private async Task HandleAuditLogAsync(AuditLogEntry entry)
    {
        try
        {
            if (entry.TargetId is null)
                return;

            var targetId = entry.TargetId.Value;

            string action;
            string details;
            switch (entry.ActionType)
            {
                case AuditLogActionType.Kick:
                    action = "kick";
                    details = "Kick manuel";
                    break;
                case AuditLogActionType.Ban:
                    action = "ban";
                    details = "Ban manuel";
                    break;
                case AuditLogActionType.MemberUpdate when entry.TimeoutUntil is not null:
                    var duration = entry.TimeoutUntil.Value - DateTimeOffset.UtcNow;
                    if (duration.TotalSeconds <= 0)
                        return;
                    action = FormatTimeoutAction(duration);
                    details = "Exclusion manuelle";
                    break;
                default:
                    return;
            }

            var fromUser = await entry.Guild.GetUserAsync(entry.UserId);
            var fromName = fromUser?.Username ?? entry.UserId.ToString();
            var toName = entry.TargetUser?.Username ?? targetId.ToString();

            var record = new WarnRecord
            {
                Date = DateTime.UtcNow,
                FromUser = fromName,
                FromId = entry.UserId,
                ToUser = toName,
                ToId = targetId,
                Reason = entry.Reason ?? "",
                Details = details,
                Action = action
            };

            // Use the same processing as /sanction (mod message + store + public + DM)
            await ProcessWarnRecordAsync(entry.Guild, entry.TargetUser, record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle audit log entry");
        }
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

    internal static Color GetActionColor(string action) => action switch
    {
        "warn" => Color.Gold,
        "kick" => Color.Orange,
        "ban" => Color.DarkRed,
        "mute_1h" or "mute_1d" or "mute_1w" => Color.LightOrange,
        "ban_vocal" => Color.LightOrange,
        _ => Color.Red
    };

    internal static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;

    private static string FormatTimeoutAction(TimeSpan duration)
    {
        var totalSeconds = (long)duration.TotalSeconds;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        var parts = new List<string>();
        if (hours > 0) parts.Add($"{hours}h");
        if (minutes > 0) parts.Add($"{minutes}mn");
        if (seconds > 0) parts.Add($"{seconds}s");

        var durationStr = string.Join(" ", parts);
        return $"exclusion du serveur pour ({durationStr})";
    }

    private static string? ExtractDuration(string action)
    {
        const string prefix = "exclusion du serveur pour (";
        if (action.StartsWith(prefix) && action.EndsWith(")"))
            return action[prefix.Length..^1].Trim();
        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

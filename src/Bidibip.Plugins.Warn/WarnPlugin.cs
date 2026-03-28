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
        catch
        {
            // ignore - user may have left or channel not found
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

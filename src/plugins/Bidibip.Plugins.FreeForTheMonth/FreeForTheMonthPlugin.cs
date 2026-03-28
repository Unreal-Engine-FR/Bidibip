using Bidibip.Plugin.Sdk;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.FreeForTheMonth;

[BidibipPlugin]
public sealed class FreeForTheMonthPlugin : IBidibipPlugin
{
    public string Name => "FreeForTheMonth";
    public string Description => "Annonces des assets gratuits temporaires sur Fab.com";

    internal static string DataPath { get; private set; } = "";
    internal static string ConfigPath => Path.Combine(DataPath, "config.json");

    internal static ILogger Logger { get; private set; } = null!;
    private DiscordSocketClient _client = null!;
    private ulong _guildId;
    private readonly CancellationTokenSource _cts = new();

    public async Task InitializeAsync(PluginContext context)
    {
        Logger = context.Logger;
        _client = context.Client;
        _guildId = context.BotConfig.GuildId;
        DataPath = context.DataPath;

        await PluginData.LoadAsync<FreeForTheMonthConfig>(ConfigPath);

        context.Events.OnBotReady(() =>
        {
            _ = BackgroundCheckLoop();
            return Task.CompletedTask;
        });
    }

    private async Task BackgroundCheckLoop()
    {
        await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await CheckAndAnnounceAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "FreeForTheMonth background check failed");
            }

            var config = await PluginData.LoadAsync<FreeForTheMonthConfig>(ConfigPath);
            var interval = TimeSpan.FromHours(Math.Max(1, config.CheckIntervalHours));
            await Task.Delay(interval, _cts.Token);
        }
    }

    private async Task CheckAndAnnounceAsync()
    {
        var config = await PluginData.LoadAsync<FreeForTheMonthConfig>(ConfigPath);
        if (config.Channel == 0) return;

        var listings = await FabListingService.FetchListingsAsync();
        if (listings.Count == 0) return;

        var currentUids = listings.Select(l => l.Uid).OrderBy(u => u).ToList();
        var knownUids = config.KnownListings.OrderBy(u => u).ToList();

        if (currentUids.SequenceEqual(knownUids)) return;

        var guild = _client.GetGuild(_guildId);
        var channel = guild?.GetTextChannel(config.Channel);
        if (channel is null) return;

        await PostAnnouncementAsync(channel, listings, config.NotifyFfmRole);

        config.KnownListings = currentUids;
        await PluginData.SaveAsync(ConfigPath, config);

        Logger.LogInformation("FreeForTheMonth: announced {Count} new free listings", listings.Count);
    }

    internal static async Task PostAnnouncementAsync(ITextChannel channel, List<FabListing> listings, ulong roleId)
    {
        var endDate = listings.FirstOrDefault()?.DiscountEnd;
        var endStr = endDate.HasValue
            ? $"<t:{endDate.Value.ToUnixTimeSeconds()}:R>"
            : "bientot";

        var roleMention = roleId != 0 ? $"<@&{roleId}>" : "";

        var header = $"{roleMention} **Nouveaux assets gratuits sur Fab !** (expire {endStr})\nhttps://www.fab.com/limited-time-free";

        var embeds = BuildEmbeds(listings);

        await channel.SendMessageAsync(
            header,
            embeds: embeds,
            allowedMentions: new AllowedMentions(AllowedMentionTypes.Roles));
    }

    internal static Embed[] BuildEmbeds(List<FabListing> listings)
    {
        var embeds = new List<Embed>();
        foreach (var listing in listings)
        {
            var formats = listing.Formats.Count > 0
                ? string.Join(", ", listing.Formats)
                : "N/A";

            var ratingStr = listing.TotalRatings > 0
                ? $"{listing.AverageRating:F1}/5 ({listing.TotalRatings} avis)"
                : "Pas encore note";

            var embed = new EmbedBuilder()
                .WithTitle(listing.Title)
                .WithUrl(listing.Url)
                .WithDescription($"Par **{listing.Seller}**")
                .AddField("Prix original", $"~~{listing.OriginalPrice:F2} EUR~~ **GRATUIT**", inline: true)
                .AddField("Formats", formats, inline: true)
                .AddField("Note", ratingStr, inline: true)
                .WithColor(Color.Green);

            if (listing.ThumbnailUrl is not null)
                embed.WithThumbnailUrl(listing.ThumbnailUrl);

            embeds.Add(embed.Build());
        }

        return embeds.ToArray();
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }
}

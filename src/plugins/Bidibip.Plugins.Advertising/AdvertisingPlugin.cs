using Bidibip.Plugin.Sdk;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Advertising;

[BidibipPlugin]
public sealed class AdvertisingPlugin : IBidibipPlugin
{
    public string Name => "Advertising";
    public string Description => "Job posting system with moderated approval flow.";

    internal static string ConfigPath { get; private set; } = null!;
    internal static BotConfig BotConfig { get; private set; } = null!;

    private ILogger _logger = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        BotConfig = context.BotConfig;
        ConfigPath = Path.Combine(context.DataPath, "config.json");

        await PluginData.LoadAsync<AdConfig>(ConfigPath);

        context.Events.OnMessageReceived(HandleMessageReceivedAsync);
    }

    // ── Config persistence ───────────────────────────────────────────

    internal static async Task<AdConfig> LoadConfigAsync() =>
        await PluginData.LoadAsync<AdConfig>(ConfigPath);

    internal static async Task SaveConfigAsync(AdConfig config) =>
        await PluginData.SaveAsync(ConfigPath, config);

    // ── Message handler (text input steps) ───────────────────────────

    private async Task HandleMessageReceivedAsync(IMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (message.Channel is not IThreadChannel thread)
            return;

        var threadId = thread.Id.ToString();
        var config = await LoadConfigAsync();

        if (!config.InProgress.TryGetValue(threadId, out var ad))
            return;

        if (ad.UserId != message.Author.Id)
            return;

        var text = message.Content.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            var currentStep = ad.Step;
            var stepDef = AdStepRegistry.GetTextStep(currentStep);
            if (stepDef is null || !stepDef.IsApplicable(ad))
                return;

            stepDef.SetValue(ad, text);

            await AdQuestions.EditQuestionWithAnswer(thread, ad, currentStep, text);

            try { await ((IUserMessage)message).DeleteAsync(); } catch { /* ignore */ }

            ad.Step = AdStepRegistry.FindNextMissingStep(ad);
            await AdQuestions.AdvanceAsync(thread, ad, config, message.Author);
            await SaveConfigAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing advertising step for thread {ThreadId}", threadId);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

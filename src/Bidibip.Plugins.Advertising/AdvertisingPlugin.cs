using System.Text.Json;
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
    internal static readonly JsonSerializerOptions JsonOptions = PluginJsonOptions.Default;

    private ILogger _logger = null!;

    public async Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        BotConfig = context.BotConfig;
        ConfigPath = Path.Combine(context.DataPath, "config.json");

        await EnsureConfigAsync();

        context.Events.OnMessageReceived(HandleMessageReceivedAsync);

        _logger.LogInformation("Advertising plugin initialized.");
    }

    // ── Config persistence ───────────────────────────────────────────

    private async Task EnsureConfigAsync()
    {
        if (File.Exists(ConfigPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var config = new AdConfig();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
        _logger.LogWarning("Advertising config not found, created default at {Path}.", ConfigPath);
    }

    internal static async Task<AdConfig> LoadConfigAsync()
    {
        if (!File.Exists(ConfigPath))
            return new AdConfig();

        var json = await File.ReadAllTextAsync(ConfigPath);
        return JsonSerializer.Deserialize<AdConfig>(json, JsonOptions) ?? new AdConfig();
    }

    internal static async Task SaveConfigAsync(AdConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

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

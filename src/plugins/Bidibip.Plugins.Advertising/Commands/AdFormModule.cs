using Bidibip.Plugin.Sdk.Permissions;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Advertising.Commands;

public sealed class AdFormModule : InteractionModuleBase<SocketInteractionContext>
{
    // ── Generic choice handler ────────────────────────────────────────
    // Button custom IDs follow the pattern: ad-c:{stepId}:{value}

    [ComponentInteraction("ad-c:*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task HandleChoiceAsync(string payload)
    {
        if (AdQuestions.IsRegenerating(Context.Channel.Id))
        {
            await RespondAsync("Une minute ! Merci d'attendre que ton annonce soit entièrement régénérée ici avant de la modifier.", ephemeral: true);
            return;
        }

        var sep = payload.IndexOf(':');
        if (sep < 0) return;

        var stepId = payload[..sep];
        var value = payload[(sep + 1)..];

        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStepRegistry.ClearDependentSteps(ad, stepId);

        var step = AdStepRegistry.GetStep(stepId);
        step?.SetValue(ad, value);

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, stepId, value);

        ad.Step = AdStepRegistry.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Generic skip handler ──────────────────────────────────────────
    // Button custom IDs follow the pattern: ad-skip:{stepId}

    [ComponentInteraction("ad-skip:*")]
    [AllowedBotRole(BotRole.Member)]
    public async Task HandleSkipAsync(string stepId)
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        var step = AdStepRegistry.GetTextStep(stepId);
        if (step is not { IsOptional: true }) return;

        step.SetValue(ad, null);
        step.OnSkip?.Invoke(ad);

        await AdQuestions.EditQuestionAsCleared(Context.Channel, ad, stepId);

        ad.Step = AdStepRegistry.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    private AdInProgress? FindAd(AdConfig config)
    {
        var threadId = Context.Channel.Id.ToString();
        return config.InProgress.TryGetValue(threadId, out var ad) ? ad : null;
    }
}

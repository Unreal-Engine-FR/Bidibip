using Bidibip.Plugin.Sdk;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Advertising.Commands;

public sealed class AdFormModule : InteractionModuleBase<SocketInteractionContext>
{
    // ── Contract type ─────────────────────────────────────────────────

    [ComponentInteraction("ad-contract-*")]
    public async Task SelectContractAsync(string contractType)
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "contract_type");
        ad.ContractType = contractType;

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "contract_type", contractType);

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Internship paid ───────────────────────────────────────────────

    [ComponentInteraction("ad-intern-paid-yes")]
    public async Task InternshipPaidYesAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "internship_paid");
        ad.HasCompensation = "yes";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "internship_paid", "yes");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-intern-paid-no")]
    public async Task InternshipPaidNoAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "internship_paid");
        ad.HasCompensation = "no";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "internship_paid", "no");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Role ──────────────────────────────────────────────────────────

    [ComponentInteraction("ad-role-worker")]
    public async Task RoleWorkerAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "role");
        ad.Role = "worker";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "role", "worker");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-role-recruiter")]
    public async Task RoleRecruiterAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "role");
        ad.Role = "recruiter";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "role", "recruiter");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Recruiter location ────────────────────────────────────────────

    [ComponentInteraction("ad-rloc-remote")]
    public async Task RecruiterLocationRemoteAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "recruiter_location");
        ad.LocationType = "remote";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "recruiter_location", "remote");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-rloc-flex")]
    public async Task RecruiterLocationFlexAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "recruiter_location");
        ad.LocationType = "flex";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "recruiter_location", "flex");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-rloc-on_site")]
    public async Task RecruiterLocationOnSiteAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "recruiter_location");
        ad.LocationType = "on_site";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "recruiter_location", "on_site");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Worker location ───────────────────────────────────────────────

    [ComponentInteraction("ad-wloc-remote")]
    public async Task WorkerLocationRemoteAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "worker_location");
        ad.WorkerLocationType = "remote";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "worker_location", "remote");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-wloc-anywhere")]
    public async Task WorkerLocationAnywhereAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "worker_location");
        ad.WorkerLocationType = "anywhere";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "worker_location", "anywhere");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-wloc-on_site")]
    public async Task WorkerLocationOnSiteAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "worker_location");
        ad.WorkerLocationType = "on_site";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "worker_location", "on_site");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Contact ───────────────────────────────────────────────────────

    [ComponentInteraction("ad-contact-discord")]
    public async Task ContactDiscordAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "contact");
        ad.ContactMethod = "discord";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "contact", "discord");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    [ComponentInteraction("ad-contact-other")]
    public async Task ContactOtherAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.ClearDependentFields(ad, "contact");
        ad.ContactMethod = "other";

        await AdQuestions.UpdateButtonStyles(Context.Channel, ad, "contact", "other");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Skip other URLs ──────────────────────────────────────────────

    [ComponentInteraction("ad-skip-urls")]
    public async Task SkipUrlsAsync()
    {
        await DeferAsync();
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        ad.OtherUrls = null;
        ad.OtherUrlsSkipped = true;

        await AdQuestions.EditQuestionAsCleared(Context.Channel, ad, "other_urls");

        ad.Step = AdStateMachine.FindNextMissingStep(ad);
        await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);
        await AdvertisingPlugin.SaveConfigAsync(config);
    }

    // ── Helper ────────────────────────────────────────────────────────

    private AdInProgress? FindAd(AdConfig config)
    {
        var threadId = Context.Channel.Id.ToString();
        return config.InProgress.TryGetValue(threadId, out var ad) ? ad : null;
    }
}

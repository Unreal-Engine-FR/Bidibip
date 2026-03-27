using Bidibip.Plugin.Sdk;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Advertising.Commands;

public sealed class AdEditModule : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("ad-field-edit-*")]
    public async Task EditFieldAsync(string stepName)
    {
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        ad.EditingStep = stepName;
        await AdvertisingPlugin.SaveConfigAsync(config);

        var currentValue = AdStateMachine.GetValueForStep(ad, stepName) ?? "";
        var questionText = AdStateMachine.GetQuestionText(stepName, ad);
        var modalTitle = questionText.Length > 45 ? questionText[..45] : questionText;

        var modal = new ModalBuilder()
            .WithTitle(modalTitle)
            .WithCustomId("ad-field-modal")
            .AddTextInput("Nouveau contenu", "text", TextInputStyle.Paragraph, placeholder: "Nouveau contenu", value: currentValue)
            .Build();

        await Context.Interaction.RespondWithModalAsync(modal);
    }

    [ModalInteraction("ad-field-modal")]
    public async Task EditFieldModalAsync(EditFieldModal modal)
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        var stepName = ad.EditingStep;
        if (string.IsNullOrEmpty(stepName))
        {
            await FollowupAsync("Erreur : aucun champ en cours d'\u00e9dition.", ephemeral: true);
            return;
        }

        ad.EditingStep = null;

        var newValue = modal.Text?.Trim();

        if (string.IsNullOrWhiteSpace(newValue))
        {
            await FollowupAsync("La valeur ne peut pas \u00eatre vide.", ephemeral: true);
            return;
        }

        AdStateMachine.SetValueForStep(ad, stepName, newValue);
        if (stepName == "other_urls")
            ad.OtherUrlsSkipped = false;

        await AdQuestions.EditQuestionWithAnswer(Context.Channel, ad, stepName, newValue);

        ad.Step = AdStateMachine.FindNextMissingStep(ad);

        if (ad.Step == "preview")
            await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);

        await AdvertisingPlugin.SaveConfigAsync(config);
        await FollowupAsync("Valeur mise \u00e0 jour !", ephemeral: true);
    }

    [ComponentInteraction("ad-field-clear-*")]
    public async Task ClearFieldAsync(string stepName)
    {
        await DeferAsync(ephemeral: true);
        var config = await AdvertisingPlugin.LoadConfigAsync();
        var ad = FindAd(config);
        if (ad is null) return;

        AdStateMachine.SetValueForStep(ad, stepName, null);
        if (stepName == "other_urls")
            ad.OtherUrlsSkipped = true;

        await AdQuestions.EditQuestionAsCleared(Context.Channel, ad, stepName);

        ad.Step = AdStateMachine.FindNextMissingStep(ad);

        if (ad.Step == "preview")
            await AdQuestions.AdvanceAsync(Context.Channel, ad, config, Context.User);

        await AdvertisingPlugin.SaveConfigAsync(config);
        await FollowupAsync("Valeur supprim\u00e9e !", ephemeral: true);
    }

    private AdInProgress? FindAd(AdConfig config)
    {
        var threadId = Context.Channel.Id.ToString();
        return config.InProgress.TryGetValue(threadId, out var ad) ? ad : null;
    }
}

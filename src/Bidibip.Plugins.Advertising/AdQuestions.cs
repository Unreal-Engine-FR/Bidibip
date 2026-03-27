using Discord;

namespace Bidibip.Plugins.Advertising;

internal static class AdQuestions
{
    // ── Advance: send next question or preview ───────────────────────

    public static async Task AdvanceAsync(IMessageChannel channel, AdInProgress ad, AdConfig config, IUser user)
    {
        if (ad.Step == "preview")
        {
            await AdPreview.SendPreviewAsync(channel, ad, user);
            return;
        }

        var stepDef = AdStepRegistry.GetStep(ad.Step);
        if (stepDef is null) return;

        IUserMessage msg = stepDef switch
        {
            TextStep text => await SendTextQuestion(channel, text, ad),
            ChoiceStep choice => await SendChoiceQuestion(channel, choice, ad),
            _ => throw new InvalidOperationException($"Unknown step type for '{ad.Step}'")
        };

        ad.QuestionMessages[ad.Step] = msg.Id;
    }

    // ── Question senders ─────────────────────────────────────────────

    private static async Task<IUserMessage> SendTextQuestion(IMessageChannel channel, TextStep step, AdInProgress ad)
    {
        var question = step.GetQuestionText(ad);

        if (step.IsOptional)
        {
            var components = new ComponentBuilder()
                .WithButton("Passer", $"ad-skip:{step.Id}", ButtonStyle.Secondary)
                .Build();

            return await channel.SendMessageAsync(
                $"## \u25b6  {question}\n> *\u00c9cris ta r\u00e9ponse sous ce message*",
                components: components);
        }

        return await channel.SendMessageAsync(
            $"## \u25b6  {question}\n> *\u00c9cris ta r\u00e9ponse sous ce message*");
    }

    private static async Task<IUserMessage> SendChoiceQuestion(IMessageChannel channel, ChoiceStep step, AdInProgress ad)
    {
        var builder = new ComponentBuilder();
        foreach (var option in step.Options)
        {
            builder.WithButton(option.Label, $"ad-c:{step.Id}:{option.Value}", ButtonStyle.Primary, row: option.Row);
        }

        return await channel.SendMessageAsync(
            $"## \u25b6  {step.GetQuestionText(ad)}",
            components: builder.Build());
    }

    // ── Question message editing ─────────────────────────────────────

    public static async Task EditQuestionWithAnswer(IMessageChannel channel, AdInProgress ad, string stepId, string value)
    {
        if (!ad.QuestionMessages.TryGetValue(stepId, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg) return;

            var step = AdStepRegistry.GetStep(stepId);
            var questionText = step?.GetQuestionText(ad) ?? stepId;
            var isOptional = (step as TextStep)?.IsOptional ?? false;
            var quotedValue = string.Join("\n", value.Split('\n').Select(l => $"> {l}"));

            var builder = new ComponentBuilder()
                .WithButton("Modifier", $"ad-field-edit-{stepId}", ButtonStyle.Primary);

            if (isOptional)
                builder.WithButton("Supprimer", $"ad-field-clear-{stepId}", ButtonStyle.Danger);

            await userMsg.ModifyAsync(m =>
            {
                m.Content = $"## \u25b6  {questionText}\n{quotedValue}";
                m.Components = builder.Build();
            });
        }
        catch { /* ignore if message not found */ }
    }

    public static async Task EditQuestionAsCleared(IMessageChannel channel, AdInProgress ad, string stepId)
    {
        if (!ad.QuestionMessages.TryGetValue(stepId, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg) return;

            var questionText = AdStepRegistry.GetStep(stepId)?.GetQuestionText(ad) ?? stepId;
            var builder = new ComponentBuilder()
                .WithButton("Modifier", $"ad-field-edit-{stepId}", ButtonStyle.Primary);

            await userMsg.ModifyAsync(m =>
            {
                m.Content = $"## \u25b6  {questionText}\n:negative_squared_cross_mark:";
                m.Components = builder.Build();
            });
        }
        catch { /* ignore */ }
    }

    public static async Task UpdateButtonStyles(IMessageChannel channel, AdInProgress ad, string stepId, string selectedValue)
    {
        if (!ad.QuestionMessages.TryGetValue(stepId, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg) return;

            var newComponents = new ComponentBuilder();
            int row = 0;
            foreach (var actionRow in userMsg.Components)
            {
                if (actionRow is ActionRowComponent arc)
                {
                    foreach (var component in arc.Components)
                    {
                        if (component is ButtonComponent button && button.CustomId is not null)
                        {
                            var isSelected = button.CustomId.EndsWith($":{selectedValue}");
                            newComponents.WithButton(
                                button.Label,
                                button.CustomId,
                                isSelected ? ButtonStyle.Success : ButtonStyle.Secondary,
                                button.Emote,
                                row: row);
                        }
                    }
                }
                row++;
            }

            await userMsg.ModifyAsync(m => m.Components = newComponents.Build());
        }
        catch { /* ignore */ }
    }
}

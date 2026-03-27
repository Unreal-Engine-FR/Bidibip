using Discord;

namespace Bidibip.Plugins.Advertising;

internal static class AdQuestions
{
    // ── Advance: send next question or preview ───────────────────────

    public static async Task AdvanceAsync(IMessageChannel channel, AdInProgress ad, AdConfig config, IUser user)
    {
        IUserMessage msg;
        switch (ad.Step)
        {
            case "title":
                msg = await SendTextQuestion(channel, "title", "Donne un titre \u00e0 ton annonce");
                ad.QuestionMessages["title"] = msg.Id;
                break;

            case "description":
                msg = await SendTextQuestion(channel, "description", "D\u00e9cris ton annonce, en quoi elle consiste, qui tu es etc...");
                ad.QuestionMessages["description"] = msg.Id;
                break;

            case "who_are_you":
                msg = await SendTextQuestion(channel, "who_are_you", "Qui es tu ? D\u00e9cris toi, ton entreprise, ton projet, ton exp\u00e9rience etc...");
                ad.QuestionMessages["who_are_you"] = msg.Id;
                break;

            case "contract_type":
                msg = await SendContractTypeButtons(channel);
                ad.QuestionMessages["contract_type"] = msg.Id;
                break;

            case "contract_duration":
                var durationLabel = ad.ContractType == "internship" ? "Dur\u00e9e du stage" : "Dur\u00e9e du contrat";
                msg = await SendTextQuestion(channel, "contract_duration", durationLabel);
                ad.QuestionMessages["contract_duration"] = msg.Id;
                break;

            case "contract_compensation":
                msg = await SendTextQuestion(channel, "contract_compensation", "R\u00e9mun\u00e9ration");
                ad.QuestionMessages["contract_compensation"] = msg.Id;
                break;

            case "internship_paid":
                msg = await SendInternshipPaidButtons(channel);
                ad.QuestionMessages["internship_paid"] = msg.Id;
                break;

            case "internship_gratification":
                msg = await SendTextQuestion(channel, "internship_gratification", "Quelle est la gratification ? (4,35\u20ac/h minimum pour un stage de plus de 10 semaines)");
                ad.QuestionMessages["internship_gratification"] = msg.Id;
                break;

            case "role":
                msg = await SendRoleButtons(channel);
                ad.QuestionMessages["role"] = msg.Id;
                break;

            case "recruiter_location":
                msg = await SendRecruiterLocationButtons(channel);
                ad.QuestionMessages["recruiter_location"] = msg.Id;
                break;

            case "recruiter_location_detail":
                msg = await SendTextQuestion(channel, "recruiter_location_detail", "Quelle est ta ville / r\u00e9gion ?");
                ad.QuestionMessages["recruiter_location_detail"] = msg.Id;
                break;

            case "recruiter_studio":
                msg = await SendTextQuestion(channel, "recruiter_studio", "Quel est le nom de ton entreprise / studio ?");
                ad.QuestionMessages["recruiter_studio"] = msg.Id;
                break;

            case "recruiter_responsibilities":
                msg = await SendTextQuestion(channel, "recruiter_responsibilities", "Quelles sont les responsabilit\u00e9es demand\u00e9es ?");
                ad.QuestionMessages["recruiter_responsibilities"] = msg.Id;
                break;

            case "recruiter_qualifications":
                msg = await SendTextQuestion(channel, "recruiter_qualifications", "Quelles sont les comp\u00e9tences requises ?");
                ad.QuestionMessages["recruiter_qualifications"] = msg.Id;
                break;

            case "worker_location":
                msg = await SendWorkerLocationButtons(channel);
                ad.QuestionMessages["worker_location"] = msg.Id;
                break;

            case "worker_location_detail":
                msg = await SendTextQuestion(channel, "worker_location_detail", "Indique ta ville / r\u00e9gion");
                ad.QuestionMessages["worker_location_detail"] = msg.Id;
                break;

            case "worker_skills":
                msg = await SendTextQuestion(channel, "worker_skills", "Quelles sont tes comp\u00e9tences ?");
                ad.QuestionMessages["worker_skills"] = msg.Id;
                break;

            case "contact":
                msg = await SendContactButtons(channel);
                ad.QuestionMessages["contact"] = msg.Id;
                break;

            case "contact_info":
                msg = await SendTextQuestion(channel, "contact_info", "Indique au moins un moyen de contact (mail etc...)");
                ad.QuestionMessages["contact_info"] = msg.Id;
                break;

            case "other_urls":
                msg = await SendOtherUrlsQuestion(channel);
                ad.QuestionMessages["other_urls"] = msg.Id;
                break;

            case "preview":
                await AdPreview.SendPreviewAsync(channel, ad, user);
                await AdvertisingPlugin.SaveConfigAsync(config);
                break;
        }
    }

    // ── Text question helper ─────────────────────────────────────────

    private static Task<IUserMessage> SendTextQuestion(IMessageChannel channel, string step, string label)
    {
        return channel.SendMessageAsync($"## \u25b6  {label}\n> *\u00c9cris ta r\u00e9ponse sous ce message*");
    }

    // ── Button question builders ─────────────────────────────────────

    private static async Task<IUserMessage> SendContractTypeButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("\ud83e\udd1d B\u00e9n\u00e9volat (non r\u00e9mun\u00e9r\u00e9)", "ad-contract-volunteering", Style("volunteering"))
            .WithButton("\ud83e\ude82 Stage", "ad-contract-internship", Style("internship"))
            .WithButton("\ud83e\udd13 Alternance (r\u00e9mun\u00e9r\u00e9)", "ad-contract-work_study", Style("work_study"))
            .WithButton("\ud83e\uddd0 Freelance", "ad-contract-freelance", Style("freelance"), row: 1)
            .WithButton("\ud83d\ude0e CDD (r\u00e9mun\u00e9r\u00e9)", "ad-contract-fixed_term", Style("fixed_term"), row: 1)
            .WithButton("\ud83e\udd2f CDI (r\u00e9mun\u00e9r\u00e9)", "ad-contract-open_ended", Style("open_ended"), row: 1)
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Quel type de contrat recherches-tu ?", components: components);
    }

    private static async Task<IUserMessage> SendInternshipPaidButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("Oui", "ad-intern-paid-yes", Style("yes"))
            .WithButton("Non", "ad-intern-paid-no", Style("no"))
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Le stage est-il r\u00e9mun\u00e9r\u00e9 ?", components: components);
    }

    private static async Task<IUserMessage> SendRoleButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("\ud83d\udd27 Je cherche du travail", "ad-role-worker", Style("worker"))
            .WithButton("\ud83d\udd75\ufe0f\u200d\u2640\ufe0f Je recrute", "ad-role-recruiter", Style("recruiter"))
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Es-tu recruteur ou recherches tu du travail ?", components: components);
    }

    private static async Task<IUserMessage> SendRecruiterLocationButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("\ud83c\udf0d Distanciel", "ad-rloc-remote", Style("remote"))
            .WithButton("\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible", "ad-rloc-flex", Style("flex"))
            .WithButton("\ud83c\udfe3 Pr\u00e9sentiel uniquement", "ad-rloc-on_site", Style("on_site"))
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Quelles sont les modalit\u00e9s de travail ?", components: components);
    }

    private static async Task<IUserMessage> SendWorkerLocationButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("\ud83c\udf0d Distanciel", "ad-wloc-remote", Style("remote"))
            .WithButton("\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible", "ad-wloc-anywhere", Style("anywhere"))
            .WithButton("\ud83c\udfe3 Pr\u00e9sentiel uniquement", "ad-wloc-on_site", Style("on_site"))
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Souhaites-tu travailler \u00e0 distance ou en pr\u00e9sentiel ?", components: components);
    }

    private static async Task<IUserMessage> SendContactButtons(IMessageChannel channel, string? selected = null)
    {
        ButtonStyle Style(string value) => selected == value ? ButtonStyle.Success : selected is not null ? ButtonStyle.Secondary : ButtonStyle.Primary;

        var components = new ComponentBuilder()
            .WithButton("Discord", "ad-contact-discord", Style("discord"))
            .WithButton("Autre", "ad-contact-other", Style("other"))
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Comment peut-on te contacter ?", components: components);
    }

    private static async Task<IUserMessage> SendOtherUrlsQuestion(IMessageChannel channel)
    {
        var components = new ComponentBuilder()
            .WithButton("Passer", "ad-skip-urls", ButtonStyle.Secondary)
            .Build();

        return await channel.SendMessageAsync("## \u25b6  Ajoutes d'autres informations (liens etc...)\n> *\u00c9cris ta r\u00e9ponse sous ce message*", components: components);
    }

    // ── Question message editing ─────────────────────────────────────

    /// <summary>
    /// Edits the question message to show the user's answer with Modifier/Supprimer buttons.
    /// </summary>
    public static async Task EditQuestionWithAnswer(IMessageChannel channel, AdInProgress ad, string step, string value)
    {
        if (!ad.QuestionMessages.TryGetValue(step, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg)
                return;

            var questionText = AdStateMachine.GetQuestionText(step, ad);
            var isOptional = step == "other_urls";
            var quotedValue = string.Join("\n", value.Split('\n').Select(l => $"> {l}"));

            var builder = new ComponentBuilder()
                .WithButton("Modifier", $"ad-field-edit-{step}", ButtonStyle.Primary);

            if (isOptional)
                builder.WithButton("Supprimer", $"ad-field-clear-{step}", ButtonStyle.Danger);

            await userMsg.ModifyAsync(m =>
            {
                m.Content = $"## \u25b6  {questionText}\n{quotedValue}";
                m.Components = builder.Build();
            });
        }
        catch { /* ignore if message not found */ }
    }

    /// <summary>
    /// Edits the question message to show it was cleared/skipped.
    /// </summary>
    public static async Task EditQuestionAsCleared(IMessageChannel channel, AdInProgress ad, string step)
    {
        if (!ad.QuestionMessages.TryGetValue(step, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg)
                return;

            var questionText = AdStateMachine.GetQuestionText(step, ad);
            var builder = new ComponentBuilder()
                .WithButton("Modifier", $"ad-field-edit-{step}", ButtonStyle.Primary);

            await userMsg.ModifyAsync(m =>
            {
                m.Content = $"## \u25b6  {questionText}\n:negative_squared_cross_mark:";
                m.Components = builder.Build();
            });
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Updates button styles on a button-choice question message:
    /// the selected button becomes Success, others become Secondary.
    /// </summary>
    public static async Task UpdateButtonStyles(IMessageChannel channel, AdInProgress ad, string step, string selectedValue)
    {
        if (!ad.QuestionMessages.TryGetValue(step, out var msgId))
            return;

        try
        {
            var msg = await channel.GetMessageAsync(msgId);
            if (msg is not IUserMessage userMsg)
                return;

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
                            var isSelected = button.CustomId.EndsWith($"-{selectedValue}");
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

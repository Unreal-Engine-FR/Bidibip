namespace Bidibip.Plugins.Advertising;

/// <summary>
/// Declarative registry of every step in the ad creation flow.
/// Adding a step = adding one entry here. No other file needs changing.
/// </summary>
internal static class AdStepRegistry
{
    public static readonly AdStepDefinition[] Steps =
    [
        // ── Free-text steps ───────────────────────────────────────────

        new TextStep("title",
            "Donne un titre \u00e0 ton annonce",
            ad => ad.Title, (ad, v) => ad.Title = v),

        new TextStep("description",
            "D\u00e9cris ton annonce, en quoi elle consiste, qui tu es etc...",
            ad => ad.Description, (ad, v) => ad.Description = v),

        new TextStep("who_are_you",
            "Qui es tu ? D\u00e9cris toi, ton entreprise, ton projet, ton exp\u00e9rience etc...",
            ad => ad.WhoAreYou, (ad, v) => ad.WhoAreYou = v),

        // ── Contract type ─────────────────────────────────────────────

        new ChoiceStep("contract_type",
            "Quel type de contrat recherches-tu ?",
            ad => ad.ContractType, (ad, v) => ad.ContractType = v,
            [
                new("volunteering", "\ud83e\udd1d B\u00e9n\u00e9volat (non r\u00e9mun\u00e9r\u00e9)"),
                new("internship",   "\ud83e\ude82 Stage"),
                new("work_study",   "\ud83e\udd13 Alternance (r\u00e9mun\u00e9r\u00e9)"),
                new("freelance",    "\ud83e\uddd0 Freelance",                    Row: 1),
                new("fixed_term",   "\ud83d\ude0e CDD (r\u00e9mun\u00e9r\u00e9)", Row: 1),
                new("open_ended",   "\ud83e\udd2f CDI (r\u00e9mun\u00e9r\u00e9)", Row: 1),
            ])
        {
            DependentStepIds = ["contract_duration", "contract_compensation", "internship_paid", "internship_gratification"]
        },

        new TextStep("contract_duration",
            "Dur\u00e9e du contrat",
            ad => ad.Duration, (ad, v) => ad.Duration = v,
            applicable: ad => ad.ContractType is "internship" or "freelance" or "work_study" or "fixed_term",
            dynamicLabel: ad => ad.ContractType == "internship" ? "Dur\u00e9e du stage" : "Dur\u00e9e du contrat"),

        new ChoiceStep("internship_paid",
            "Le stage est-il r\u00e9mun\u00e9r\u00e9 ?",
            ad => ad.HasCompensation, (ad, v) => ad.HasCompensation = v,
            [new("yes", "Oui"), new("no", "Non")],
            applicable: ad => ad.ContractType == "internship")
        {
            DependentStepIds = ["internship_gratification"]
        },

        new TextStep("internship_gratification",
            "Quelle est la gratification ? (4,35\u20ac/h minimum pour un stage de plus de 10 semaines)",
            ad => ad.Compensation, (ad, v) => ad.Compensation = v,
            applicable: ad => ad.ContractType == "internship" && ad.HasCompensation == "yes"),

        new TextStep("contract_compensation",
            "R\u00e9mun\u00e9ration",
            ad => ad.Compensation, (ad, v) => ad.Compensation = v,
            applicable: ad => ad.ContractType is "freelance" or "work_study" or "fixed_term" or "open_ended"),

        // ── Role ──────────────────────────────────────────────────────

        new ChoiceStep("role",
            "Es-tu recruteur ou recherches tu du travail ?",
            ad => ad.Role, (ad, v) => ad.Role = v,
            [
                new("worker",    "\ud83d\udd27 Je cherche du travail"),
                new("recruiter", "\ud83d\udd75\ufe0f\u200d\u2640\ufe0f Je recrute"),
            ])
        {
            DependentStepIds =
            [
                "recruiter_location", "recruiter_location_detail", "recruiter_studio",
                "recruiter_responsibilities", "recruiter_qualifications",
                "worker_location", "worker_location_detail", "worker_skills"
            ]
        },

        // ── Recruiter path ────────────────────────────────────────────

        new ChoiceStep("recruiter_location",
            "Quelles sont les modalit\u00e9s de travail ?",
            ad => ad.LocationType, (ad, v) => ad.LocationType = v,
            [
                new("remote",  "\ud83c\udf0d Distanciel"),
                new("flex",    "\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible"),
                new("on_site", "\ud83c\udfe3 Pr\u00e9sentiel uniquement"),
            ],
            applicable: ad => ad.Role == "recruiter")
        {
            DependentStepIds = ["recruiter_location_detail"]
        },

        new TextStep("recruiter_location_detail",
            "Quelle est ta ville / r\u00e9gion ?",
            ad => ad.LocationDetail, (ad, v) => ad.LocationDetail = v,
            applicable: ad => ad.Role == "recruiter" && ad.LocationType is "flex" or "on_site"),

        new TextStep("recruiter_studio",
            "Quel est le nom de ton entreprise / studio ?",
            ad => ad.StudioName, (ad, v) => ad.StudioName = v,
            applicable: ad => ad.Role == "recruiter"),

        new TextStep("recruiter_responsibilities",
            "Quelles sont les responsabilit\u00e9es demand\u00e9es ?",
            ad => ad.Responsibilities, (ad, v) => ad.Responsibilities = v,
            applicable: ad => ad.Role == "recruiter"),

        new TextStep("recruiter_qualifications",
            "Quelles sont les comp\u00e9tences requises ?",
            ad => ad.Qualifications, (ad, v) => ad.Qualifications = v,
            applicable: ad => ad.Role == "recruiter"),

        // ── Worker path ───────────────────────────────────────────────

        new ChoiceStep("worker_location",
            "Souhaites-tu travailler \u00e0 distance ou en pr\u00e9sentiel ?",
            ad => ad.WorkerLocationType, (ad, v) => ad.WorkerLocationType = v,
            [
                new("remote",   "\ud83c\udf0d Distanciel"),
                new("anywhere", "\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible"),
                new("on_site",  "\ud83c\udfe3 Pr\u00e9sentiel uniquement"),
            ],
            applicable: ad => ad.Role == "worker")
        {
            DependentStepIds = ["worker_location_detail"]
        },

        new TextStep("worker_location_detail",
            "Indique ta ville / r\u00e9gion",
            ad => ad.WorkerLocationDetail, (ad, v) => ad.WorkerLocationDetail = v,
            applicable: ad => ad.Role == "worker" && ad.WorkerLocationType is "anywhere" or "on_site"),

        new TextStep("worker_skills",
            "Quelles sont tes comp\u00e9tences ?",
            ad => ad.Skills, (ad, v) => ad.Skills = v,
            applicable: ad => ad.Role == "worker"),

        // ── Contact ───────────────────────────────────────────────────

        new ChoiceStep("contact",
            "Comment peut-on te contacter ?",
            ad => ad.ContactMethod, (ad, v) => ad.ContactMethod = v,
            [new("discord", "Discord"), new("other", "Autre")])
        {
            DependentStepIds = ["contact_info"]
        },

        new TextStep("contact_info",
            "Indique au moins un moyen de contact (mail etc...)",
            ad => ad.ContactInfo, (ad, v) => ad.ContactInfo = v,
            applicable: ad => ad.ContactMethod == "other"),

        // ── Optional extras ───────────────────────────────────────────

        new TextStep("other_urls",
            "Ajoutes d'autres informations (liens etc...)",
            ad => ad.OtherUrls,
            (ad, v) => { ad.OtherUrls = v; if (v is not null) ad.OtherUrlsSkipped = false; })
        {
            IsOptional = true,
            IsSkippedCheck = ad => ad.OtherUrlsSkipped,
            OnSkip = ad => ad.OtherUrlsSkipped = true,
        },
    ];

    private static readonly Dictionary<string, AdStepDefinition> ById =
        Steps.ToDictionary(s => s.Id);

    // ── Lookups ──────────────────────────────────────────────────────

    public static AdStepDefinition? GetStep(string id) =>
        ById.GetValueOrDefault(id);

    public static TextStep? GetTextStep(string id) =>
        ById.GetValueOrDefault(id) as TextStep;

    public static ChoiceStep? GetChoiceStep(string id) =>
        ById.GetValueOrDefault(id) as ChoiceStep;

    // ── Navigation ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the first incomplete, applicable step — or "preview" if all done.
    /// </summary>
    public static string FindNextMissingStep(AdInProgress ad)
    {
        foreach (var step in Steps)
        {
            if (step.IsApplicable(ad) && !step.IsCompleted(ad))
                return step.Id;
        }
        return "preview";
    }

    /// <summary>
    /// Clears every dependent step's value and question message when a choice changes.
    /// </summary>
    public static void ClearDependentSteps(AdInProgress ad, string changedStepId)
    {
        if (GetStep(changedStepId) is not ChoiceStep choice)
            return;

        foreach (var depId in choice.DependentStepIds)
        {
            GetStep(depId)?.SetValue(ad, null);
            ad.QuestionMessages.Remove(depId);
        }
    }
}

namespace Bidibip.Plugins.Advertising;

internal static class AdStateMachine
{
    /// <summary>
    /// Attempts to consume a text message for the current step.
    /// Returns true if the step was handled and the state machine should advance.
    /// </summary>
    public static bool ProcessTextStep(AdInProgress ad, string text)
    {
        switch (ad.Step)
        {
            case "title": ad.Title = text; return true;
            case "description": ad.Description = text; return true;
            case "who_are_you": ad.WhoAreYou = text; return true;
            case "contract_duration": ad.Duration = text; return true;
            case "contract_compensation": ad.Compensation = text; return true;
            case "internship_gratification": ad.Compensation = text; return true;
            case "recruiter_location_detail": ad.LocationDetail = text; return true;
            case "recruiter_studio": ad.StudioName = text; return true;
            case "recruiter_responsibilities": ad.Responsibilities = text; return true;
            case "recruiter_qualifications": ad.Qualifications = text; return true;
            case "worker_location_detail": ad.WorkerLocationDetail = text; return true;
            case "worker_skills": ad.Skills = text; return true;
            case "contact_info": ad.ContactInfo = text; return true;
            case "other_urls":
                ad.OtherUrls = text;
                ad.OtherUrlsSkipped = false;
                return true;
            default: return false;
        }
    }

    /// <summary>
    /// Scans through all steps in order and returns the first one with a missing value.
    /// If all are filled, returns "preview".
    /// </summary>
    public static string FindNextMissingStep(AdInProgress ad)
    {
        if (string.IsNullOrEmpty(ad.Title)) return "title";
        if (string.IsNullOrEmpty(ad.Description)) return "description";
        if (string.IsNullOrEmpty(ad.WhoAreYou)) return "who_are_you";
        if (string.IsNullOrEmpty(ad.ContractType)) return "contract_type";

        // Contract-specific
        if (ad.ContractType is "internship" or "freelance" or "work_study" or "fixed_term" && string.IsNullOrEmpty(ad.Duration))
            return "contract_duration";
        if (ad.ContractType == "internship" && string.IsNullOrEmpty(ad.HasCompensation))
            return "internship_paid";
        if (ad.ContractType == "internship" && ad.HasCompensation == "yes" && string.IsNullOrEmpty(ad.Compensation))
            return "internship_gratification";
        if (ad.ContractType is "freelance" or "work_study" or "fixed_term" or "open_ended" && string.IsNullOrEmpty(ad.Compensation))
            return "contract_compensation";

        if (string.IsNullOrEmpty(ad.Role)) return "role";

        // Role-specific
        if (ad.Role == "recruiter")
        {
            if (string.IsNullOrEmpty(ad.LocationType)) return "recruiter_location";
            if (ad.LocationType is "flex" or "on_site" && string.IsNullOrEmpty(ad.LocationDetail))
                return "recruiter_location_detail";
            if (string.IsNullOrEmpty(ad.StudioName)) return "recruiter_studio";
            if (string.IsNullOrEmpty(ad.Responsibilities)) return "recruiter_responsibilities";
            if (string.IsNullOrEmpty(ad.Qualifications)) return "recruiter_qualifications";
        }
        if (ad.Role == "worker")
        {
            if (string.IsNullOrEmpty(ad.WorkerLocationType)) return "worker_location";
            if (ad.WorkerLocationType is "anywhere" or "on_site" && string.IsNullOrEmpty(ad.WorkerLocationDetail))
                return "worker_location_detail";
            if (string.IsNullOrEmpty(ad.Skills)) return "worker_skills";
        }

        if (string.IsNullOrEmpty(ad.ContactMethod)) return "contact";
        if (ad.ContactMethod == "other" && string.IsNullOrEmpty(ad.ContactInfo))
            return "contact_info";
        if (!ad.OtherUrlsSkipped && string.IsNullOrEmpty(ad.OtherUrls))
            return "other_urls";

        return "preview";
    }

    /// <summary>Returns the French question text for a given step.</summary>
    public static string GetQuestionText(string step, AdInProgress ad) => step switch
    {
        "title" => "Donne un titre \u00e0 ton annonce",
        "description" => "D\u00e9cris ton annonce, en quoi elle consiste, qui tu es etc...",
        "who_are_you" => "Qui es tu ? D\u00e9cris toi, ton entreprise, ton projet, ton exp\u00e9rience etc...",
        "contract_duration" => ad.ContractType == "internship" ? "Dur\u00e9e du stage" : "Dur\u00e9e du contrat",
        "contract_compensation" => "R\u00e9mun\u00e9ration",
        "internship_gratification" => "Quelle est la gratification ? (4,35\u20ac/h minimum pour un stage de plus de 10 semaines)",
        "recruiter_location_detail" => "Quelle est ta ville / r\u00e9gion ?",
        "recruiter_studio" => "Quel est le nom de ton entreprise / studio ?",
        "recruiter_responsibilities" => "Quelles sont les responsabilit\u00e9es demand\u00e9es ?",
        "recruiter_qualifications" => "Quelles sont les comp\u00e9tences requises ?",
        "worker_location_detail" => "Indique ta ville / r\u00e9gion",
        "worker_skills" => "Quelles sont tes comp\u00e9tences ?",
        "contact_info" => "Indique au moins un moyen de contact (mail etc...)",
        "other_urls" => "Ajoutes d'autres informations (liens etc...)",
        _ => step
    };

    /// <summary>Gets the current value for a text step.</summary>
    public static string? GetValueForStep(AdInProgress ad, string step) => step switch
    {
        "title" => ad.Title,
        "description" => ad.Description,
        "who_are_you" => ad.WhoAreYou,
        "contract_duration" => ad.Duration,
        "contract_compensation" => ad.Compensation,
        "internship_gratification" => ad.Compensation,
        "recruiter_location_detail" => ad.LocationDetail,
        "recruiter_studio" => ad.StudioName,
        "recruiter_responsibilities" => ad.Responsibilities,
        "recruiter_qualifications" => ad.Qualifications,
        "worker_location_detail" => ad.WorkerLocationDetail,
        "worker_skills" => ad.Skills,
        "contact_info" => ad.ContactInfo,
        "other_urls" => ad.OtherUrls,
        _ => null
    };

    /// <summary>Sets the value for a text step.</summary>
    public static void SetValueForStep(AdInProgress ad, string step, string? value)
    {
        switch (step)
        {
            case "title": ad.Title = value; break;
            case "description": ad.Description = value; break;
            case "who_are_you": ad.WhoAreYou = value; break;
            case "contract_duration": ad.Duration = value; break;
            case "contract_compensation": ad.Compensation = value; break;
            case "internship_gratification": ad.Compensation = value; break;
            case "recruiter_location_detail": ad.LocationDetail = value; break;
            case "recruiter_studio": ad.StudioName = value; break;
            case "recruiter_responsibilities": ad.Responsibilities = value; break;
            case "recruiter_qualifications": ad.Qualifications = value; break;
            case "worker_location_detail": ad.WorkerLocationDetail = value; break;
            case "worker_skills": ad.Skills = value; break;
            case "contact_info": ad.ContactInfo = value; break;
            case "other_urls": ad.OtherUrls = value; break;
        }
    }

    /// <summary>Clears dependent fields when a choice step is changed.</summary>
    public static void ClearDependentFields(AdInProgress ad, string changedStep)
    {
        switch (changedStep)
        {
            case "contract_type":
                ad.Duration = null;
                ad.Compensation = null;
                ad.HasCompensation = null;
                RemoveQuestionMessages(ad, "contract_duration", "contract_compensation", "internship_paid", "internship_gratification");
                break;
            case "role":
                ad.LocationType = null;
                ad.LocationDetail = null;
                ad.StudioName = null;
                ad.Responsibilities = null;
                ad.Qualifications = null;
                ad.WorkerLocationType = null;
                ad.WorkerLocationDetail = null;
                ad.Skills = null;
                RemoveQuestionMessages(ad,
                    "recruiter_location", "recruiter_location_detail", "recruiter_studio",
                    "recruiter_responsibilities", "recruiter_qualifications",
                    "worker_location", "worker_location_detail", "worker_skills");
                break;
            case "recruiter_location":
                ad.LocationDetail = null;
                RemoveQuestionMessages(ad, "recruiter_location_detail");
                break;
            case "worker_location":
                ad.WorkerLocationDetail = null;
                RemoveQuestionMessages(ad, "worker_location_detail");
                break;
            case "internship_paid":
                ad.Compensation = null;
                RemoveQuestionMessages(ad, "internship_gratification");
                break;
            case "contact":
                ad.ContactInfo = null;
                RemoveQuestionMessages(ad, "contact_info");
                break;
        }
    }

    public static void RemoveQuestionMessages(AdInProgress ad, params string[] steps)
    {
        foreach (var step in steps)
            ad.QuestionMessages.Remove(step);
    }
}

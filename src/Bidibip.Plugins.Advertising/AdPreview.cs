using Discord;

namespace Bidibip.Plugins.Advertising;

internal static class AdPreview
{
    private static readonly Color PurpleColor = new(0x9B, 0x59, 0xB6);

    public static async Task SendPreviewAsync(IMessageChannel channel, AdInProgress ad, IUser user)
    {
        var embeds = BuildPreviewEmbeds(ad, user);

        var components = new ComponentBuilder()
            .WithButton("Soumetre l'annonce", "ad-pre-publish", ButtonStyle.Primary)
            .Build();

        // Delete previous preview if exists
        if (ad.PreviewMessageId.HasValue)
        {
            try
            {
                var oldMsg = await channel.GetMessageAsync(ad.PreviewMessageId.Value);
                if (oldMsg is IUserMessage userMsg)
                    await userMsg.DeleteAsync();
            }
            catch { /* ignore if already deleted */ }
        }

        var msg = await channel.SendMessageAsync(
            "# Voici ton annonce telle qu'elle sera pr\u00e9sent\u00e9e. V\u00e9rifies les informations pr\u00e9sentes avant de la publier.",
            embeds: embeds,
            components: components);

        ad.PreviewMessageId = msg.Id;
    }

    public static Embed[] BuildPreviewEmbeds(AdInProgress ad, IUser user)
    {
        var embeds = new List<Embed>();

        // 1. Main embed
        var authorLine = BuildAuthorLine(ad, user);
        var avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();

        var mainEmbed = new EmbedBuilder()
            .WithAuthor(authorLine, avatarUrl)
            .WithTitle(ad.Title ?? "[Titre manquant]")
            .WithDescription(Truncate(ad.Description ?? "[Aucune description]", 4000))
            .WithColor(PurpleColor);

        AddContractFields(mainEmbed, ad);
        AddLocationFields(mainEmbed, ad);

        embeds.Add(mainEmbed.Build());

        // 2. "Qui suis-je ?" embed
        embeds.Add(new EmbedBuilder()
            .WithTitle("Qui suis-je ?")
            .WithDescription(Truncate(ad.WhoAreYou ?? "[Aucune description]", 4000))
            .WithColor(PurpleColor)
            .Build());

        // 3. Role-specific embeds
        if (ad.Role == "recruiter")
        {
            embeds.Add(new EmbedBuilder()
                .WithTitle("Qualifications")
                .WithDescription(Truncate(ad.Qualifications ?? "[Donn\u00e9e manquante]", 4000))
                .WithColor(PurpleColor)
                .Build());

            embeds.Add(new EmbedBuilder()
                .WithTitle("Responsabilit\u00e9s")
                .WithDescription(Truncate(ad.Responsibilities ?? "[Donn\u00e9e manquante]", 4000))
                .WithColor(PurpleColor)
                .Build());
        }
        else if (ad.Role == "worker")
        {
            embeds.Add(new EmbedBuilder()
                .WithTitle("Mes comp\u00e9tences")
                .WithDescription(Truncate(ad.Skills ?? "[Donn\u00e9e manquante]", 4000))
                .WithColor(PurpleColor)
                .Build());
        }

        // 4. Contact embed
        var contactEmbed = new EmbedBuilder()
            .WithTitle("Contact")
            .WithColor(PurpleColor);

        if (ad.ContactMethod == "discord")
            contactEmbed.WithDescription($"Discord : {user.Username}");
        else
            contactEmbed.WithDescription(Truncate(ad.ContactInfo ?? "[Donn\u00e9e manquante]", 4000));

        if (!string.IsNullOrWhiteSpace(ad.OtherUrls))
            contactEmbed.AddField("Autre", Truncate(ad.OtherUrls, 1024), false);

        embeds.Add(contactEmbed.Build());

        return embeds.ToArray();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string BuildAuthorLine(AdInProgress ad, IUser user)
    {
        var username = user.Username;

        if (ad.Role == "recruiter")
        {
            var kind = ad.ContractType switch
            {
                "volunteering" => "un.e volontaire",
                "internship" => "un.e stagiaire",
                "freelance" => "un.e freelance",
                "work_study" => "un.e alternant.e",
                "fixed_term" => "pour un.e CDD",
                "open_ended" => "pour un.e CDI",
                _ => "[Type manquant]"
            };
            return $"{username} recrute {kind}";
        }

        if (ad.Role == "worker")
        {
            var kind = ad.ContractType switch
            {
                "volunteering" => "volontaire",
                "internship" => "candidat.e pour un stage",
                "freelance" => "un.e freelance",
                "work_study" => "candidat.e pour une alternance",
                "fixed_term" => "candidat.e pour un CDD",
                "open_ended" => "candidat.e pour un CDI",
                _ => "[Type manquant]"
            };
            return $"{username} est {kind}";
        }

        return $"{username}";
    }

    private static void AddContractFields(EmbedBuilder embed, AdInProgress ad)
    {
        switch (ad.ContractType)
        {
            case "internship":
                embed.AddField("Dur\u00e9e", ad.Duration ?? "[Donn\u00e9e manquante]", true);
                if (ad.HasCompensation == "yes")
                    embed.AddField("R\u00e9mun\u00e9ration", ad.Compensation ?? "[Donn\u00e9e manquante]", true);
                break;

            case "freelance":
            case "work_study":
            case "fixed_term":
                embed.AddField("Dur\u00e9e", ad.Duration ?? "[Donn\u00e9e manquante]", true);
                embed.AddField("R\u00e9mun\u00e9ration", ad.Compensation ?? "[Donn\u00e9e manquante]", true);
                break;

            case "open_ended":
                embed.AddField("R\u00e9mun\u00e9ration", ad.Compensation ?? "[Donn\u00e9e manquante]", true);
                break;
        }
    }

    private static void AddLocationFields(EmbedBuilder embed, AdInProgress ad)
    {
        if (ad.Role == "recruiter")
        {
            var location = ad.LocationType switch
            {
                "remote" => "\ud83c\udf0d Distanciel uniquement",
                "flex" => $"{ad.LocationDetail ?? "[Donn\u00e9e manquante]"} (\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible)",
                "on_site" => $"{ad.LocationDetail ?? "[Donn\u00e9e manquante]"} (\ud83c\udfe3 sur site)",
                _ => null
            };
            if (location is not null)
                embed.AddField("Emplacement", location, true);

            if (!string.IsNullOrWhiteSpace(ad.StudioName))
                embed.AddField("Entreprise", ad.StudioName, true);
        }
        else if (ad.Role == "worker")
        {
            var location = ad.WorkerLocationType switch
            {
                "remote" => "\ud83c\udf0d Distanciel uniquement",
                "anywhere" => $"{ad.WorkerLocationDetail ?? "[Donn\u00e9e manquante]"} (\ud83e\udd37\u200d\u2640\ufe0f T\u00e9l\u00e9travail possible)",
                "on_site" => $"{ad.WorkerLocationDetail ?? "[Donn\u00e9e manquante]"} (\ud83c\udfe3 sur site)",
                _ => null
            };
            if (location is not null)
                embed.AddField("Emplacement", location, true);
        }
    }

    public static string GetContractEmoji(string? contractType) => contractType switch
    {
        "volunteering" => "\ud83e\udd1d",
        "internship" => "\ud83e\ude82",
        "work_study" => "\ud83e\udd13",
        "freelance" => "\ud83e\uddd0",
        "fixed_term" => "\ud83d\ude0e",
        "open_ended" => "\ud83e\udd2f",
        _ => ""
    };

    public static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;
}

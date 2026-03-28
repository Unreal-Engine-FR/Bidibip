using System.Text.Json;
using Bidibip.Plugin.Sdk.Permissions;
using Discord.Interactions;

namespace Bidibip.Plugins.Update.Commands;

public sealed class UpdateModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string Repo = "Unreal-Engine-FR/Bidibip";
    private const string ApiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";

    [SlashCommand("update", "Vérifier et installer les mises à jour du bot")]
    [AllowedBotRole(BotRole.Moderator)]
    public async Task UpdateAsync()
    {
        var appDir = Path.GetDirectoryName(Environment.ProcessPath);
        var versionFile = appDir is not null ? Path.Combine(appDir, "version.txt") : null;

        var currentVersion = versionFile is not null && File.Exists(versionFile)
            ? (await File.ReadAllTextAsync(versionFile)).Trim()
            : null;

        // Check latest release from GitHub
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Bidibip-Bot");

        string latestTag;
        try
        {
            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            latestTag = doc.RootElement.GetProperty("tag_name").GetString()!;
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Impossible de vérifier les mises à jour : {ex.Message}",
                ephemeral: true);
            return;
        }

        if (currentVersion == latestTag)
        {
            await FollowupAsync($"Le bot est déjà à jour ({latestTag}).", ephemeral: true);
            return;
        }

        var status = currentVersion is not null
            ? $"Mise à jour de {currentVersion} vers {latestTag}, redémarrage..."
            : $"Mise à jour vers {latestTag}, redémarrage...";

        await FollowupAsync(status, ephemeral: true);

        // Exit — Docker restarts the container, entrypoint downloads the latest release.
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            Environment.Exit(0);
        });
    }
}

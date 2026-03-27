using System.Text;
using Bidibip.Plugin.Sdk;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Admin.Commands;

[DefaultMemberPermissions(GuildPermission.Administrator)]
public class PluginModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IPluginManager _pluginManager;

    public PluginModule(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    [SlashCommand("plugins", "List all available plugins")]
    public async Task ListPluginsAsync()
    {
        var plugins = _pluginManager.GetAllPlugins();

        var embed = new EmbedBuilder()
            .WithTitle("Plugins")
            .WithColor(Color.Blue);

        if (plugins.Count == 0)
        {
            embed.WithDescription("No plugins found.");
        }
        else
        {
            var sb = new StringBuilder();
            foreach (var p in plugins.OrderBy(p => p.Name))
            {
                var status = p.IsLoaded ? "Enabled" : "Disabled";
                sb.AppendLine($"`{p.Name}` — {status}");
            }
            embed.WithDescription(sb.ToString());
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    [SlashCommand("plugin-enable", "Enable a disabled plugin")]
    public async Task EnablePluginAsync(
        [Summary("name", "Name of the plugin to enable")] string pluginName)
    {
        var success = await _pluginManager.EnablePluginAsync(pluginName);
        if (success)
            await FollowupAsync($"Plugin **{pluginName}** has been enabled.", ephemeral: true);
        else
            await FollowupAsync($"Plugin **{pluginName}** was not found or is already enabled.", ephemeral: true);
    }

    [SlashCommand("plugin-disable", "Disable a plugin")]
    public async Task DisablePluginAsync(
        [Summary("name", "Name of the plugin to disable")] string pluginName)
    {
        if (pluginName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            await FollowupAsync("The Admin plugin cannot be disabled.", ephemeral: true);
            return;
        }

        var success = await _pluginManager.DisablePluginAsync(pluginName);
        if (success)
            await FollowupAsync($"Plugin **{pluginName}** has been disabled.", ephemeral: true);
        else
            await FollowupAsync($"Plugin **{pluginName}** was not found or is already disabled.", ephemeral: true);
    }
}

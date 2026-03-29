// ──────────────────────────────────────────────────────────────────────────────
// Program.cs — Application entry point
//
// This is the top-level file that bootstraps the bot. It:
//   1. Sets up Serilog logging (console + rolling file + Discord channel)
//   2. Loads configuration from Saved/config.json and .env
//   3. Registers all DI services (Discord client, InteractionService, PluginManager)
//   4. Starts the .NET Generic Host which runs BotService and PluginManager
//
// The bot's lifetime is managed by the Generic Host: BotService connects to
// Discord, and PluginManager loads plugins and watches for hot-reload changes.
// ──────────────────────────────────────────────────────────────────────────────

using Bidibip.Logging;
using Bidibip.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

// ── Log directory setup ─────────────────────────────────────────────────────
// Keep at most 10 log files (9 existing + the new one). Oldest are deleted
// on startup to avoid filling the disk over time.
var logsPath = Path.Combine("Saved", "logs");
Directory.CreateDirectory(logsPath);

var oldLogs = Directory.GetFiles(logsPath, "bidibip-*.log")
    .OrderByDescending(File.GetCreationTimeUtc)
    .Skip(9) // keep 9 existing + the new one = 10
    .ToList();
foreach (var old in oldLogs)
{
    try { File.Delete(old); }
    catch { /* best effort — file may be locked by another process */ }
}

var logFilePath = Path.Combine(logsPath, $"bidibip-{DateTime.Now:yyyyMMdd-HHmmss}.log");
var discordLogService = new DiscordLogService(logsPath);

// ── Serilog configuration ───────────────────────────────────────────────────
// Three sinks run in parallel:
//   - Console: immediate developer feedback
//   - File: persistent log for post-mortem debugging
//   - DiscordSink: forwards log entries to DiscordLogService, which batches
//     them and sends them to the configured Discord log channel
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(logFilePath,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Sink(new DiscordSink(discordLogService), LogEventLevel.Information)
    .CreateLogger();

try
{
    // ── Configuration bootstrap ─────────────────────────────────────────────
    // On first launch, seed a default config.json so the user knows what
    // values to fill in. All IDs default to "0" which means "not configured".
    var configPath = Path.Combine("Saved", "config.json");
    Directory.CreateDirectory("Saved");

    if (!File.Exists(configPath))
    {
        var defaultConfig = """
            {
              "Bot": {
                "GuildId": "0",
                "Roles": {
                  "Administrator": "0",
                  "Moderator": "0",
                  "Helper": "0",
                  "Member": "0",
                  "Mute": "0"
                },
                "Channels": {
                  "LogChannel": "0",
                  "StaffChannel": "0"
                }
              },
              "Plugins": {
                "Path": "plugins"
              }
            }
            """;
        File.WriteAllText(configPath, defaultConfig);
        Log.Logger.Warning("Default config created at {Path} — please fill in the values", configPath);
    }

    Log.Logger.Information("Load config from {Path}", configPath);

    // ── Host setup ────────────────────────────────────────────────────────
    // The Generic Host manages the application's lifetime and DI container.
    // Two hosted services run concurrently:
    //   - BotService: connects to Discord and handles interactions
    //   - PluginManager: loads/unloads/hot-reloads plugin DLLs
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            // config.json is the main configuration file (roles, channels, guild ID).
            // reloadOnChange: true allows the bot to pick up config edits without restart.
            config.AddJsonFile(configPath,
                optional: false,
                reloadOnChange: true);

            // Load .env file manually: parse KEY=VALUE lines and inject them as
            // environment variables. This is how the Discord token is provided
            // (DISCORD__TOKEN=xxx). The double underscore maps to "Discord:Token"
            // in .NET's configuration system.
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                        continue;
                    var sep = trimmed.IndexOf('=');
                    if (sep < 0) continue;
                    var key = trimmed[..sep].Trim();
                    var value = trimmed[(sep + 1)..].Trim();
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            // Re-add env vars so they take precedence over config.json values.
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            // Gateway intents control which events Discord sends us.
            // MessageContent is a privileged intent required to read message text.
            // GuildMembers is a privileged intent required for user join/leave events.
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds
                    | GatewayIntents.GuildMessages
                    | GatewayIntents.MessageContent
                    | GatewayIntents.GuildMembers
                    | GatewayIntents.GuildBans,
                // Cache size for the History plugin to retrieve deleted/edited messages
                MessageCacheSize = 10000,
                LogLevel = LogSeverity.Info,
                // Disable snowflake-based date parsing for interactions to avoid
                // clock-skew issues that can cause "interaction expired" errors
                UseInteractionSnowflakeDate = false
            };

            services.AddSingleton(config);
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
            // PluginManager is both a singleton (for DI) and a hosted service (for lifecycle)
            services.AddSingleton<PluginManager>();
            services.AddSingleton(discordLogService);
            services.AddHostedService<BotService>();
            services.AddHostedService(sp => sp.GetRequiredService<PluginManager>());
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bot terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

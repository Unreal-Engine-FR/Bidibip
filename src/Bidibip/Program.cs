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

// Prepare log directory and clean old execution logs (keep up to 10)
var logsPath = Path.Combine("Saved", "logs");
Directory.CreateDirectory(logsPath);

var oldLogs = Directory.GetFiles(logsPath, "bidibip-*.log")
    .OrderByDescending(File.GetCreationTimeUtc)
    .Skip(9) // keep 9 existing + the new one = 10
    .ToList();
foreach (var old in oldLogs)
{
    try { File.Delete(old); }
    catch { /* best effort */ }
}

var logFilePath = Path.Combine(logsPath, $"bidibip-{DateTime.Now:yyyyMMdd-HHmmss}.log");
var discordLogService = new DiscordLogService(logsPath);

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
    var configPath = Path.Combine("Saved", "config.json");
    Directory.CreateDirectory("Saved");

    // Seed a default config file if none exists
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

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile(configPath,
                optional: false,
                reloadOnChange: true);
            // Load .env file from the working directory if it exists.
            // Values set here become environment variables and override appsettings.
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
            // Re-add env vars so they override appsettings values (e.g. Token)
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMembers | GatewayIntents.GuildBans,
                MessageCacheSize = 10000,
                LogLevel = LogSeverity.Info,
                UseInteractionSnowflakeDate = false
            };

            services.AddSingleton(config);
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
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

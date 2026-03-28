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
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    Log.Logger.Information("Load config from {Path}", configPath);
    
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            // Load appsettings.json from the binary directory so it works
            // regardless of which directory the bot is launched from.
            config.AddJsonFile(configPath,
                optional: true,
                reloadOnChange: true);
            // Re-add env vars so they override appsettings values (e.g. Token)
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
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

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Bidibip.Plugins;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SdkCommandInfo = Bidibip.Plugin.Sdk.CommandInfo;

namespace Bidibip.Services;

public sealed class PluginManager : IHostedService, ICommandRegistry, IPluginManager, IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BotConfig _botConfig;

    private readonly ConcurrentDictionary<string, LoadedPlugin> _plugins = new();
    private readonly HashSet<string> _disabledPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    internal IServiceProvider ServiceProvider => _globalServiceProvider;
    private readonly IServiceProvider _globalServiceProvider;
    private readonly PermissionData _permissionData = new();
    private volatile bool _botReady;
    private FileSystemWatcher? _watcher;
    private string _pluginsPath = null!;

    public PluginManager(
        DiscordSocketClient client,
        InteractionService interactions,
        IConfiguration configuration,
        ILogger<PluginManager> logger,
        ILoggerFactory loggerFactory)
    {
        _client = client;
        _interactions = interactions;
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;

        _botConfig = new BotConfig();
        configuration.GetSection("Bot").Bind(_botConfig);

        // Shared service provider for all plugin modules (used for DI in slash command handlers)
        _globalServiceProvider = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_botConfig)
            .AddSingleton<ICommandRegistry>(this)
            .AddSingleton<IPluginManager>(this)
            .AddSingleton(_loggerFactory)
            .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
            .BuildServiceProvider();
    }

    public IReadOnlyList<SdkCommandInfo> GetCommands()
    {
        return _interactions.Modules
            .SelectMany(m => m.SlashCommands)
            .Select(cmd =>
            {
                // Find which plugin owns this module
                string? pluginName = null;
                var declaringAssembly = cmd.Module.GetType().Assembly;
                foreach (var plugin in _plugins.Values)
                {
                    if (plugin.RegisteredModules.Any(rm => rm == cmd.Module))
                    {
                        pluginName = plugin.Instance.Name;
                        break;
                    }
                }

                var roleAttr = cmd.Preconditions
                    .OfType<AllowedBotRoleAttribute>()
                    .FirstOrDefault();

                return new SdkCommandInfo
                {
                    Name = cmd.Name,
                    Description = cmd.Description,
                    PluginName = pluginName,
                    MinimumRole = roleAttr?.MinimumRole ?? BotRole.Administrator
                };
            })
            .ToList();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _pluginsPath = _configuration["Plugins:Path"]
            ?? "plugins";

        if (!Path.IsPathRooted(_pluginsPath))
            _pluginsPath = Path.Combine(Directory.GetCurrentDirectory(), _pluginsPath);

        _logger.LogInformation("Resolved plugins path: {Path}", _pluginsPath);
        Directory.CreateDirectory(_pluginsPath);

        LoadDisabledList();
        WireDiscordEvents();

        var dlls = Directory.GetFiles(_pluginsPath, "*.dll");
        _logger.LogInformation("Found {Count} DLL(s) in plugins directory", dlls.Length);

        foreach (var dll in dlls)
        {
            var fileName = Path.GetFileName(dll);
            if (_disabledPlugins.Contains(fileName))
            {
                _logger.LogInformation("Skipping disabled plugin: {File}", fileName);
                continue;
            }

            _logger.LogInformation("Attempting to load: {File}", fileName);
            await LoadPluginAsync(dll);
        }

        _watcher = new FileSystemWatcher(_pluginsPath, "*.dll")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnPluginFileCreated;
        _watcher.Deleted += OnPluginFileDeleted;
        _watcher.Changed += OnPluginFileChanged;

        _logger.LogInformation("Plugin manager started. Watching {Path}", _pluginsPath);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;

        foreach (var key in _plugins.Keys.ToList())
        {
            if (_plugins.TryRemove(key, out var loaded))
            {
                loaded.EventBus.Clear();
                await loaded.Instance.DisposeAsync();
                loaded.LoadContext.Unload();
            }
        }

        // Clean up shadow copies
        var shadowDir = Path.Combine(_pluginsPath, ".shadow");
        if (Directory.Exists(shadowDir))
        {
            try { Directory.Delete(shadowDir, recursive: true); }
            catch { /* best effort */ }
        }

        _logger.LogInformation("Plugin manager stopped");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _loadLock.Dispose();
    }

    private async Task LoadPluginAsync(string dllPath)
    {
        await _loadLock.WaitAsync();
        try
        {
            var fileName = Path.GetFileName(dllPath);
            if (_plugins.ContainsKey(fileName))
            {
                _logger.LogWarning("Plugin {File} is already loaded", fileName);
                return;
            }

            // Copy DLL to a temp path so the original file is never locked
            // This allows rebuilding plugins while the bot is running
            var shadowDir = Path.Combine(_pluginsPath, ".shadow");
            Directory.CreateDirectory(shadowDir);
            var shadowPath = Path.Combine(shadowDir, $"{Path.GetFileNameWithoutExtension(dllPath)}_{Guid.NewGuid():N}.dll");
            File.Copy(dllPath, shadowPath, overwrite: true);

            // Also copy .deps.json if it exists (needed for AssemblyDependencyResolver)
            var depsFile = Path.ChangeExtension(dllPath, ".deps.json");
            if (File.Exists(depsFile))
            {
                var shadowDeps = Path.ChangeExtension(shadowPath, ".deps.json");
                File.Copy(depsFile, shadowDeps, overwrite: true);
            }

            var loadContext = new PluginLoadContext(shadowPath);
            var assembly = loadContext.LoadFromAssemblyPath(shadowPath);

            // Check SDK version compatibility
            var pluginSdkRef = assembly.GetReferencedAssemblies()
                .FirstOrDefault(a => a.Name == "Bidibip.Plugin.Sdk");
            var hostSdkVersion = typeof(SdkInfo).Assembly.GetName().Version;

            if (pluginSdkRef?.Version != null && hostSdkVersion != null)
            {
                // Compare major.minor — patch differences are OK
                if (pluginSdkRef.Version.Major != hostSdkVersion.Major ||
                    pluginSdkRef.Version.Minor != hostSdkVersion.Minor)
                {
                    _logger.LogError(
                        "Plugin {File} was built against SDK {PluginSdk} but host has SDK {HostSdk}. Skipping.",
                        fileName,
                        pluginSdkRef.Version.ToString(3),
                        hostSdkVersion.ToString(3));
                    loadContext.Unload();
                    return;
                }
            }

            var pluginVersion = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    t.GetCustomAttribute<BidibipPluginAttribute>() != null &&
                    typeof(IBidibipPlugin).IsAssignableFrom(t));

            if (pluginType is null)
            {
                _logger.LogWarning("No plugin class found in {File}", fileName);
                loadContext.Unload();
                return;
            }

            var instance = (IBidibipPlugin)Activator.CreateInstance(pluginType)!;
            var eventBus = new PluginEventBus();

            var pluginConfigDir = Path.Combine(
                _pluginsPath, Path.GetFileNameWithoutExtension(dllPath));
            var pluginConfigPath = Path.Combine(pluginConfigDir, "config.json");

            var configBuilder = new ConfigurationBuilder();
            if (File.Exists(pluginConfigPath))
                configBuilder.AddJsonFile(pluginConfigPath, optional: true, reloadOnChange: true);

            var dataPath = Path.Combine("Saved", "data", instance.Name);
            Directory.CreateDirectory(dataPath);

            var context = new PluginContext
            {
                Logger = _loggerFactory.CreateLogger($"Plugin.{instance.Name}"),
                Configuration = configBuilder.Build(),
                Events = eventBus,
                BotConfig = _botConfig,
                Commands = this,
                DataPath = dataPath
            };

            await instance.InitializeAsync(context);

            // Register slash command modules from the plugin assembly
            IEnumerable<Discord.Interactions.ModuleInfo> modules = [];
            try
            {
                modules = await _interactions.AddModulesAsync(assembly, _globalServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register commands for plugin {Name}. The plugin will load without commands.", instance.Name);
            }

            var loaded = new LoadedPlugin
            {
                FilePath = dllPath,
                LoadContext = loadContext,
                Instance = instance,
                WeakRef = new WeakReference(loadContext),
                EventBus = eventBus
            };
            loaded.RegisteredModules.AddRange(modules);

            _plugins[fileName] = loaded;

            await RegisterCommandsAsync();

            _logger.LogInformation("Loaded plugin: {Name} v{Version} ({File}) [SDK {SdkVersion}]",
                instance.Name, pluginVersion, fileName, pluginSdkRef?.Version?.ToString(3) ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {Path}", dllPath);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task UnloadPluginAsync(string dllPath)
    {
        await _loadLock.WaitAsync();
        try
        {
            var fileName = Path.GetFileName(dllPath);
            if (!_plugins.TryRemove(fileName, out var loaded))
                return;

            loaded.EventBus.Clear();

            foreach (var module in loaded.RegisteredModules)
            {
                await _interactions.RemoveModuleAsync(module);
            }

            await loaded.Instance.DisposeAsync();
            loaded.LoadContext.Unload();

            await RegisterCommandsAsync();

            _logger.LogInformation("Unloaded plugin: {Name}", loaded.Instance.Name);

            // Check if the assembly was properly collected
            for (var i = 0; i < 8 && loaded.WeakRef.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (loaded.WeakRef.IsAlive)
                _logger.LogWarning("Plugin assembly was not fully unloaded (leaked references)");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task ReloadPluginAsync(string dllPath)
    {
        _logger.LogInformation("Reloading plugin: {Path}", Path.GetFileName(dllPath));
        await UnloadPluginAsync(dllPath);
        // Small delay to ensure the file is fully written
        await Task.Delay(500);
        await LoadPluginAsync(dllPath);
    }

    public void MarkBotReady() => _botReady = true;

    public async Task RegisterCommandsAsync()
    {
        if (!_botReady)
            return;

        // Fetch actual role permissions from Discord (like the Rust implementation's fetch_roles)
        if (!_permissionData.IsLoaded)
            await _permissionData.FetchRolesAsync(_client, _botConfig, _logger);

        // Build command properties with DefaultMemberPermissions derived from AllowedBotRole
        var commandProperties = BuildCommandProperties();

        // Build a fingerprint of what we want registered: "name:perms"
        // Use raw numeric permission values to ensure consistent comparison
        var localFingerprint = commandProperties
            .Select(c =>
            {
                var name = c.Name.IsSpecified ? c.Name.Value : "";
                var perms = c.DefaultMemberPermissions.IsSpecified
                    ? ((ulong)c.DefaultMemberPermissions.Value).ToString()
                    : "none";
                return $"{name}:{perms}";
            })
            .OrderBy(c => c)
            .ToList();

        // Fetch what Discord currently has registered
        var guildId = ResolveGuildId();
        IReadOnlyCollection<Discord.IApplicationCommand> remoteCommands;

        if (guildId != 0)
        {
            var guild = _client.GetGuild(guildId);
            remoteCommands = guild != null
                ? await guild.GetApplicationCommandsAsync()
                : Array.Empty<Discord.IApplicationCommand>();
        }
        else
        {
            remoteCommands = await _client.GetGlobalApplicationCommandsAsync();
        }

        var remoteFingerprint = remoteCommands
            .Select(c =>
            {
                var perms = c.DefaultMemberPermissions.RawValue != 0
                    ? c.DefaultMemberPermissions.RawValue.ToString()
                    : "none";
                return $"{c.Name}:{perms}";
            })
            .OrderBy(c => c)
            .ToList();

        if (localFingerprint.SequenceEqual(remoteFingerprint))
        {
            _logger.LogInformation("Commands are up to date ({Count} command(s)), skipping registration", commandProperties.Count);
            return;
        }

        _logger.LogInformation("Commands changed (local: [{Local}], remote: [{Remote}]), registering...",
            string.Join(", ", localFingerprint.Select(c => c.Split(':')[0])),
            string.Join(", ", remoteFingerprint.Select(c => c.Split(':')[0])));

        if (guildId != 0)
        {
            var guild = _client.GetGuild(guildId);
            if (guild != null)
                await guild.BulkOverwriteApplicationCommandAsync(commandProperties.ToArray());
            _logger.LogInformation("Registered {Count} command(s) to guild {GuildId}", commandProperties.Count, guildId);
        }
        else
        {
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(commandProperties.ToArray());
            _logger.LogInformation("Registered {Count} command(s) globally (may take up to 1 hour)", commandProperties.Count);
        }
    }

    /// <summary>
    /// Builds ApplicationCommandProperties for all registered slash/user/message commands,
    /// automatically deriving DefaultMemberPermissions from AllowedBotRole attributes.
    /// This replaces InteractionService.RegisterCommandsToGuildAsync so we control the
    /// Discord-side visibility of commands based on our permission system.
    /// </summary>
    private List<ApplicationCommandProperties> BuildCommandProperties()
    {
        var result = new List<ApplicationCommandProperties>();

        foreach (var module in _interactions.Modules)
        {
            // Slash commands
            foreach (var cmd in module.SlashCommands)
            {
                var builder = new SlashCommandBuilder()
                    .WithName(cmd.Name)
                    .WithDescription(cmd.Description);

                // Add parameters
                foreach (var param in cmd.Parameters)
                {
                    builder.AddOption(BuildSlashOption(param));
                }

                // Derive DefaultMemberPermissions from AllowedBotRole
                var minRole = ExtractMinimumRole(cmd.Preconditions);
                var discordPerm = ResolveDiscordPermission(minRole);
                if (discordPerm.HasValue)
                    builder.WithDefaultMemberPermissions(discordPerm.Value);

                result.Add(builder.Build());
            }

            // User context menu commands
            foreach (var cmd in module.ContextCommands)
            {
                if (cmd.CommandType == ApplicationCommandType.User)
                {
                    var builder = new UserCommandBuilder()
                        .WithName(cmd.Name);

                    var minRole = ExtractMinimumRole(cmd.Preconditions);
                    var discordPerm = ResolveDiscordPermission(minRole);
                    if (discordPerm.HasValue)
                        builder.WithDefaultMemberPermissions(discordPerm.Value);

                    result.Add(builder.Build());
                }
                else if (cmd.CommandType == ApplicationCommandType.Message)
                {
                    var builder = new MessageCommandBuilder()
                        .WithName(cmd.Name);

                    var minRole = ExtractMinimumRole(cmd.Preconditions);
                    var discordPerm = ResolveDiscordPermission(minRole);
                    if (discordPerm.HasValue)
                        builder.WithDefaultMemberPermissions(discordPerm.Value);

                    result.Add(builder.Build());
                }
            }
        }

        return result;
    }

    private static SlashCommandOptionBuilder BuildSlashOption(Discord.Interactions.SlashCommandParameterInfo param)
    {
        var option = new SlashCommandOptionBuilder()
            .WithName(param.Name)
            .WithDescription(param.Description ?? param.Name)
            .WithRequired(param.IsRequired)
            .WithType(param.DiscordOptionType ?? ApplicationCommandOptionType.String);

        foreach (var choice in param.Choices)
        {
            option.AddChoice(choice.Name, choice.Value?.ToString() ?? choice.Name);
        }

        return option;
    }

    /// <summary>
    /// Resolves the Discord permission set for command visibility based on the
    /// fetched role permissions. Uses the intersection of all tiers >= the target role,
    /// reproducing the Rust implementation's at_least_X() approach.
    /// Returns null for Member/Everyone (visible to all).
    /// </summary>
    private GuildPermission? ResolveDiscordPermission(BotRole role)
    {
        if (!_permissionData.IsLoaded)
        {
            // Fallback if permissions haven't been fetched yet
            return role switch
            {
                BotRole.Administrator => GuildPermission.Administrator,
                BotRole.Moderator => GuildPermission.ModerateMembers,
                _ => null
            };
        }

        return _permissionData.AtLeast(role);
    }

    private static BotRole ExtractMinimumRole(IReadOnlyCollection<PreconditionAttribute> preconditions)
    {
        var attr = preconditions.OfType<AllowedBotRoleAttribute>().FirstOrDefault();
        return attr?.MinimumRole ?? BotRole.Administrator;
    }

    private ulong ResolveGuildId()
    {
        if (_botConfig.GuildId != 0)
            return _botConfig.GuildId;

        var devGuildIdStr = _configuration["Discord:DevGuildId"];
        return ulong.TryParse(devGuildIdStr, out var parsed) ? parsed : 0;
    }

    private void WireDiscordEvents()
    {
        _client.InteractionCreated += async interaction =>
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchInteractionCreated(interaction);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} interaction handler",
                        plugin.Instance.Name);
                }
            }
        };

        _client.MessageReceived += async msg =>
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchMessageReceived(msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} message handler",
                        plugin.Instance.Name);
                }
            }
        };

        _client.ReactionAdded += async (cacheable, channel, reaction) =>
        {
            var msgChannel = await channel.GetOrDownloadAsync();
            if (msgChannel is not IMessageChannel mc)
                return;

            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchReactionAdded(reaction, mc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} reaction handler",
                        plugin.Instance.Name);
                }
            }
        };

        _client.UserJoined += user =>
        {
            _ = Task.Run(async () =>
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        await plugin.EventBus.DispatchUserJoined(user);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in plugin {Name} user joined handler",
                            plugin.Instance.Name);
                    }
                }
            });
            return Task.CompletedTask;
        };

        _client.UserLeft += (guild, user) =>
        {
            _ = Task.Run(async () =>
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        await plugin.EventBus.DispatchUserLeft(guild, user);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in plugin {Name} user left handler",
                            plugin.Instance.Name);
                    }
                }
            });
            return Task.CompletedTask;
        };

        _client.AuditLogCreated += async (socketEntry, guild) =>
        {
            // Skip actions performed by the bot itself
            if (socketEntry.User?.Id == _client.CurrentUser?.Id)
                return;

            Plugin.Sdk.AuditLogActionType actionType;
            ulong? targetId = null;
            IUser? targetUser = null;
            DateTimeOffset? timeoutUntil = null;

            switch (socketEntry.Action)
            {
                case ActionType.Kick:
                    actionType = Plugin.Sdk.AuditLogActionType.Kick;
                    if (socketEntry.Data is Discord.WebSocket.SocketKickAuditLogData kickData)
                    {
                        targetId = kickData.Target.Id;
                        try { targetUser = await kickData.Target.GetOrDownloadAsync(); } catch { }
                    }
                    break;
                case ActionType.Ban:
                    actionType = Plugin.Sdk.AuditLogActionType.Ban;
                    if (socketEntry.Data is Discord.WebSocket.SocketBanAuditLogData banData)
                    {
                        targetId = banData.Target.Id;
                        try { targetUser = await banData.Target.GetOrDownloadAsync(); } catch { }
                    }
                    break;
                case ActionType.MemberUpdated:
                    actionType = Plugin.Sdk.AuditLogActionType.MemberUpdate;
                    if (socketEntry.Data is Discord.WebSocket.SocketMemberUpdateAuditLogData memberData)
                    {
                        targetId = memberData.Target.Id;
                        try { targetUser = await memberData.Target.GetOrDownloadAsync(); } catch { }
                        if (targetId.HasValue)
                        {
                            var member = guild.GetUser(targetId.Value);
                            if (member?.TimedOutUntil is not null && member.TimedOutUntil > DateTimeOffset.UtcNow)
                                timeoutUntil = member.TimedOutUntil;
                        }
                    }
                    break;
                default:
                    return;
            }

            var entry = new Plugin.Sdk.AuditLogEntry
            {
                UserId = socketEntry.User?.Id ?? 0,
                TargetId = targetId,
                ActionType = actionType,
                Reason = socketEntry.Reason,
                Guild = guild,
                TargetUser = targetUser,
                TimeoutUntil = timeoutUntil
            };

            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchAuditLogCreated(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} audit log handler",
                        plugin.Instance.Name);
                }
            }
        };

        _client.ThreadCreated += thread =>
        {
            _ = Task.Run(async () =>
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        await plugin.EventBus.DispatchThreadCreated(thread);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in plugin {Name} thread created handler",
                            plugin.Instance.Name);
                    }
                }
            });
            return Task.CompletedTask;
        };

        _client.MessageDeleted += async (message, channel) =>
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchMessageDeleted(message, channel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} message deleted handler",
                        plugin.Instance.Name);
                }
            }
        };

        _client.MessageUpdated += async (before, after, channel) =>
        {
            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    await plugin.EventBus.DispatchMessageUpdated(before, after, channel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in plugin {Name} message updated handler",
                        plugin.Instance.Name);
                }
            }
        };
    }

    private async void OnPluginFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_disabledPlugins.Contains(Path.GetFileName(e.FullPath)))
            return;
        // Delay to avoid loading while the file is still being copied
        await Task.Delay(500);
        await LoadPluginAsync(e.FullPath);
    }

    private async void OnPluginFileDeleted(object sender, FileSystemEventArgs e)
    {
        await UnloadPluginAsync(e.FullPath);
    }

    private async void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disabledPlugins.Contains(Path.GetFileName(e.FullPath)))
            return;
        await ReloadPluginAsync(e.FullPath);
    }

    // --- IPluginManager implementation ---

    public IReadOnlyList<Bidibip.Plugin.Sdk.PluginInfo> GetAllPlugins()
    {
        var result = new List<Bidibip.Plugin.Sdk.PluginInfo>();
        var dlls = Directory.GetFiles(_pluginsPath, "*.dll");

        foreach (var dll in dlls)
        {
            var fileName = Path.GetFileName(dll);
            if (_plugins.TryGetValue(fileName, out var loaded))
            {
                result.Add(new Bidibip.Plugin.Sdk.PluginInfo
                {
                    FileName = fileName,
                    Name = loaded.Instance.Name,
                    IsLoaded = true
                });
            }
            else
            {
                result.Add(new Bidibip.Plugin.Sdk.PluginInfo
                {
                    FileName = fileName,
                    Name = DerivePluginName(fileName),
                    IsLoaded = false
                });
            }
        }

        return result;
    }

    public async Task<bool> EnablePluginAsync(string pluginName)
    {
        var fileName = FindPluginFileName(pluginName);
        if (fileName is null || !_disabledPlugins.Contains(fileName))
            return false;

        _disabledPlugins.Remove(fileName);
        SaveDisabledList();

        var dllPath = Path.Combine(_pluginsPath, fileName);
        if (File.Exists(dllPath))
            await LoadPluginAsync(dllPath);

        return true;
    }

    public async Task<bool> DisablePluginAsync(string pluginName)
    {
        var entry = _plugins.Values.FirstOrDefault(p =>
            p.Instance.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return false;

        var fileName = Path.GetFileName(entry.FilePath);
        await UnloadPluginAsync(entry.FilePath);

        _disabledPlugins.Add(fileName);
        SaveDisabledList();

        return true;
    }

    private string? FindPluginFileName(string pluginName)
    {
        // Check loaded plugins
        var loaded = _plugins.Values.FirstOrDefault(p =>
            p.Instance.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
        if (loaded is not null)
            return Path.GetFileName(loaded.FilePath);

        // Check all DLLs by derived name
        foreach (var dll in Directory.GetFiles(_pluginsPath, "*.dll"))
        {
            var fn = Path.GetFileName(dll);
            if (DerivePluginName(fn).Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                return fn;
        }

        return null;
    }

    private static string DerivePluginName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        const string prefix = "Bidibip.Plugins.";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            name = name[prefix.Length..];
        return name;
    }

    private void LoadDisabledList()
    {
        var path = Path.Combine(_pluginsPath, "disabled.json");
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is not null)
            {
                foreach (var item in list)
                    _disabledPlugins.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disabled plugins list");
        }
    }

    private void SaveDisabledList()
    {
        var path = Path.Combine(_pluginsPath, "disabled.json");
        try
        {
            var json = JsonSerializer.Serialize(_disabledPlugins.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save disabled plugins list");
        }
    }
}

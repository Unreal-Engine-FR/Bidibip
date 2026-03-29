# Architecture

## Overview

Bidibip is made of three layers:

```
+-------------------+     +-------------------+     +-------------------+
|                   |     |                   |     |   Plugin A (.dll) |
|   Bidibip (host)  |---->|   Plugin SDK      |<----|   Plugin B (.dll) |
|                   |     |                   |     |   Plugin C (.dll) |
+-------------------+     +-------------------+     +-------------------+
```

- **Bidibip (host)** — The main application. Connects to Discord, loads plugins, routes events, registers slash commands.
- **Plugin SDK** — A shared library that defines the interfaces and helpers available to plugins.
- **Plugins** — Independent DLLs loaded at runtime. Each plugin is a separate C# project that references the SDK.

## Startup sequence

1. **Logging setup** — Configures three Serilog sinks: console, rolling file, and Discord channel
2. **Load configuration** — Reads `.env` (token) and `Saved/config.json` (roles, channels)
3. **Connect to Discord** — The bot logs in via WebSocket with the required gateway intents (guilds, messages, message content, members, bans)
4. **Load plugins** — The `PluginManager` scans the `plugins/` folder and loads every DLL it finds
5. **Register commands** — All slash commands from all plugins are registered with Discord
6. **Ready** — The `BotReady` event is dispatched to every plugin

### Entry point (Program.cs)

The application uses .NET's Generic Host which manages the DI container and hosted service lifetime. Two hosted services run concurrently:

| Service | Role |
|---|---|
| `BotService` | Connects to Discord, handles interactions, applies permission checks |
| `PluginManager` | Loads/unloads plugins, watches for hot-reload, syncs commands with Discord |

Configuration is loaded from two sources, merged in this order (later wins):

1. `Saved/config.json` — Main config (guild ID, role IDs, channel IDs)
2. `.env` file — Parsed into environment variables (e.g., `DISCORD__TOKEN=xxx`)
3. Environment variables — The `__` separator maps to `:` in .NET config (so `DISCORD__TOKEN` becomes `Discord:Token`)

## Plugin loading

The `PluginManager` uses .NET's `AssemblyLoadContext` to isolate each plugin:

1. Scans the `plugins/` folder for `.dll` files
2. Copies each DLL to a `.shadow/` folder (shadow copying, see below)
3. Loads the DLL in a dedicated `AssemblyLoadContext`
4. Looks for a class decorated with `[BidibipPlugin]` that implements `IBidibipPlugin`
5. Verifies SDK version compatibility (major.minor must match)
6. Calls `InitializeAsync()` with a `PluginContext` containing the logger, config, event bus, and more
7. Registers any slash command modules found in the assembly

### Shadow copying

DLLs are copied to a `.shadow/` subfolder before being loaded into memory. This prevents file locks on the original files:

```
plugins/
  Bidibip.Plugins.Help.dll          <-- original (never locked)
  .shadow/
    Bidibip.Plugins.Help_abc123.dll <-- shadow copy (loaded into memory)
```

This means you can rebuild a plugin while the bot is running. The `FileSystemWatcher` detects the change to the original file and triggers a hot-reload.

### Assembly isolation (PluginLoadContext)

Each plugin runs in its own `AssemblyLoadContext` (ALC). This provides:

- **Type isolation** — Two plugins can bundle different versions of the same library without conflict
- **Unloadability** — The ALC is `collectible`, so the runtime can garbage-collect the entire assembly when the plugin is unloaded
- **Type unification** — Certain assemblies are shared with the host (loaded from the default context) so that interface casts work:

| Shared assemblies | Why |
|---|---|
| `Bidibip.Plugin.Sdk` | `IBidibipPlugin`, `BotConfig`, `IEventBus`, etc. must be the same type in host and plugin |
| `Discord.Net.*` | `SocketInteractionContext`, `IMessage`, etc. must unify for event handlers and command modules |
| `Microsoft.Extensions.*` | `ILogger`, `IConfiguration` must be shared for DI to work |

If a plugin bundled its own copy of these assemblies, `IBidibipPlugin` in the plugin would be a **different type** than in the host, causing "does not implement IBidibipPlugin" errors.

### SDK version check

When loading a plugin, the host compares the SDK assembly version (major.minor) between the host and the plugin. If they don't match, the plugin is skipped with an error. This prevents subtle bugs when the SDK interface evolves.

### Hot-reload

A `FileSystemWatcher` monitors the `plugins/` folder. When a `.dll` file changes:

1. The old plugin is disposed (`DisposeAsync()`)
2. Its event handlers are cleared
3. Its command modules are removed from the InteractionService
4. The old `AssemblyLoadContext` is unloaded (garbage collected)
5. The new DLL is loaded through the same process
6. Commands are re-registered with Discord if needed

The bot verifies unloading succeeded by checking a `WeakReference` to the old ALC. If it's still alive after several GC cycles, a warning is logged — this means the plugin leaked references (e.g., a static field or undisposed event handler).

### Disabled plugins

Plugins can be disabled at runtime via the Admin plugin's `/plugin` command. Disabled plugins are recorded in `plugins/disabled.json` (a simple JSON array of DLL file names) and skipped during loading.

## Event routing

The bot listens to all Discord events and distributes them to plugins via per-plugin event buses:

```
Discord WebSocket
      |
      v
  BotService (permission gates, auto-defer)
      |
      v
  PluginManager.WireDiscordEvents()
      |
      +---> Plugin A EventBus: OnMessageReceived handlers
      +---> Plugin B EventBus: OnMessageReceived handlers
      +---> Plugin A EventBus: OnUserJoined handlers
      +---> Plugin C EventBus: OnInteractionCreated handlers
      ...
```

Each plugin gets its own `PluginEventBus` instance. The plugin registers handlers in `InitializeAsync()`, and the host dispatches events to all registered handlers. Exceptions in one plugin's handler don't affect other plugins.

### Available events

| Event | Triggered when | Common uses |
|---|---|---|
| `OnMessageReceived` | A message is sent | Anti-spam, text commands, logging |
| `OnMessageDeleted` | A message is deleted | History plugin |
| `OnMessageUpdated` | A message is edited | History plugin |
| `OnReactionAdded` | A reaction is added to a message | (available for custom plugins) |
| `OnUserJoined` | A user joins the server | Welcome messages, user count |
| `OnUserLeft` | A user leaves the server | Leave messages, user count |
| `OnAuditLogCreated` | A moderation action is recorded (kick, ban, timeout) | Warn/Log plugins |
| `OnInteractionCreated` | A button/select menu/modal interaction occurs | Advertising, Reglement |
| `OnThreadCreated` | A thread is created | (available for custom plugins) |
| `OnBotReady` | The bot is connected and ready | Background tasks, initial data fetch |

### Gateway thread safety

Discord.Net's gateway event handlers **must not block**. If a handler awaits a long operation, it blocks the gateway thread, which freezes the bot (no events received, heartbeats missed, eventual disconnection).

That's why `BotService` uses `Task.Run()` to offload work to the thread pool:

```csharp
_client.InteractionCreated += interaction =>
{
    _ = Task.Run(() => HandleInteractionAsync(interaction));
    return Task.CompletedTask;
};
```

Similarly, `PluginManager.WireDiscordEvents()` wraps some handlers (UserJoined, UserLeft, ThreadCreated) in `Task.Run()`.

## Slash commands

### Module discovery

Plugins define their commands in "module" classes that inherit from `InteractionModuleBase<SocketInteractionContext>`. When a plugin is loaded, the host calls `InteractionService.AddModulesAsync(assembly)` which scans the assembly for module classes and registers them.

### Auto-defer

Discord gives 3 seconds to respond to an interaction before it expires. Since most commands need more time (database lookups, API calls), `BotService` automatically defers all slash commands. This shows "Bot is thinking..." to the user and extends the response window to 15 minutes.

Commands then use `FollowupAsync()` instead of `RespondAsync()` to send their response.

**Exceptions to auto-defer:**

| Interaction type | Why not auto-deferred |
|---|---|
| User context menu commands | May need to show a modal as the first response |
| The `/sanction` command | Needs to show a modal for the sanction reason |

Discord modals can only be sent as the **first response** to an interaction. If the interaction is already deferred, showing a modal fails. That's why these interactions are excluded from auto-defer.

### Command fingerprinting

The system computes a fingerprint of all commands (name + permission bits) and compares it with what Discord currently has registered. Commands are only re-registered when something actually changed:

```
Local:  ["help:none", "ping:none", "warn:2147483648", "plugin:8"]
Remote: ["help:none", "ping:none", "warn:2147483648"]
         → "plugin" is new → re-register all commands
```

This avoids unnecessary API calls which would hit Discord's rate limits (especially problematic during development with frequent restarts).

### DefaultMemberPermissions mapping

Discord lets you control which users can **see** a command in the slash command picker using `DefaultMemberPermissions`. Bidibip maps its `BotRole` tiers to Discord permission bits:

1. At startup, the bot fetches the actual Discord permissions for each configured role (Administrator, Moderator, Helper, Member)
2. For each command, it computes the **intersection** of permissions across all roles at or above the command's minimum tier
3. This intersection becomes the command's `DefaultMemberPermissions`

For example, if a command requires `BotRole.Moderator`:
- It computes: `ModeratorPerms AND AdministratorPerms`
- The result is a set of permission bits shared by both roles
- Discord shows the command to anyone who has those permission bits

This is a **visibility hint only** — the actual permission check happens in the triple-gate system (see below).

## Permission system

### Triple-gate verification

Permissions are checked at three points for defense-in-depth:

```
Interaction arrives
      |
      v
  Gate 1: CheckPermissionGate() ──── BEFORE defer
      |                               (can respond with error immediately)
      v
  Auto-defer (if applicable)
      |
      v
  Gate 2: CheckPermissionGate() ──── AFTER defer
      |                               (defense-in-depth, uses FollowupAsync)
      v
  Gate 3: [AllowedBotRole] attribute  ── Discord.Net precondition
      |                               (runs inside ExecuteCommandAsync)
      v
  Command handler executes
```

Why three gates?
- Gate 1 catches unauthorized users before any processing, with a clean error response
- Gate 2 catches edge cases where roles changed between gate 1 and execution
- Gate 3 is the framework-level safety net inside Discord.Net's own precondition system

### Secure by default

Any command without an explicit `[AllowedBotRole]` attribute defaults to `BotRole.Administrator`. This prevents accidental permission leaks when a developer forgets to annotate a command.

### How role checking works

`PermissionHelper.HasRole()` compares the user's Discord roles against the configured role IDs:

1. If the minimum role is `Everyone`, always pass
2. If the user is the server owner, always pass
3. Otherwise, find the user's highest role tier and check if it's >= the required tier

## Logging pipeline

The bot uses Serilog with three sinks that run in parallel:

```
Log.Information("message")
      |
      +---> Console sink (immediate output)
      +---> File sink (Saved/logs/bidibip-YYYYMMDD-HHMMSS.log)
      +---> DiscordSink → DiscordLogService → Discord log channel
```

### Discord log channel

`DiscordLogService` batches log entries using a producer-consumer pattern:

1. `DiscordSink.Emit()` enqueues a `QueuedLog` (non-blocking, safe from any thread)
2. Before the bot connects, entries accumulate in the queue
3. After `MarkReady()`, a background loop drains the queue every 2 seconds
4. Normal entries (Info, Warning) are batched into a single Discord message
5. Error entries are sent immediately as individual messages with:
   - A role ping (the configured Support role) to alert the team
   - A separate error file (`Saved/logs/error-*.log`) with the full stack trace

### Log format in Discord

```
:green_circle: Plugin::Help Bot is ready
:yellow_circle: Services::BotService Rate limit hit on /api/interactions
:red_circle: Plugin::FreeForTheMonth @Support Failed to fetch Fab listings : connection timeout
```

### Log rotation

On startup, the bot keeps the 10 most recent log files and deletes older ones.

## Data persistence

All data is stored as JSON files in `Saved/`:

```
Saved/
  config.json              # Global bot configuration
  data/
    Log/config.json        # Log plugin config
    Welcome/config.json    # Welcome plugin config
    Warn/warns.json        # Warn plugin data
    ...
  logs/
    bidibip-20240315.log   # Log files (10 max, oldest auto-deleted)
    error-20240315-*.log   # Detailed error reports
```

### PluginData helper

The `PluginData` helper provides two methods:
- **`PluginData.LoadAsync<T>(path)`** — Loads a JSON file into a C# object. Creates the file with defaults if missing. Automatically merges new fields if the model has evolved (schema migration).
- **`PluginData.SaveAsync<T>(path, data)`** — Serializes and writes the object to the file.

### Schema migration

When a plugin adds new fields to its config model, existing config files are **not** overwritten. Instead, `PluginData.LoadAsync` performs a merge:

1. Loads the existing JSON file
2. Serializes a default instance of the model
3. Recursively compares both JSON objects
4. Adds any missing properties from the default to the file
5. Rewrites the file if anything was added

This means users never lose their settings after an update, and new fields appear automatically with their default values.

### Discord ID handling

Discord IDs are 64-bit unsigned integers (`ulong` in C#). JavaScript's `Number.MAX_SAFE_INTEGER` is 2^53, so storing these as JSON numbers risks precision loss. The `UlongJsonConverter` handles this by serializing `ulong` values as JSON strings and parsing both strings and numbers on read.

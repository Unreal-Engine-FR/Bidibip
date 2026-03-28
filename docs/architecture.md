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

1. **Load configuration** — Reads `.env` (token) and `Saved/config.json` (roles, channels)
2. **Connect to Discord** — The bot logs in via WebSocket with the required gateway intents (guilds, messages, message content, members, bans)
3. **Load plugins** — The `PluginManager` scans the `plugins/` folder and loads every DLL it finds
4. **Register commands** — All slash commands from all plugins are registered with Discord
5. **Ready** — The `BotReady` event is dispatched to every plugin

## Plugin loading

The `PluginManager` uses .NET's `AssemblyLoadContext` to isolate each plugin:

1. Scans the `plugins/` folder for `.dll` files
2. Copies each DLL to a `.shadow/` folder (avoids file locks so you can rebuild while the bot runs)
3. Loads the DLL in a dedicated `AssemblyLoadContext`
4. Looks for a class decorated with `[BidibipPlugin]` that implements `IBidibipPlugin`
5. Verifies SDK version compatibility (major.minor must match)
6. Calls `InitializeAsync()` with a `PluginContext` containing the logger, config, event bus, and more

### Hot-reload

A `FileSystemWatcher` monitors the `plugins/` folder. When a `.dll` file changes:

1. The old plugin is disposed (`DisposeAsync()`)
2. The old `AssemblyLoadContext` is unloaded (garbage collected)
3. The new DLL is loaded through the same process
4. Commands are re-registered with Discord if needed

This lets you recompile a plugin while the bot is running — the new version takes effect within a second, no restart needed.

## Event routing

The bot listens to all Discord events and distributes them to plugins via the event bus:

```
Discord WebSocket
      |
      v
  BotService (permission gates, auto-defer)
      |
      v
  PluginManager (dispatches to all plugins)
      |
      +---> Plugin A: OnMessageReceived
      +---> Plugin B: OnMessageReceived
      +---> Plugin A: OnUserJoined
      +---> Plugin C: OnInteractionCreated
      ...
```

Each plugin subscribes to the events it cares about in `InitializeAsync()`. Available events:

| Event | Triggered when |
|---|---|
| `OnMessageReceived` | A message is sent |
| `OnMessageDeleted` | A message is deleted |
| `OnMessageUpdated` | A message is edited |
| `OnReactionAdded` | A reaction is added to a message |
| `OnUserJoined` | A user joins the server |
| `OnUserLeft` | A user leaves the server |
| `OnAuditLogCreated` | An audit log entry is created (kick, ban, timeout) |
| `OnInteractionCreated` | A Discord interaction occurs (button, select menu, modal) |
| `OnThreadCreated` | A thread is created |
| `OnBotReady` | The bot is connected and ready |

## Slash commands

Plugins define their commands in "module" classes that inherit from `InteractionModuleBase<SocketInteractionContext>`. The host discovers them automatically through reflection and registers them with Discord.

The system computes a fingerprint of all commands (name + permissions) and compares it with what Discord currently has. Commands are only re-registered when something actually changed, to avoid API rate limits.

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
```

The `PluginData` helper provides two methods:
- **`PluginData.LoadAsync<T>(path)`** — Loads a JSON file into a C# object. Creates the file with defaults if missing. Automatically merges new fields if the model has evolved.
- **`PluginData.SaveAsync<T>(path, data)`** — Serializes and writes the object to the file.

Discord IDs (type `ulong`) are stored as JSON strings to avoid floating-point precision issues.

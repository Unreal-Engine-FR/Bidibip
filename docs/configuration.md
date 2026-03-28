# Configuration

## Config files overview

| File | Purpose |
|---|---|
| `.env` | Discord bot token (secret, never commit this) |
| `Saved/config.json` | Main bot config: server ID, role IDs, channel IDs |
| `Saved/data/{Plugin}/config.json` | Per-plugin configuration (auto-created) |

## `.env` — Discord token

```env
DISCORD__TOKEN=your-token-here
```

The double underscore `__` is the .NET convention for nested environment variables. This file is loaded at startup and merged into the bot's configuration system.

## `Saved/config.json` — Main configuration

This file is auto-created on first run. Replace all `0` values with real Discord IDs.

```json
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
```

### Fields

| Field | Description |
|---|---|
| `GuildId` | Your Discord server ID. Set to `0` to register slash commands globally (slower to propagate, recommended for production). Set to your server ID for instant command updates (useful during development). |
| `Roles.Administrator` | Discord role ID for administrators. Users with this role have full access to all commands. |
| `Roles.Moderator` | Discord role ID for moderators. |
| `Roles.Helper` | Discord role ID for helpers/support staff. |
| `Roles.Member` | Discord role ID for verified members (typically assigned after accepting rules). |
| `Roles.Mute` | Discord role ID for muted users (used by the AntiSpam plugin). |
| `Channels.LogChannel` | Channel ID where the bot sends activity logs. |
| `Channels.StaffChannel` | Channel ID for moderation alerts. |
| `Plugins.Path` | Path to the folder containing plugin DLLs. Relative to the working directory. |

### How to find Discord IDs

1. Enable **Developer Mode** in Discord: User Settings > Advanced > Developer Mode
2. Right-click any role, channel, or server
3. Click "Copy ID"

## Plugin configuration

Each plugin stores its own config in `Saved/data/{PluginName}/config.json`. These files are created automatically the first time a plugin loads, filled with default values.

### Automatic schema merging

When a plugin is updated and introduces new config fields, the existing config file is **not** overwritten. Instead, the bot:

1. Loads the existing file
2. Compares it against the plugin's default config model
3. Adds any missing fields with their default values
4. Rewrites the file with the merged result

This means you never lose your existing settings after an update, and new fields appear automatically.

See [Plugins](plugins.md) for the specific configuration of each plugin.

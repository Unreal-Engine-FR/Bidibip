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

The double underscore `__` is the .NET convention for nested environment variables. It maps to `Discord:Token` in the configuration system, which the bot reads at startup to authenticate with Discord.

This file is loaded at startup, parsed into environment variables, and merged into the bot's configuration. Environment variables take precedence over values in `config.json`.

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
      "Mute": "0",
      "Support": "0"
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

#### Bot

| Field | Description |
|---|---|
| `GuildId` | Your Discord server ID. Set to `0` to register slash commands globally (slower to propagate, recommended for production). Set to your server ID for instant command updates (useful during development). |

#### Roles

These map the bot's abstract permission tiers to actual Discord role IDs. See [Permissions](permissions.md) for how the role hierarchy works.

| Field | Permission tier | Description |
|---|---|---|
| `Roles.Administrator` | Tier 4 | Full access to all commands. |
| `Roles.Moderator` | Tier 3 | Moderation commands (warn, mute, kick, ban). |
| `Roles.Helper` | Tier 2 | Support staff commands. |
| `Roles.Member` | Tier 1 | Verified member commands (typically assigned after accepting rules). |
| `Roles.Mute` | N/A | Role assigned to muted users by the AntiSpam plugin. Not a permission tier. |
| `Roles.Support` | N/A | Role pinged in the log channel when an error occurs. Not a permission tier — this is for alerting, not access control. |

#### Channels

| Field | Description |
|---|---|
| `Channels.LogChannel` | Channel where the bot sends activity logs and error alerts. Error messages ping the Support role here. |
| `Channels.StaffChannel` | Channel for moderation alerts (spam detection, etc.). |

#### Plugins

| Field | Description |
|---|---|
| `Plugins.Path` | Path to the folder containing plugin DLLs. Relative to the working directory. Default: `plugins`. |

### How to find Discord IDs

1. Enable **Developer Mode** in Discord: User Settings > Advanced > Developer Mode
2. Right-click any role, channel, or server
3. Click "Copy ID"

### Example with real values

```json
{
  "Bot": {
    "GuildId": "329551638388015115",
    "Roles": {
      "Administrator": "329553341275308033",
      "Moderator": "1108800112081244190",
      "Helper": "1124351866642366515",
      "Member": "1108799832371515443",
      "Mute": "1115307412581257216",
      "Support": "1234567890123456789"
    },
    "Channels": {
      "LogChannel": "1108084616063103006",
      "StaffChannel": "1108084737974730752"
    }
  },
  "Plugins": {
    "Path": "plugins"
  }
}
```

## Plugin configuration

Each plugin stores its own config in `Saved/data/{PluginName}/config.json`. These files are created automatically the first time a plugin loads, filled with default values.

### Config location

Plugin configs live under `Saved/data/`, not `plugins/`:

```
Saved/
  data/
    Welcome/config.json      # Welcome plugin config
    Warn/warns.json          # Warn plugin data
    AntiSpam/config.json     # AntiSpam plugin config
    Advertising/config.json  # Advertising plugin config + state
    ...
```

### Automatic schema merging

When a plugin is updated and introduces new config fields, the existing config file is **not** overwritten. Instead, the bot:

1. Loads the existing file
2. Compares it against the plugin's default config model
3. Adds any missing fields with their default values
4. Rewrites the file with the merged result

This means you never lose your existing settings after an update, and new fields appear automatically.

### Example

If a plugin's config model adds a new `enabled` field with default `true`:

**Before update** (existing file):
```json
{
  "channel": "123456789"
}
```

**After update** (auto-merged):
```json
{
  "channel": "123456789",
  "enabled": true
}
```

See [Plugins](plugins.md) for the specific configuration of each plugin.

## Discord IDs in JSON

Discord IDs are 64-bit unsigned integers that exceed JavaScript's `Number.MAX_SAFE_INTEGER` (2^53). To avoid precision loss, the bot serializes all `ulong` values as JSON strings (e.g., `"329551638388015115"` instead of `329551638388015115`).

When reading, the bot accepts both strings and numbers, so you can use either format in your config files. But when the bot writes back to the file, it always uses strings.

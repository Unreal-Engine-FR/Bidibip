# Permissions

## Role hierarchy

Bidibip defines 5 permission levels. A user with a higher role always has access to commands from lower levels.

| Level | Role | Description |
|---|---|---|
| 0 | `Everyone` | Anyone, no role required |
| 1 | `Member` | Verified server member |
| 2 | `Helper` | Support staff |
| 3 | `Moderator` | Moderator |
| 4 | `Administrator` | Full access (default for all commands) |

The server owner always has full access regardless of their roles.

## Mapping to Discord roles

These bot roles are mapped to actual Discord role IDs in `Saved/config.json`:

```json
{
  "Bot": {
    "Roles": {
      "Administrator": "123456789012345678",
      "Moderator": "234567890123456789",
      "Helper": "345678901234567890",
      "Member": "456789012345678901",
      "Mute": "567890123456789012"
    }
  }
}
```

When checking permissions, the bot looks at which of these Discord roles the user has and determines their highest bot role level.

## Secure by default

**Any command without an explicit permission annotation defaults to `Administrator`.**

If a developer forgets to add `[AllowedBotRole(...)]` to a command, only administrators can use it. This is intentional — it prevents accidental permission leaks.

## Setting permissions on a command

In a command module, add `[AllowedBotRole(BotRole.X)]` on each method:

```csharp
[SlashCommand("ping", "Replies with pong")]
[AllowedBotRole(BotRole.Everyone)]    // Anyone can use /ping
public async Task PingAsync() { ... }

[SlashCommand("warn", "Warn a member")]
[AllowedBotRole(BotRole.Moderator)]   // Moderators and above
public async Task WarnAsync() { ... }

[SlashCommand("plugin", "Manage plugins")]
// No annotation -> Administrator by default
public async Task PluginAsync() { ... }
```

## Triple-gate verification

Permissions are checked at three points to ensure security:

1. **Pre-defer gate** — Before the bot even acknowledges the interaction
2. **Post-defer gate** — After acknowledging but before executing (in case roles changed)
3. **Precondition gate** — Discord.Net's built-in precondition system runs the `[AllowedBotRole]` attribute check during command execution

If any gate fails, the user gets an ephemeral error message and the command is not executed.

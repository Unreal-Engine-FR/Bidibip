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
      "Mute": "567890123456789012",
      "Support": "678901234567890123"
    }
  }
}
```

When checking permissions, the bot looks at which of these Discord roles the user has and determines their highest bot role level. For example, a user who has both the Helper and Moderator Discord roles is treated as a Moderator (tier 3).

Note: `Mute` and `Support` are **not** permission tiers. `Mute` is assigned to muted users, and `Support` is pinged on errors.

## Secure by default

**Any command without an explicit permission annotation defaults to `Administrator`.**

If a developer forgets to add `[AllowedBotRole(...)]` to a command, only administrators can use it. This is intentional — it prevents accidental permission leaks on new commands.

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

Permissions are checked at three independent points for defense-in-depth:

```
Interaction arrives from Discord
        |
        v
    Gate 1: Pre-defer check
        |   Checks user roles BEFORE acknowledging the interaction.
        |   Can respond with an ephemeral error immediately.
        |
        v
    Auto-defer (shows "Bot is thinking...")
        |
        v
    Gate 2: Post-defer check
        |   Re-checks roles after deferral in case they changed.
        |   Uses FollowupAsync for the error response.
        |
        v
    Gate 3: [AllowedBotRole] precondition
        |   Discord.Net's built-in precondition system runs the
        |   attribute check during ExecuteCommandAsync.
        |
        v
    Command handler executes
```

If any gate fails, the user gets an ephemeral error message ("Tu n'as pas la permission d'utiliser cette commande.") and the command is not executed.

### Why three gates?

- **Gate 1** catches unauthorized users before any processing, with a clean error response
- **Gate 2** catches edge cases where roles changed between gate 1 and execution (e.g., role removed by another bot during the defer)
- **Gate 3** is Discord.Net's framework-level safety net — the `[AllowedBotRole]` attribute acts as a precondition that runs inside `ExecuteCommandAsync`

Any single gate is sufficient for security, but having three means the system remains secure even if one mechanism has a bug or race condition.

## How role checking works

`PermissionHelper.HasRole()` determines if a user meets the minimum role requirement:

1. If the minimum role is `Everyone` → always pass
2. If the user is the server owner → always pass
3. Otherwise, scan the user's Discord roles and compare each against the configured role IDs to find their highest tier
4. If their highest tier >= the required tier → pass

```
User has Discord roles: [Member, Helper]
Command requires: BotRole.Moderator (tier 3)
User's highest tier: Helper (tier 2)
Result: denied (2 < 3)
```

```
User has Discord roles: [Member, Administrator]
Command requires: BotRole.Moderator (tier 3)
User's highest tier: Administrator (tier 4)
Result: allowed (4 >= 3)
```

## Command visibility in Discord (DefaultMemberPermissions)

Beyond access control, Bidibip also controls which users can **see** commands in Discord's slash command picker. This is a UX feature — hiding admin commands from regular users declutters the command list.

### How it works

1. At startup (READY event), the bot fetches the actual Discord permissions for each configured role
2. For each command, based on its `[AllowedBotRole]` tier, it computes an **intersection** of permission bits:

| Command tier | Intersection formula | Effect |
|---|---|---|
| `Administrator` | `AdminPerms` | Only users with admin-like perms see it |
| `Moderator` | `ModeratorPerms AND AdminPerms` | Users with shared mod/admin perms see it |
| `Helper` | `HelperPerms AND ModeratorPerms AND AdminPerms` | Users with shared helper+ perms see it |
| `Member` / `Everyone` | No restriction | Everyone sees it |

3. The intersection result becomes the command's `DefaultMemberPermissions` when registered with Discord

### Why intersections?

Discord shows a command to anyone who has the required permission bits. By intersecting across all tiers that should have access, we find permission bits that are common to all qualifying roles. This way, Discord correctly shows the command to all intended users.

### Important caveat

`DefaultMemberPermissions` is a **visibility hint only**. It controls what users see in the command picker, but it does not enforce access control. The actual permission check happens in the triple-gate system. A user who bypasses the UI (e.g., via API calls) would still be blocked by the gates.

### Role permission warnings

At startup, the bot warns if configured roles have identical Discord permissions (e.g., Member and Helper both having the same permission set). This would cause commands to be visible to the wrong users in the Discord UI, even though the gate checks would still block unauthorized execution.

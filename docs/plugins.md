# Plugins

All included plugins and what they do.

## Admin

**Manages plugins at runtime.**

Provides commands to list, enable, and disable plugins without restarting the bot.

| Command | Permission | Description |
|---|---|---|
| `/plugin` | Administrator | List all plugins and their status, enable or disable them |

Disabled plugins are remembered across restarts (stored in `plugins/disabled.json`).

---

## Advertising

**Full job posting system with a step-by-step form, preview, and moderation review.**

This is the most complex plugin. It provides an interactive workflow for users to create job postings (or candidacy announcements) that go through a review process before being published.

### How it works

1. A user runs `/annonce` in any channel
2. The bot creates a **private thread** in a configured channel
3. The user goes through a **multi-step form** inside the thread:
   - Title, description, who they are
   - Contract type (volunteering, internship, freelance, work-study, fixed-term, open-ended)
   - Role (recruiter or candidate) — this changes which fields are shown
   - Conditional fields: duration, compensation, location, studio name, skills, qualifications, responsibilities
   - Contact method, optional URLs
4. Each step is either **text input** (the user types in the thread) or a **button choice** (the bot shows options as buttons)
5. At the end, the bot shows a **preview** with formatted embeds
6. The user submits the posting for review
7. Users with a **reviewer role** can approve or reject the posting
8. Approved postings are published to a configured forum channel

### Features

- Users can **edit** any field before submitting (via button + modal)
- Users can **edit** already-published postings
- **Maximum ads per user** is configurable (default: 2)
- Form steps are **conditional** — changing "contract type" or "role" clears and re-asks dependent fields
- **Optional fields** can be skipped with a button

### Config

`Saved/data/Advertising/config.json`:

```json
{
  "ad_forum": "0",
  "in_progress_channel": "0",
  "reviewer_roles": [],
  "max_ad_per_user": 2,
  "in_progress": {},
  "stored_ads": {}
}
```

| Field | Description |
|---|---|
| `ad_forum` | Forum or channel ID where approved ads are published |
| `in_progress_channel` | Channel ID where creation threads are spawned |
| `reviewer_roles` | List of role IDs that can approve/reject postings |
| `max_ad_per_user` | Maximum number of concurrent ads per user |
| `in_progress` | Internal state: ongoing form sessions (do not edit) |
| `stored_ads` | Internal state: published ads per user (do not edit) |

### Form steps

The form adapts based on user choices. Here's the full flow:

```
Title → Description → Who are you → Contract type
                                         |
              +--------------------------+---------------------------+
              |                          |                           |
         internship                  freelance/CDD/CDI          volunteering
              |                          |                           |
         Duration                   Duration                    (skip pay)
         Paid? (Y/N)               Compensation                     |
         Gratification                   |                           |
              |                          |                           |
              +--------------------------+---------------------------+
                                         |
                                       Role
                                         |
                          +--------------+--------------+
                          |                             |
                      Recruiter                      Candidate
                          |                             |
                    Location type                 Location type
                    Location detail                Location detail
                    Studio name                   Skills
                    Responsibilities                    |
                    Qualifications                      |
                          |                             |
                          +--------------+--------------+
                                         |
                                   Contact method
                                   Contact info (if not Discord)
                                   Other URLs (optional)
                                         |
                                      Preview
                                         |
                                      Submit
```

---

## AntiSpam

**Detects spam and auto-mutes offenders.**

Monitors messages for repetitive content. When a user sends the same (or very similar) message multiple times in a short period, the bot:
1. Mutes the user (assigns the configured Mute role)
2. Sends an alert to the staff channel
3. Logs the event

### Config

`Saved/data/AntiSpam/config.json`:

| Field | Description |
|---|---|
| `mute_role` | Role ID to assign when muting (should match `Roles.Mute` in main config) |
| `moderation_channel` | Channel ID for spam alerts |

---

## Example

**A template plugin for learning and reference.**

Does two things:
- Responds to `!hello` messages with "Hello from the Example plugin!"
- Provides a `/ping` command (accessible to everyone) that replies "Pong from Example plugin!"

This plugin is meant to be copied as a starting point for new plugins. See [Creating a plugin](creating-a-plugin.md).

---

## FreeForTheMonth

**Announces free assets from the Fab.com marketplace.**

Every month, Fab.com (the Unreal Engine / Epic Games marketplace) offers a selection of assets for free. This plugin automatically detects when the free assets change and posts an announcement with rich embeds (title, image, rating, price, seller info).

### How it works

1. A **background task** checks the Fab.com API periodically (default: every 6 hours)
2. When new free listings are detected (compared by UID against the last known set), an announcement is posted to the configured channel
3. Users can also run `/freeforthemonth` to see the current free listings on demand
4. Users can **subscribe** to a notification role to be pinged when new assets are announced

### Technical note

Fab.com uses Cloudflare with TLS fingerprinting that blocks standard HTTP clients. The plugin uses [curl-impersonate](https://github.com/lexiforest/curl-impersonate) (Chrome profile) to bypass this protection. This requires `curl-impersonate` to be installed in the Docker container (included in the default Dockerfile).

| Command | Permission | Description |
|---|---|---|
| `/freeforthemonth` | Member | View current free assets, optionally subscribe/unsubscribe to notifications |

### Config

`Saved/data/FreeForTheMonth/config.json`:

| Field | Description |
|---|---|
| `channel` | Channel ID where automatic announcements are posted |
| `notify_ffm_role` | Role ID to ping when new free assets are detected. Users can self-assign this role via the `/freeforthemonth subscribe:Oui` option |
| `check_interval_hours` | How often the background task checks for new assets (default: 6 hours, minimum: 1) |
| `known_listings` | Internal state: UIDs of the last announced listings (do not edit) |

---

## Help

**Lists all available commands.**

| Command | Permission | Description |
|---|---|---|
| `/help` | Everyone | Shows all registered slash commands with their required permission level |

The list is generated dynamically from whatever plugins are currently loaded.

---

## History

**Logs edited and deleted messages.**

Watches for message edits and deletions and posts them to the configured log channel with the original content (from the message cache).

### Config

`Saved/data/History/config.json`:

| Field | Description |
|---|---|
| `channel_blacklist` | List of channel IDs to ignore (e.g., bot-spam channels) |

---

## Log

**Logs server activity and slash command usage.**

Records every slash command execution to the log channel, showing who ran what command and when. Can be configured to exclude certain channels.

### Config

`Saved/data/Log/config.json`:

| Field | Description |
|---|---|
| `channel_blacklist` | List of channel IDs to ignore |

---

## Modo

**Moderation ticket system.**

Allows users to create private support threads with the moderation team.

### Config

`Saved/data/Modo/config.json`: configurable channels and roles for the ticket system.

---

## Reglement

**Server rules enforcement with role assignment.**

This plugin is triggered when a **file is uploaded** to the rules channel. It reads the uploaded JSON file and posts the rules as a series of text messages, embeds, and interactive buttons.

When a user clicks the approval button, the bot assigns them the Member role.

### How it works

1. An admin uploads a JSON file to the rules channel
2. The bot parses the JSON and posts:
   - Text blocks (`textes`)
   - Rich embeds (`embeds`)
   - Buttons (`interactions`) — typically an "I accept the rules" button
3. When a user clicks the approval button (`reglement_approval`), the bot assigns the Member role silently

### JSON format

The uploaded JSON file should follow this structure:

```json
{
  "textes": ["First text block", "Second text block"],
  "embeds": [
    {
      "titre": "Rules",
      "description": "Server rules here...",
      "couleur": "#3498db"
    }
  ],
  "interactions": [
    {
      "bouton": {
        "identifiant": "reglement_approval",
        "texte": "I accept the rules",
        "type": "Success"
      }
    }
  ]
}
```

---

## Repost

**Automatically reposts old advertisements.**

A background task that monitors a channel and bumps posts that haven't been reposted in a configurable time period.

### Config

`Saved/data/Repost/config.json`: configurable channel, repost interval, and message formatting.

---

## Say

**Bot message relay.**

| Command | Permission | Description |
|---|---|---|
| `/say` | Moderator | Makes the bot send a message in the current channel |

Useful for posting announcements or messages as the bot.

---

## Update

**Check and install bot updates from Discord.**

| Command | Permission | Description |
|---|---|---|
| `/update` | Moderator | Check for new versions and trigger an update |

The command:
1. Reads the current version from `version.txt` (written during build)
2. Queries the [GitHub Releases API](https://api.github.com/repos/Unreal-Engine-FR/Bidibip/releases/latest)
3. If a newer version exists, reports the versions and shuts down the bot
4. Docker restarts the container, and the entrypoint downloads the new release

If the bot is already up to date, it simply reports that.

---

## UserCount

**Displays the server member count.**

Sets the bot's activity status to "Nous sommes {count} membres" and updates it whenever a user joins or leaves.

On startup, it downloads the full member list to get an accurate count.

---

## Warn

**Sanction system with history.**

Provides moderation commands to warn, mute, kick, or ban users, with a persistent history of all sanctions.

| Command | Permission | Description |
|---|---|---|
| `/sanction` | Moderator | Apply a sanction to a user (warn, mute, kick, ban) with a reason |

All sanctions are stored in `Saved/data/Warn/warns.json` and can be consulted later.

---

## Welcome

**Join and leave announcements.**

Sends customizable messages when users join or leave the server.

### Config

`Saved/data/Welcome/config.json`:

| Field | Description |
|---|---|
| `join_channel` | Channel ID for welcome messages |
| `leave_channel` | Channel ID for leave messages |
| `reglement_channel` | Channel ID for the rules (used in `{reglement}` placeholder) |
| `welcome_messages` | List of welcome message templates (one is picked randomly) |
| `leave_messages` | List of leave message templates |

Templates support placeholders:
- `{user}` — Mentions the user
- `{reglement}` — Links to the rules channel

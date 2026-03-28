# Bidibip v5

Bidibip is a modular Discord bot built with .NET 8. It uses a plugin architecture where every feature (moderation, welcome messages, anti-spam, job postings, etc.) is an independent plugin loaded at runtime.

Plugins can be enabled, disabled, or hot-reloaded without restarting the bot. You don't need to know C# to install and run it.

## Quick start (Docker)

The recommended way to run Bidibip. The container automatically downloads the latest release on every start. No .NET installation required.

```bash
# Create a folder for the bot
mkdir bidibip && cd bidibip

# Download the docker-compose file
curl -fsSLO https://raw.githubusercontent.com/Unreal-Engine-FR/Bidibip/bidibip-v5/docker-compose.yml

# Create the .env file with your Discord token
echo "DISCORD__TOKEN=your-token-here" > .env

# Create the required directories
mkdir -p Saved plugins

# Start the bot
docker compose up -d
```

On first launch the bot creates `Saved/config.json` with placeholder values. You need to fill in your server's role and channel IDs for the bot to work properly. See [Configuration](docs/configuration.md).

## Documentation

| Document | Description |
|---|---|
| [Installation](docs/installation.md) | Full installation guide (Docker, binaries, from source) |
| [Configuration](docs/configuration.md) | Setting up the bot and plugin configs |
| [Architecture](docs/architecture.md) | How the bot works under the hood |
| [Creating a plugin](docs/creating-a-plugin.md) | Step-by-step guide to writing your own plugin |
| [Plugins](docs/plugins.md) | Description of every included plugin |
| [Permissions](docs/permissions.md) | Role hierarchy and access control |
| [Updating](docs/updating.md) | How to update the bot |

## Project structure

```
Bidibip/
  src/
    Bidibip/                    # Main application (host)
    Bidibip.Plugin.Sdk/         # SDK library for plugin development
    plugins/                    # Plugin source code
  docker/                       # Dockerfile and entrypoint script
  plugins/                      # Compiled plugin DLLs (build output)
  Saved/                        # Persistent data (created at runtime)
    config.json                 # Main bot configuration
    data/                       # Per-plugin data
    logs/                       # Log files
  .env                          # Discord token (never committed)
  docker-compose.yml            # Docker Compose definition
```

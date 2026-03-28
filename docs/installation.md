# Installation

## Prerequisites

- A [Discord bot application](https://discord.com/developers/applications) with a token
- The bot must be invited to your server with the `Administrator` permission (or at minimum: Manage Roles, Send Messages, Read Messages, Use Slash Commands, Manage Threads)
- The following **Privileged Gateway Intents** must be enabled in the Discord Developer Portal:
  - `Server Members Intent`
  - `Message Content Intent`

## Method 1: Docker (recommended)

This is the simplest approach. The container downloads the latest release binaries automatically on every startup. No .NET installation required.

### Step 1 — Create a directory

```bash
mkdir bidibip && cd bidibip
```

### Step 2 — Download the compose file

```bash
curl -fsSLO https://raw.githubusercontent.com/Unreal-Engine-FR/Bidibip/bidibip-v5/docker-compose.yml
```

This gives you:

```yaml
services:
  bot:
    image: ghcr.io/unreal-engine-fr/bidibip-v5:latest
    container_name: bidibip-v5
    restart: unless-stopped
    volumes:
      - ./Saved:/opt/bidibip/Saved
      - ./plugins:/opt/bidibip/plugins
      - ./.env:/opt/bidibip/.env
```

### Step 3 — Create the `.env` file

```bash
echo "DISCORD__TOKEN=your-token-here" > .env
```

Replace `your-token-here` with your actual Discord bot token.

> The separator is a **double underscore** `__`, not a single dot. This is a .NET convention for environment variables.

### Step 4 — Create the data directories

```bash
mkdir -p Saved plugins
```

### Step 5 — Start

```bash
docker compose up -d
```

View logs:

```bash
docker compose logs -f
```

### How it works

Every time the container starts, the entrypoint script:
1. Downloads the latest release from GitHub Releases
2. Extracts the bot binary and plugin DLLs
3. Runs the bot

When the bot restarts (via the `/update` command or a crash), Docker automatically restarts the container thanks to `restart: unless-stopped`, and the entrypoint re-downloads the latest version.

## Method 2: Pre-built binaries

### Step 1 — Download

Grab the latest release from [GitHub Releases](https://github.com/Unreal-Engine-FR/Bidibip/releases/latest):
- `Bidibip-linux-x64.tar.gz` — standard Linux
- `Bidibip-linux-alpine-x64.tar.gz` — Alpine Linux

### Step 2 — Extract

```bash
tar xzf Bidibip-linux-x64.tar.gz
cd linux-x64
```

### Step 3 — Configure

Create a `.env` file next to the executable:

```bash
echo "DISCORD__TOKEN=your-token-here" > .env
```

### Step 4 — Run

```bash
chmod +x Bidibip
./Bidibip
```

## Method 3: Build from source

### Additional prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```bash
git clone https://github.com/Unreal-Engine-FR/Bidibip.git
cd Bidibip

# Build the bot
dotnet publish src/Bidibip/Bidibip.csproj -c Release -o out

# Build all plugins
for csproj in src/plugins/*/Bidibip.Plugins.*.csproj; do
  dotnet build "$csproj" -c Release -o out/plugins
done
```

### Run

```bash
cd out
echo "DISCORD__TOKEN=your-token-here" > .env
./Bidibip
```

## First launch

On the first run, the bot creates `Saved/config.json` with default values (all IDs set to `0`). The bot will connect to Discord but won't function properly until you fill in the real IDs.

See [Configuration](configuration.md) for the next steps.

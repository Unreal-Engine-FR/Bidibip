# Updating

## Docker (recommended)

### Using the `/update` command

If the **Update** plugin is loaded, moderators can check for updates and apply them directly from Discord:

1. Type `/update` in any channel
2. The bot checks the latest release on GitHub
3. If a new version is available, it shows the current and new version, then shuts down
4. Docker automatically restarts the container (thanks to `restart: unless-stopped`)
5. The entrypoint script downloads the new release and starts the bot

The whole process takes a few seconds.

### Manual restart

Since the Docker container downloads the latest release on every start, you can also update by simply restarting:

```bash
docker compose restart
```

Or to also pull a new version of the container image itself:

```bash
docker compose pull
docker compose up -d
```

## Pre-built binaries

1. Download the latest release from [GitHub Releases](https://github.com/Unreal-Engine-FR/Bidibip/releases/latest)
2. Extract it over your existing installation
3. Restart the bot

Your `Saved/` directory and `.env` file are preserved since they are outside the binary directory.

## From source

```bash
git pull
dotnet publish src/Bidibip/Bidibip.csproj -c Release -o out
for csproj in src/plugins/*/Bidibip.Plugins.*.csproj; do
  dotnet build "$csproj" -c Release -o out/plugins
done
```

Then restart the bot.

## What happens to config files

Config files are **never** overwritten by updates. When a plugin adds new configuration fields, the automatic schema merging fills them in with default values the next time the plugin loads. Your existing settings are always preserved.

## Release process

Releases are triggered by pushing a version tag:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The GitHub Actions workflow then:
1. Builds self-contained binaries for Linux x64 and Alpine x64
2. Builds all plugin DLLs
3. Packages everything as `.tar.gz` archives
4. Creates a GitHub Release with the archives attached
5. Builds and pushes the Docker image to `ghcr.io/unreal-engine-fr/bidibip-v5:latest`

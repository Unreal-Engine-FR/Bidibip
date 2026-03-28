# Creating a plugin

This guide walks you through creating a Bidibip plugin from scratch. Each plugin is a standalone C# project that compiles to a `.dll` file.

## Quick start

The fastest way to create a plugin is to copy the **Example** plugin and modify it.

```bash
cp -r src/plugins/Bidibip.Plugins.Example src/plugins/Bidibip.Plugins.MyPlugin
```

Then rename files and namespaces. The rest of this guide explains what each piece does.

## Project setup

### 1. Create the project file

Create `src/plugins/Bidibip.Plugins.MyPlugin/Bidibip.Plugins.MyPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <Import Project="../../Bidibip.Plugin.props" />

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>Bidibip.Plugins.MyPlugin</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputPath>../../../plugins/</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Bidibip.Plugin.Sdk\Bidibip.Plugin.Sdk.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
    <PackageReference Include="Discord.Net.Core" Version="3.17.2">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Discord.Net.Interactions" Version="3.17.2">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Discord.Net.WebSocket" Version="3.17.2">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

Key points:
- **`<Import Project="../../Bidibip.Plugin.props" />`** — Imports shared build properties, including automatic version incrementing.
- **`<OutputPath>../../../plugins/</OutputPath>`** — The compiled DLL goes straight into the `plugins/` folder, ready for the bot to load.
- **`<Private>false</Private>` + `<ExcludeAssets>runtime</ExcludeAssets>`** — The SDK and Discord.Net DLLs are already loaded by the host. The plugin must not bundle its own copies, otherwise it would cause type conflicts.

### 2. Add the project to the solution

```bash
dotnet sln add src/plugins/Bidibip.Plugins.MyPlugin/Bidibip.Plugins.MyPlugin.csproj
```

## The plugin class

Every plugin needs exactly one class that:
1. Has the `[BidibipPlugin]` attribute
2. Implements `IBidibipPlugin`

```csharp
using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.MyPlugin;

[BidibipPlugin]
public sealed class MyPlugin : IBidibipPlugin
{
    private ILogger? _logger;

    // Display name shown in /help and plugin management
    public string Name => "MyPlugin";

    // Short description
    public string Description => "Does something useful.";

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _logger.LogInformation("MyPlugin loaded!");

        // Subscribe to events here (see below)

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger?.LogInformation("MyPlugin unloaded.");
        return ValueTask.CompletedTask;
    }
}
```

## The PluginContext

When the bot calls `InitializeAsync()`, it passes a `PluginContext` with everything a plugin needs:

| Property | Type | Description |
|---|---|---|
| `Logger` | `ILogger` | Plugin-specific logger (outputs to console and log files) |
| `Configuration` | `IConfiguration` | Full bot configuration (rarely needed directly) |
| `Events` | `IEventBus` | Subscribe to Discord events |
| `BotConfig` | `BotConfig` | Quick access to role IDs, channel IDs, guild ID |
| `Commands` | `ICommandRegistry` | List all registered commands |
| `Client` | `DiscordSocketClient` | Direct access to the Discord client |
| `DataPath` | `string` | Path to `Saved/data/{PluginName}/` for persistent storage |

## Listening to events

Subscribe to events in `InitializeAsync()`:

```csharp
public Task InitializeAsync(PluginContext context)
{
    // React to messages
    context.Events.OnMessageReceived(async msg =>
    {
        if (msg.Author.IsBot) return;

        if (msg.Content == "!hello")
        {
            await msg.Channel.SendMessageAsync("Hello!");
        }
    });

    // React to users joining
    context.Events.OnUserJoined(async user =>
    {
        context.Logger.LogInformation("{User} joined the server", user.Username);
    });

    // Do something when the bot is ready
    context.Events.OnBotReady(async () =>
    {
        context.Logger.LogInformation("Bot is ready!");
    });

    return Task.CompletedTask;
}
```

## Adding slash commands

Slash commands are defined in separate "module" classes. Create a `Commands/` folder in your plugin:

### Basic command

`Commands/GreetModule.cs`:

```csharp
using Bidibip.Plugin.Sdk.Permissions;
using Discord.Interactions;

namespace Bidibip.Plugins.MyPlugin.Commands;

public class GreetModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("greet", "Say hello to someone")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task GreetAsync(
        [Summary("user", "The user to greet")] IUser user)
    {
        await FollowupAsync($"Hello {user.Mention}!");
    }
}
```

Key points:
- The class must inherit from `InteractionModuleBase<SocketInteractionContext>`
- Each command method needs `[SlashCommand("name", "description")]`
- Add `[AllowedBotRole(BotRole.X)]` to set the permission level (defaults to Administrator if omitted)
- Use `FollowupAsync()` to reply (the bot auto-defers most commands)

### Command with choices

```csharp
[SlashCommand("color", "Pick a color")]
[AllowedBotRole(BotRole.Everyone)]
public async Task ColorAsync(
    [Summary("color", "Your favorite color")]
    [Choice("Red", "red"), Choice("Blue", "blue"), Choice("Green", "green")]
    string color)
{
    await FollowupAsync($"You picked {color}!");
}
```

### User context menu command

```csharp
[UserCommand("User info")]
[AllowedBotRole(BotRole.Moderator)]
public async Task UserInfoAsync(IUser user)
{
    await RespondAsync($"{user.Username} joined on {((IGuildUser)user).JoinedAt}",
        ephemeral: true);
}
```

## Persisting data

Use `PluginData` to save and load JSON config or data files.

### Define a config model

```csharp
using System.Text.Json.Serialization;

namespace Bidibip.Plugins.MyPlugin;

public sealed class MyConfig
{
    [JsonPropertyName("welcome_channel")]
    public ulong WelcomeChannel { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Welcome!";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
```

Always set default values on properties. When the bot loads a config file and finds missing fields, it fills them in with these defaults.

### Load and save

```csharp
using Bidibip.Plugin.Sdk;

[BidibipPlugin]
public sealed class MyPlugin : IBidibipPlugin
{
    private string _configPath = null!;

    public Task InitializeAsync(PluginContext context)
    {
        _configPath = Path.Combine(context.DataPath, "config.json");

        // Load config (creates the file with defaults if it doesn't exist)
        var config = await PluginData.LoadAsync<MyConfig>(_configPath);

        // Use the config
        if (config.Enabled)
        {
            context.Logger.LogInformation("Channel: {Id}", config.WelcomeChannel);
        }

        // Save changes
        config.Message = "Updated!";
        await PluginData.SaveAsync(_configPath, config);

        return Task.CompletedTask;
    }

    // ...
}
```

The config file is stored at `Saved/data/MyPlugin/config.json` and looks like:

```json
{
  "welcome_channel": "0",
  "message": "Welcome!",
  "enabled": true
}
```

Users can edit this file directly. Discord IDs (`ulong`) are automatically serialized as strings to avoid precision issues.

## Building and testing

### Build your plugin

```bash
dotnet build src/plugins/Bidibip.Plugins.MyPlugin/Bidibip.Plugins.MyPlugin.csproj
```

The DLL is output to `plugins/Bidibip.Plugins.MyPlugin.dll`. If the bot is already running, it detects the file change and hot-reloads the plugin automatically.

### Build in Release mode

```bash
dotnet build src/plugins/Bidibip.Plugins.MyPlugin/Bidibip.Plugins.MyPlugin.csproj -c Release
```

## Full example

Here is a complete minimal plugin with a slash command and event handling:

```csharp
using Bidibip.Plugin.Sdk;
using Bidibip.Plugin.Sdk.Permissions;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Counter;

[BidibipPlugin]
public sealed class CounterPlugin : IBidibipPlugin
{
    private int _messageCount;

    public string Name => "Counter";
    public string Description => "Counts messages and provides a /count command.";

    public Task InitializeAsync(PluginContext context)
    {
        context.Events.OnMessageReceived(async msg =>
        {
            if (!msg.Author.IsBot)
                Interlocked.Increment(ref _messageCount);
        });

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// In a separate file: Commands/CountModule.cs
public class CountModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("count", "Show the message count since last restart")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task CountAsync()
    {
        // Note: accessing plugin state from a command module requires
        // a shared static field or a service registered in DI.
        await FollowupAsync("Use the plugin's event-based approach for real counting.");
    }
}
```

## Tips

- **Always check `msg.Author.IsBot`** in message handlers to avoid reacting to the bot's own messages (or other bots).
- **Use `FollowupAsync()`** instead of `RespondAsync()` for slash commands — the bot auto-defers most interactions.
- **Don't bundle SDK or Discord.Net DLLs** — they're provided by the host. The `.csproj` template above handles this with `<Private>false</Private>`.
- **Use `context.DataPath`** for file storage — it points to a dedicated folder for your plugin under `Saved/data/`.
- **Set default values** on all config properties so new fields are automatically populated.

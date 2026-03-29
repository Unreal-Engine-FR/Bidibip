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
- **`<Private>false</Private>` + `<ExcludeAssets>runtime</ExcludeAssets>`** — The SDK and Discord.Net DLLs are already loaded by the host. The plugin must not bundle its own copies, otherwise types would conflict (e.g., the host's `IBidibipPlugin` and the plugin's `IBidibipPlugin` would be different types).

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
        // Clean up resources here (cancel background tasks, close connections, etc.)
        _logger?.LogInformation("MyPlugin unloaded.");
        return ValueTask.CompletedTask;
    }
}
```

## The PluginContext

When the bot calls `InitializeAsync()`, it passes a `PluginContext` with everything a plugin needs:

| Property | Type | Description |
|---|---|---|
| `Logger` | `ILogger` | Plugin-specific logger (tagged with `Plugin.{Name}`, outputs to console, file, and Discord channel) |
| `Configuration` | `IConfiguration` | Plugin-specific configuration from `plugins/{DllName}/config.json` |
| `Events` | `IEventBus` | Subscribe to Discord events (messages, reactions, joins, etc.) |
| `BotConfig` | `BotConfig` | Quick access to role IDs, channel IDs, guild ID from the global config |
| `Commands` | `ICommandRegistry` | List all registered slash commands across all plugins |
| `Client` | `DiscordSocketClient` | Direct access to the Discord client for operations not covered by the event bus |
| `DataPath` | `string` | Path to `Saved/data/{PluginName}/` for persistent storage (directory is pre-created) |

## Listening to events

Subscribe to events in `InitializeAsync()`. Handlers are automatically cleaned up when the plugin is unloaded — you don't need to unsubscribe manually.

```csharp
public Task InitializeAsync(PluginContext context)
{
    // React to messages
    context.Events.OnMessageReceived(async msg =>
    {
        // IMPORTANT: always check IsBot to avoid reacting to bot messages
        // (including the bot's own messages, which would cause infinite loops)
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

    // Do something when the bot is ready (connected to Discord)
    // Use this for tasks that need the Discord client to be operational,
    // like fetching channels or downloading the member list.
    context.Events.OnBotReady(async () =>
    {
        context.Logger.LogInformation("Bot is ready!");
    });

    return Task.CompletedTask;
}
```

### Available events

| Event | Parameter | When it fires |
|---|---|---|
| `OnMessageReceived` | `IMessage` | Any message in a visible channel |
| `OnMessageDeleted` | `Cacheable<IMessage>, Cacheable<IMessageChannel>` | A message is deleted (may not be in cache) |
| `OnMessageUpdated` | `Cacheable<IMessage>, IMessage, IMessageChannel` | A message is edited |
| `OnReactionAdded` | `IReaction, IMessageChannel` | A reaction is added |
| `OnUserJoined` | `IGuildUser` | A user joins the server |
| `OnUserLeft` | `IGuild, IUser` | A user leaves (or is kicked/banned) |
| `OnAuditLogCreated` | `AuditLogEntry` | A moderation action is recorded (kick, ban, timeout) |
| `OnInteractionCreated` | `SocketInteraction` | A button, select menu, or modal interaction |
| `OnThreadCreated` | `SocketThreadChannel` | A thread is created |
| `OnBotReady` | (none) | The bot is connected and ready |

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
- Add `[AllowedBotRole(BotRole.X)]` to set the permission level (defaults to Administrator if omitted — see [Permissions](permissions.md))
- **Use `FollowupAsync()` to reply** — the bot auto-defers most slash commands, so the interaction is already acknowledged by the time your handler runs

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

Right-click on a user → Apps → Your command:

```csharp
[UserCommand("User info")]
[AllowedBotRole(BotRole.Moderator)]
public async Task UserInfoAsync(IUser user)
{
    // User context menu commands are NOT auto-deferred (they may need to show modals).
    // Use RespondAsync() here, not FollowupAsync().
    await RespondAsync($"{user.Username} joined on {((IGuildUser)user).JoinedAt}",
        ephemeral: true);
}
```

### Command with a modal (popup form)

Modals can only be the **first response** to an interaction. If your slash command needs a modal, it must be excluded from auto-defer in `BotService.cs` (see the `"sanction"` example).

```csharp
// In your module:
[SlashCommand("feedback", "Submit feedback")]
[AllowedBotRole(BotRole.Everyone)]
public async Task FeedbackAsync()
{
    // This only works if the command is NOT auto-deferred.
    // You must add an exclusion in BotService.HandleInteractionAsync().
    await RespondWithModalAsync<FeedbackModal>("feedback_modal");
}

// The modal definition:
public class FeedbackModal : IModal
{
    public string Title => "Your Feedback";

    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Tell us what you think...")]
    public string Message { get; set; } = "";
}

// Handle the modal submission:
[ModalInteraction("feedback_modal")]
[AllowedBotRole(BotRole.Everyone)]
public async Task HandleFeedbackAsync(FeedbackModal modal)
{
    await RespondAsync($"Thanks for your feedback: {modal.Message}", ephemeral: true);
}
```

### Handling button/select menu interactions

Buttons and select menus use custom IDs. The `OnInteractionCreated` event bus or Discord.Net's `[ComponentInteraction]` attribute can handle them:

```csharp
// Send a message with a button
[SlashCommand("poll", "Start a simple poll")]
[AllowedBotRole(BotRole.Everyone)]
public async Task PollAsync([Summary("question", "The poll question")] string question)
{
    var builder = new ComponentBuilder()
        .WithButton("Yes", "poll_yes", ButtonStyle.Success)
        .WithButton("No", "poll_no", ButtonStyle.Danger);

    await FollowupAsync(question, components: builder.Build());
}

// Handle button clicks (the * wildcard matches any suffix)
[ComponentInteraction("poll_*")]
[AllowedBotRole(BotRole.Everyone)]
public async Task HandlePollButtonAsync()
{
    var interaction = Context.Interaction as SocketMessageComponent;
    var choice = interaction?.Data.CustomId == "poll_yes" ? "Yes" : "No";
    await RespondAsync($"{Context.User.Mention} voted: {choice}", ephemeral: true);
}
```

### Accessing BotConfig in command modules

Command modules are created by Discord.Net's DI system. You can inject `BotConfig` via constructor:

```csharp
public class MyModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BotConfig _botConfig;

    // BotConfig is automatically injected from the shared service provider
    public MyModule(BotConfig botConfig)
    {
        _botConfig = botConfig;
    }

    [SlashCommand("info", "Show server info")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task InfoAsync()
    {
        await FollowupAsync($"Guild ID: {_botConfig.GuildId}");
    }
}
```

Available injectable services:
- `BotConfig` — Global bot configuration
- `DiscordSocketClient` — The Discord client
- `ICommandRegistry` — List of all commands
- `IPluginManager` — Enable/disable plugins
- `ILoggerFactory` / `ILogger<T>` — Logging

## Background tasks

Use `OnBotReady` to start long-running tasks. Use a `CancellationTokenSource` to stop them cleanly on disposal.

```csharp
[BidibipPlugin]
public sealed class ReminderPlugin : IBidibipPlugin
{
    private CancellationTokenSource? _cts;
    private ILogger? _logger;

    public string Name => "Reminder";
    public string Description => "Periodic reminders.";

    public Task InitializeAsync(PluginContext context)
    {
        _logger = context.Logger;
        _cts = new CancellationTokenSource();

        context.Events.OnBotReady(async () =>
        {
            // Start the background loop after the bot is connected
            _ = BackgroundLoopAsync(context.Client, _cts.Token);
        });

        return Task.CompletedTask;
    }

    private async Task BackgroundLoopAsync(DiscordSocketClient client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), ct);
                // Do periodic work here...
                _logger?.LogInformation("Hourly check completed");
            }
            catch (OperationCanceledException)
            {
                break; // Plugin is being unloaded
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Background task failed");
                // Don't rethrow — keep the loop running
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
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

Always set default values on properties. When the bot loads a config file and finds missing fields, it fills them in with these defaults automatically (schema migration).

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

Users can edit this file directly. Discord IDs (`ulong`) are automatically serialized as strings to avoid precision issues in JSON.

### Schema migration

When you add a new field to your config model and release an update, existing config files are not overwritten. Instead, `PluginData.LoadAsync` automatically:

1. Loads the existing JSON
2. Compares it with a default instance of your model
3. Adds any missing fields with their default values
4. Rewrites the file

This means users keep their settings, and new fields appear automatically.

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

Here is a complete minimal plugin with a slash command, event handling, and data persistence:

```csharp
// CounterPlugin.cs
using Bidibip.Plugin.Sdk;
using Microsoft.Extensions.Logging;

namespace Bidibip.Plugins.Counter;

[BidibipPlugin]
public sealed class CounterPlugin : IBidibipPlugin
{
    // Static field so the command module can access it
    // (modules are created by DI and don't have direct access to the plugin instance)
    internal static int MessageCount;

    public string Name => "Counter";
    public string Description => "Counts messages and provides a /count command.";

    public Task InitializeAsync(PluginContext context)
    {
        context.Events.OnMessageReceived(async msg =>
        {
            if (!msg.Author.IsBot)
                Interlocked.Increment(ref MessageCount);
        });

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

```csharp
// Commands/CountModule.cs
using Bidibip.Plugin.Sdk.Permissions;
using Discord.Interactions;
using Discord.WebSocket;

namespace Bidibip.Plugins.Counter.Commands;

public class CountModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("count", "Show the message count since last restart")]
    [AllowedBotRole(BotRole.Everyone)]
    public async Task CountAsync()
    {
        var count = CounterPlugin.MessageCount;
        await FollowupAsync($"Messages counted since restart: **{count}**");
    }
}
```

## Tips

- **Always check `msg.Author.IsBot`** in message handlers to avoid reacting to the bot's own messages (or other bots).
- **Use `FollowupAsync()`** for slash commands and **`RespondAsync()`** for non-deferred interactions (user context menus, modals).
- **Don't bundle SDK or Discord.Net DLLs** — they're provided by the host. The `.csproj` template above handles this with `<Private>false</Private>`.
- **Use `context.DataPath`** for file storage — it points to a dedicated folder for your plugin under `Saved/data/`.
- **Set default values** on all config properties so new fields are automatically populated via schema migration.
- **Cancel background tasks in `DisposeAsync()`** — the plugin may be hot-reloaded at any time.
- **Prefer `ephemeral: true`** for error responses and admin commands so they don't clutter the channel.
- **Log errors, don't swallow them** — use `context.Logger.LogError(ex, "...")` so errors appear in the Discord log channel.

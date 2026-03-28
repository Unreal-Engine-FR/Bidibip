using System.Text.Json.Serialization;

namespace Bidibip.Plugins.Welcome;

public sealed class WelcomeConfig
{
    [JsonPropertyName("join_channel")]
    public ulong JoinChannel { get; set; }

    [JsonPropertyName("leave_channel")]
    public ulong LeaveChannel { get; set; }

    [JsonPropertyName("reglement_channel")]
    public ulong ReglementChannel { get; set; }

    [JsonPropertyName("welcome_messages")]
    public string[] WelcomeMessages { get; set; } = [];

    [JsonPropertyName("leave_messages")]
    public string[] LeaveMessages { get; set; } = [];

    public static WelcomeConfig CreateDefault() => new()
    {
        JoinChannel = 0,
        LeaveChannel = 0,
        ReglementChannel = 0,
        WelcomeMessages =
        [
            "Bienvenue {user} ! N'oublie pas de lire le {reglement} !",
            "Hey {user}, content de te voir parmi nous ! Jette un oeil au {reglement} !",
            "{user} vient de rejoindre le serveur, bienvenue !"
        ],
        LeaveMessages =
        [
            "{user} a quitté le serveur. À bientôt !",
            "{user} nous a quittés.",
            "Au revoir {user} !"
        ]
    };
}

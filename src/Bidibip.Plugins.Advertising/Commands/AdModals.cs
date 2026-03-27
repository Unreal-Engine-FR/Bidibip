using Discord;
using Discord.Interactions;

namespace Bidibip.Plugins.Advertising.Commands;

public class EditFieldModal : IModal
{
    public string Title => "Modifier";

    [InputLabel("Nouveau contenu")]
    [ModalTextInput("text", TextInputStyle.Paragraph, "Nouveau contenu")]
    public string Text { get; set; } = "";
}

public class DenyModal : IModal
{
    public string Title => "Contenu probl\u00e9matique";

    [InputLabel("Raison")]
    [ModalTextInput("reason", TextInputStyle.Paragraph, "Raison du refus")]
    public string Reason { get; set; } = "";
}

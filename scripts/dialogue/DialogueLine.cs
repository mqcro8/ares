namespace ares.scripts.dialogue;

using Godot;

[GlobalClass]
public partial class DialogueLine : Resource
{
	[Export] public string Speaker { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
}

namespace ares.scripts.ending;

using Godot;

/// <summary>One image and one timed dialogue line in an ending cinematic.</summary>
[GlobalClass]
public partial class CinematicBeat : Resource
{
	[Export] public Texture2D Image { get; set; }
	[Export] public string Speaker { get; set; } = "MISSION CONTROL";
	[Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
	[Export(PropertyHint.Range, "1,20,0.1")] public double HoldSeconds { get; set; } = 5.0;
	[Export(PropertyHint.Range, "0,3,0.1")] public double CrossfadeSeconds { get; set; } = 0.8;
}

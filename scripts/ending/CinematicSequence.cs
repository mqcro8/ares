namespace ares.scripts.ending;

using Godot;

/// <summary>Ordered beats for one mission-ending cinematic.</summary>
[GlobalClass]
public partial class CinematicSequence : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public Godot.Collections.Array<CinematicBeat> Beats { get; set; } = new();
}

namespace Ares.Systems.Audio;

using Godot;

/// <summary>The shared catalog of music and SFX. Edit the instance in data/audio/sound_bank.tres.</summary>
[GlobalClass]
public partial class SoundBank : Resource
{
	[Export] public Godot.Collections.Array<SoundDef> Music { get; set; } = new();

	[Export] public Godot.Collections.Array<SoundDef> Sfx { get; set; } = new();
}

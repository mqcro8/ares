namespace Ares.Systems.Audio;

using Godot;

/// <summary>One entry in the shared sound catalog.</summary>
[GlobalClass]
public partial class SoundDef : Resource
{
	/// <summary>Name used by code, e.g. "rover_collision".</summary>
	[Export] public string Id { get; set; } = "";

	/// <summary>A single stream, or an AudioStreamRandomizer for variations.</summary>
	[Export] public AudioStream Stream { get; set; }

	/// <summary>Per-sound trim. The Music/SFX bus volume is controlled by the options menu.</summary>
	[Export(PropertyHint.Range, "-40,6,0.1")] public float VolumeDb { get; set; }

	/// <summary>Random pitch spread for repeated one-shots, e.g. 0.05 for impacts.</summary>
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float PitchVariation { get; set; }

	/// <summary>Loop this stream. Use for music, ambience, and continuous emitters.</summary>
	[Export] public bool Loop { get; set; }
}

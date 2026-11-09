namespace Ares.Systems.Audio;

using Godot;

/// <summary>
/// A positional 3D sound attached to a node. Use it for looping or moving sounds
/// (machinery, engines, ambience). For a quick one-shot, use AudioManager.PlaySfx3D.
/// Configure UnitSize, MaxDistance, and AttenuationModel in the inspector.
/// </summary>
[GlobalClass]
public partial class SfxEmitter3D : AudioStreamPlayer3D
{
	private const float MuteDb = -80f;

	/// <summary>Id from data/audio/sound_bank.tres.</summary>
	[Export] public string SoundId { get; set; } = "";

	/// <summary>Start playing as soon as the scene is ready.</summary>
	[Export] public bool PlayOnReady { get; set; } = true;

	/// <summary>Local trim on top of the SoundDef volume.</summary>
	[Export(PropertyHint.Range, "-40,6,0.1")] public float VolumeDbOffset { get; set; }

	private float _baseVolumeDb;
	private Tween _fadeTween;

	public override void _Ready()
	{
		Bus = AudioManager.SfxBusName;

		if (string.IsNullOrWhiteSpace(SoundId))
		{
			GD.PushWarning("SfxEmitter3D: SoundId is empty.");
			return;
		}

		if (!AudioManager.TryGetSfx(SoundId, out var def))
		{
			GD.PushWarning($"SfxEmitter3D: unknown sound id '{SoundId}'.");
			return;
		}

		Stream = def.Stream;
		_baseVolumeDb = def.VolumeDb + VolumeDbOffset;
		VolumeDb = _baseVolumeDb;

		if (def.PitchVariation > 0f)
			PitchScale = 1f + (float)GD.RandRange(-def.PitchVariation, def.PitchVariation);

		if (PlayOnReady)
			Play();
	}

	/// <summary>Start or restart, cancelling any fade-out in progress.</summary>
	public void Play()
	{
		_fadeTween?.Kill();
		VolumeDb = _baseVolumeDb;
		base.Play();
	}

	/// <summary>Start or restart, cancelling any fade-out in progress.</summary>
	public void PlaySound() => Play();

	/// <summary>Fade out and stop. Use 0 for an immediate stop.</summary>
	public void Stop(float fadeSeconds)
	{
		if (!Playing || fadeSeconds <= 0f)
		{
			_fadeTween?.Kill();
			StopImmediately();
			return;
		}

		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(this, "volume_db", MuteDb, fadeSeconds);
		_fadeTween.TweenCallback(Callable.From(StopImmediately));
	}

	private void StopImmediately()
	{
		VolumeDb = _baseVolumeDb;
		base.Stop();
	}
}

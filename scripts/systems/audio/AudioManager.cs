namespace Ares.Systems.Audio;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Global audio service. Registered as an autoload.
/// Scenes call the static methods; they never create their own players for one-shots.
/// Music is a single, crossfaded track. SFX uses pre-created voice pools.
/// </summary>
public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	private const string BankPath = "res://data/audio/sound_bank.tres";
	private const int SfxVoices = 16;
	private const int Sfx2DVoices = 16;
	private const int Sfx3DVoices = 16;
	private const float MuteDb = -80f;

	public const string MusicBusName = "Music";
	public const string SfxBusName = "SFX";

	private readonly Dictionary<string, SoundDef> _musicDefs = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, SoundDef> _sfxDefs = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _warnedIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly RandomNumberGenerator _rng = new();

	private AudioStreamPlayer[] _sfxVoices = Array.Empty<AudioStreamPlayer>();
	private AudioStreamPlayer2D[] _sfx2DVoices = Array.Empty<AudioStreamPlayer2D>();
	private AudioStreamPlayer3D[] _sfx3DVoices = Array.Empty<AudioStreamPlayer3D>();
	private int _nextSfxVoice;
	private int _nextSfx2DVoice;
	private int _nextSfx3DVoice;

	private AudioStreamPlayer _musicA;
	private AudioStreamPlayer _musicB;
	private AudioStreamPlayer _activeMusic;
	private string _currentMusicId;
	private Tween _musicTween;
	private bool _loaded;
	private bool _musicPaused;
	private static bool _warnedNotReady;

	/// <summary>Id of the track that is currently active, or null when music is stopped.</summary>
	public string CurrentMusicId => _currentMusicId;
	public static bool IsMusicPaused => Instance?._musicPaused ?? false;
	public static bool IsActiveMusicStreamPaused => Instance?._activeMusic?.StreamPaused ?? false;

	public override void _EnterTree()
	{
		Instance = this;
		_loaded = false;
		_warnedNotReady = false;
	}

	public override void _ExitTree()
	{
		_musicTween?.Kill();
		Release(_musicA);
		Release(_musicB);

		foreach (var player in _sfxVoices)
			Release(player);
		foreach (var player in _sfx2DVoices)
			Release(player);
		foreach (var player in _sfx3DVoices)
			Release(player);

		if (Instance == this)
			Instance = null;
	}

	private static void Release(AudioStreamPlayer player)
	{
		if (player == null) return;
		player.Stop();
		player.Stream = null;
	}

	private static void Release(AudioStreamPlayer2D player)
	{
		if (player == null) return;
		player.Stop();
		player.Stream = null;
	}

	private static void Release(AudioStreamPlayer3D player)
	{
		if (player == null) return;
		player.Stop();
		player.Stream = null;
	}

	public override void _Ready()
	{
		_rng.Randomize();
		LoadBank();
		CreatePlayers();
		_loaded = true;
	}

	// ---------------------------------------------------------------------
	// Public API
	// ---------------------------------------------------------------------

	public static void PlayMusic(string id, double fadeSeconds = 1.0, bool restart = false)
	{
		if (EnsureReady()) Instance.PlayMusicInternal(id, fadeSeconds, restart);
	}

	public static void StopMusic(double fadeSeconds = 0.5)
	{
		if (EnsureReady()) Instance.StopMusicInternal(fadeSeconds);
	}

	public static void SetMusicPaused(bool paused)
	{
		if (EnsureReady()) Instance.SetMusicPausedInternal(paused);
	}

	public static void PlaySfx(string id, float volumeDb = 0f, float pitchScale = 1f)
	{
		if (EnsureReady()) Instance.PlaySfxInternal(id, volumeDb, pitchScale);
	}

	public static void PlaySfx2D(string id, Vector2 globalPosition, float volumeDb = 0f, float pitchScale = 1f)
	{
		if (EnsureReady()) Instance.PlaySfx2DInternal(id, globalPosition, volumeDb, pitchScale);
	}

	public static void PlaySfx3D(string id, Vector3 globalPosition, float volumeDb = 0f, float pitchScale = 1f)
	{
		if (EnsureReady()) Instance.PlaySfx3DInternal(id, globalPosition, volumeDb, pitchScale);
	}

	public static void StopAllSfx()
	{
		if (EnsureReady()) Instance.StopAllSfxInternal();
	}

	public static bool TryGetSfx(string id, out SoundDef def)
	{
		def = null;
		return EnsureReady() && Instance._sfxDefs.TryGetValue(id ?? string.Empty, out def);
	}

	public static bool TryGetMusic(string id, out SoundDef def)
	{
		def = null;
		return EnsureReady() && Instance._musicDefs.TryGetValue(id ?? string.Empty, out def);
	}

	private static bool EnsureReady()
	{
		if (Instance != null && Instance._loaded)
			return true;

		if (!_warnedNotReady)
		{
			_warnedNotReady = true;
			GD.PushWarning("AudioManager: called before it was ready; audio was skipped.");
		}

		return false;
	}

	// ---------------------------------------------------------------------
	// Catalog setup
	// ---------------------------------------------------------------------

	private void LoadBank()
	{
		var bank = ResourceLoader.Load<SoundBank>(BankPath);
		if (bank == null)
		{
			GD.PushError($"AudioManager: could not load '{BankPath}'.");
			return;
		}

		if (bank.Music != null)
			foreach (var def in bank.Music)
				Register(_musicDefs, def, "music");

		if (bank.Sfx != null)
			foreach (var def in bank.Sfx)
				Register(_sfxDefs, def, "sfx");
	}

	private static void Register(Dictionary<string, SoundDef> target, SoundDef def, string category)
	{
		if (def == null)
		{
			GD.PushWarning($"AudioManager: null {category} entry in sound bank.");
			return;
		}

		if (string.IsNullOrWhiteSpace(def.Id))
		{
			GD.PushWarning($"AudioManager: {category} entry has no Id.");
			return;
		}

		if (def.Stream == null)
		{
			GD.PushWarning($"AudioManager: {category} entry '{def.Id}' has no Stream.");
			return;
		}

		ApplyLoop(def.Stream, def.Loop);

		if (!target.TryAdd(def.Id, def))
			GD.PushWarning($"AudioManager: duplicate {category} id '{def.Id}'.");
	}

	private static void ApplyLoop(AudioStream stream, bool loop)
	{
		switch (stream)
		{
			case AudioStreamMP3 mp3:
				mp3.Loop = loop;
				break;
			case AudioStreamOggVorbis ogg:
				ogg.Loop = loop;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
				break;
			case AudioStreamRandomizer randomizer:
				for (int i = 0; i < randomizer.StreamsCount; i++)
				{
					var child = randomizer.GetStream(i);
					if (child != null)
						ApplyLoop(child, loop);
				}
				break;
		}
	}

	private void CreatePlayers()
	{
		_sfxVoices = new AudioStreamPlayer[SfxVoices];
		for (int i = 0; i < SfxVoices; i++)
		{
			var player = new AudioStreamPlayer { Bus = SfxBusName };
			AddChild(player);
			_sfxVoices[i] = player;
		}

		_sfx2DVoices = new AudioStreamPlayer2D[Sfx2DVoices];
		for (int i = 0; i < Sfx2DVoices; i++)
		{
			var player = new AudioStreamPlayer2D { Bus = SfxBusName };
			AddChild(player);
			_sfx2DVoices[i] = player;
		}

		_sfx3DVoices = new AudioStreamPlayer3D[Sfx3DVoices];
		for (int i = 0; i < Sfx3DVoices; i++)
		{
			var player = new AudioStreamPlayer3D { Bus = SfxBusName };
			AddChild(player);
			_sfx3DVoices[i] = player;
		}

		_musicA = new AudioStreamPlayer { Bus = MusicBusName, VolumeDb = MuteDb, ProcessMode = ProcessModeEnum.Always };
		_musicB = new AudioStreamPlayer { Bus = MusicBusName, VolumeDb = MuteDb, ProcessMode = ProcessModeEnum.Always };
		AddChild(_musicA);
		AddChild(_musicB);
	}

	// ---------------------------------------------------------------------
	// SFX
	// ---------------------------------------------------------------------

	private void PlaySfxInternal(string id, float volumeDb, float pitchScale)
	{
		if (!TryGetDef(_sfxDefs, id, out var def))
			return;

		var player = NextVoice(_sfxVoices, ref _nextSfxVoice);
		player.Stream = def.Stream;
		player.Bus = SfxBusName;
		player.VolumeDb = def.VolumeDb + volumeDb;
		player.PitchScale = pitchScale * RandomPitch(def);
		player.Play();
	}

	private void PlaySfx2DInternal(string id, Vector2 globalPosition, float volumeDb, float pitchScale)
	{
		if (!TryGetDef(_sfxDefs, id, out var def))
			return;

		var player = NextVoice(_sfx2DVoices, ref _nextSfx2DVoice);
		player.Stream = def.Stream;
		player.Bus = SfxBusName;
		player.VolumeDb = def.VolumeDb + volumeDb;
		player.PitchScale = pitchScale * RandomPitch(def);
		player.GlobalPosition = globalPosition;
		player.Play();
	}

	private void PlaySfx3DInternal(string id, Vector3 globalPosition, float volumeDb, float pitchScale)
	{
		if (!TryGetDef(_sfxDefs, id, out var def))
			return;

		var player = NextVoice(_sfx3DVoices, ref _nextSfx3DVoice);
		player.Stream = def.Stream;
		player.Bus = SfxBusName;
		player.VolumeDb = def.VolumeDb + volumeDb;
		player.PitchScale = pitchScale * RandomPitch(def);
		player.GlobalPosition = globalPosition;
		player.Play();
	}

	private void StopAllSfxInternal()
	{
		foreach (var player in _sfxVoices)
			if (player.Playing) player.Stop();

		foreach (var player in _sfx2DVoices)
			if (player.Playing) player.Stop();

		foreach (var player in _sfx3DVoices)
			if (player.Playing) player.Stop();
	}

	private float RandomPitch(SoundDef def)
	{
		if (def.PitchVariation <= 0f)
			return 1f;

		return 1f + _rng.RandfRange(-def.PitchVariation, def.PitchVariation);
	}

	private bool TryGetDef(Dictionary<string, SoundDef> map, string id, out SoundDef def)
	{
		if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out def))
			return true;

		def = null;
		string key = string.IsNullOrEmpty(id) ? "<empty>" : id;
		if (_warnedIds.Add(key))
			GD.PushWarning($"AudioManager: unknown sound id '{key}'.");

		return false;
	}

	private static AudioStreamPlayer NextVoice(AudioStreamPlayer[] pool, ref int cursor)
	{
		for (int i = 0; i < pool.Length; i++)
		{
			int index = (cursor + i) % pool.Length;
			if (!pool[index].Playing)
			{
				cursor = (index + 1) % pool.Length;
				return pool[index];
			}
		}

		var stolen = pool[cursor];
		stolen.Stop();
		cursor = (cursor + 1) % pool.Length;
		return stolen;
	}

	private static AudioStreamPlayer2D NextVoice(AudioStreamPlayer2D[] pool, ref int cursor)
	{
		for (int i = 0; i < pool.Length; i++)
		{
			int index = (cursor + i) % pool.Length;
			if (!pool[index].Playing)
			{
				cursor = (index + 1) % pool.Length;
				return pool[index];
			}
		}

		var stolen = pool[cursor];
		stolen.Stop();
		cursor = (cursor + 1) % pool.Length;
		return stolen;
	}

	private static AudioStreamPlayer3D NextVoice(AudioStreamPlayer3D[] pool, ref int cursor)
	{
		for (int i = 0; i < pool.Length; i++)
		{
			int index = (cursor + i) % pool.Length;
			if (!pool[index].Playing)
			{
				cursor = (index + 1) % pool.Length;
				return pool[index];
			}
		}

		var stolen = pool[cursor];
		stolen.Stop();
		cursor = (cursor + 1) % pool.Length;
		return stolen;
	}

	// ---------------------------------------------------------------------
	// Music
	// ---------------------------------------------------------------------

	private void PlayMusicInternal(string id, double fadeSeconds, bool restart)
	{
		if (!TryGetDef(_musicDefs, id, out var def))
			return;

		fadeSeconds = Math.Max(0.0, fadeSeconds);

		if (!restart && _currentMusicId == id && _activeMusic != null && _activeMusic.Playing)
			return;

		_musicTween?.Kill();

		var incoming = _activeMusic == _musicA ? _musicB : _musicA;
		var outgoing = _activeMusic;

		// If a previous fade-out was interrupted, the inactive player may still be audible.
		var stale = incoming == _musicA ? _musicB : _musicA;
		if (stale != outgoing && stale.Playing)
			stale.Stop();

		incoming.Stop();
		incoming.Stream = def.Stream;
		incoming.Bus = MusicBusName;
		incoming.VolumeDb = fadeSeconds > 0 ? MuteDb : def.VolumeDb;
		incoming.Play();
		incoming.StreamPaused = _musicPaused;

		_activeMusic = incoming;
		_currentMusicId = id;

		if (fadeSeconds <= 0)
		{
			if (outgoing != null && outgoing != incoming)
				outgoing.Stop();
			return;
		}

		_musicTween = CreateTween();
		_musicTween.SetPauseMode(Tween.TweenPauseMode.Process);
		_musicTween.SetParallel(true);
		_musicTween.TweenProperty(incoming, "volume_db", def.VolumeDb, fadeSeconds);

		if (outgoing != null && outgoing != incoming)
		{
			_musicTween.TweenProperty(outgoing, "volume_db", MuteDb, fadeSeconds);
			_musicTween.Chain().TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(outgoing) && outgoing.Playing)
					outgoing.Stop();
			}));
		}
	}

	private void SetMusicPausedInternal(bool paused)
	{
		_musicPaused = paused;
		if (_musicA != null) _musicA.StreamPaused = paused;
		if (_musicB != null) _musicB.StreamPaused = paused;
	}

	private void StopMusicInternal(double fadeSeconds)
	{
		fadeSeconds = Math.Max(0.0, fadeSeconds);

		if (_activeMusic == null && _currentMusicId == null)
			return;

		_musicTween?.Kill();

		var active = _activeMusic;
		var other = active == _musicA ? _musicB : _musicA;

		_activeMusic = null;
		_currentMusicId = null;

		// A crossfade may have left the previous track fading; silence it now.
		if (other != null && other.Playing)
			other.Stop();

		if (fadeSeconds <= 0 || active == null)
		{
			if (active != null) active.Stop();
			return;
		}

		_musicTween = CreateTween();
		_musicTween.SetPauseMode(Tween.TweenPauseMode.Process);
		_musicTween.TweenProperty(active, "volume_db", MuteDb, fadeSeconds);
		_musicTween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(active)) active.Stop();
		}));
	}
}

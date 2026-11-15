using Godot;
using System;
using System.Linq;
using Ares.Systems.Audio;

namespace ares.scripts.stages;

public partial class RocketMalfunction : Control
{
	public const string ScenePath = "res://scenes/cinematics/RocketMalfunction.tscn";
	internal const string GatewayScene = "res://scenes/gateway/Gateway.tscn";
	private const string AudioPath = "res://audio/sfx/system-malfunction-sound-effects.wav";
	private static readonly string[] Frames = Enumerable.Range(0, 12)
		.Select(index => $"res://art/rocket/malfunction_frames/frame_{index:00}.png").ToArray();

	private AnimatedSprite2D _animation;
	private AudioStreamPlayer _sound;
	private bool _transitioning;
	internal Func<string, Error> SceneLoader { get; set; }

	public override void _Ready()
	{
		_animation = new AnimatedSprite2D
		{
			Name = "MalfunctionAnimation",
			Centered = true,
			Position = GetViewportRect().Size / 2,
			Scale = Vector2.One * Math.Min(GetViewportRect().Size.X / 430f, GetViewportRect().Size.Y / 430f),
		};
		var frames = new SpriteFrames();
		frames.AddAnimation("malfunction");
		frames.SetAnimationSpeed("malfunction", 1000.0 / 180.0);
		frames.SetAnimationLoopMode("malfunction", SpriteFrames.LoopMode.Linear);
		foreach (var path in Frames)
		{
			var texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushError($"Unable to load malfunction frame: {path}");
				TransitionToGateway();
				return;
			}
			frames.AddFrame("malfunction", texture);
		}
		_animation.SpriteFrames = frames;
		AddChild(_animation);
		_animation.Play("malfunction");

		_sound = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>(AudioPath),
			Bus = AudioManager.SfxBusName,
			ProcessMode = ProcessModeEnum.Pausable
		};
		AddChild(_sound);
		if (_sound.Stream == null)
		{
			GD.PushError($"Unable to load malfunction audio: {AudioPath}");
			TransitionToGateway();
			return;
		}
		_sound.Finished += TransitionToGateway;
		_sound.Play();
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_sound))
			_sound.Finished -= TransitionToGateway;
	}

	internal void TransitionToGateway()
	{
		if (_transitioning) return;
		_transitioning = true;
		var error = SceneLoader != null ? SceneLoader(GatewayScene) : GetTree().ChangeSceneToFile(GatewayScene);
		if (error != Error.Ok)
		{
			_transitioning = false;
			GD.PushError($"Unable to load Gateway after malfunction cutscene: {error}");
		}
	}
}

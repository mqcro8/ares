using System;
using Godot;

namespace Ares.Systems.Audio;

// Musica solo para testing, cambiar despues
public partial class MusicPlayer : AudioStreamPlayer
{
	private const string MusicBus = "Music";
	private const string StreamPath = "res://audio/music/menu_test_music.mp3";
	private const string MenuPrefix = "res://scenes/main_menu/";

	private Node _currentScene;

	public override void _Ready()
	{
		Bus = MusicBus;
		Stream = GD.Load<AudioStream>(StreamPath);
		if (Stream == null)
			GD.PushWarning($"MusicPlayer: could not load \"{StreamPath}\".");
	}

	public override void _Process(double delta)
	{
		Node scene = GetTree().CurrentScene;
		if (scene == _currentScene)
			return;
		_currentScene = scene;
		if (scene == null)
			return;

		if (scene.SceneFilePath.StartsWith(MenuPrefix, StringComparison.Ordinal))
		{
			if (!Playing)
				Play();
		}
		else if (Playing)
		{
			Stop();
		}
	}
}

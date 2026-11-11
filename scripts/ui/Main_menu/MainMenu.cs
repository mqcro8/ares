using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		GetNodeOrNull<Button>("MenuLayout/MenuStack/VBoxContainer2/Start")?.GrabFocus();
		var telemetry = GetNodeOrNull<Label>("Telemetry");
		if (telemetry != null)
		{
			var tween = CreateTween().SetLoops();
			tween.TweenProperty(telemetry, "modulate:a", 0.35f, 1.4).SetTrans(Tween.TransitionType.Sine);
			tween.TweenProperty(telemetry, "modulate:a", 1.0f, 1.4).SetTrans(Tween.TransitionType.Sine);
		}
	}

	public void OnStartPressed()
	{
		const string introScene = "res://scenes/intro/Intro.tscn";
		const string trainingScene = "res://scenes/training/Training.tscn";
		string target = ResourceLoader.Exists(introScene) ? introScene : trainingScene;
		if (GetTree().ChangeSceneToFile(target) != Error.Ok) return;
		ares.scripts.mission.MissionState.Instance.BeginFromMainMenu();
		if (target == trainingScene)
			ares.scripts.mission.MissionState.Instance.CompleteIntro();
	}

	public void OnOptionsPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn");
	}

	public void OnCreditsPressed()
	{
		const string creditsScene = "res://scenes/credits/credits.tscn";
		if (ResourceLoader.Exists(creditsScene))
			GetTree().ChangeSceneToFile(creditsScene);
		else
			GD.PushWarning("Credits: credits scene not implemented yet.");
	}

	public void OnQuitGamePressed()
	{
		GetTree().Quit();
	}
}

using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class MainMenu : Control, ares.scripts.systems.IDevSceneSkipTarget
{
	public bool CanSkipWithDevCode => IsInsideTree();
	private readonly FeedbackSounds _sounds = new();
	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		GetNodeOrNull<Button>("MenuLayout/MenuStack/VBoxContainer2/Start")?.GrabFocus();
		UiNavigationSounds.Attach(this);
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
		if (GetTree().ChangeSceneToFile(target) != Error.Ok)
		{
			_sounds.Play(SoundIds.UiDenied, 300, true);
			return;
		}
		_sounds.Play(SoundIds.UiConfirm, 100, true);
		ares.scripts.mission.MissionState.Instance.BeginFromMainMenu();
		if (target == trainingScene)
			ares.scripts.mission.MissionState.Instance.CompleteIntro();
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		OnStartPressed();
		return true;
	}

	public void OnOptionsPressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn") == Error.Ok
			? SoundIds.UiConfirm : SoundIds.UiDenied, 300, true);
	}

	public void OnCreditsPressed()
	{
		const string creditsScene = "res://scenes/credits/credits.tscn";
		if (ResourceLoader.Exists(creditsScene))
			_sounds.Play(GetTree().ChangeSceneToFile(creditsScene) == Error.Ok
				? SoundIds.UiConfirm : SoundIds.UiDenied, 300, true);
		else
		{
			_sounds.Play(SoundIds.UiDenied, 300, true);
			GD.PushWarning("Credits: credits scene not implemented yet.");
		}
	}

	public void OnQuitGamePressed()
	{
		GetTree().Quit();
	}
}

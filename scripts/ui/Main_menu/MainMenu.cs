using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
	}

	public void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/training/Training.tscn");
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

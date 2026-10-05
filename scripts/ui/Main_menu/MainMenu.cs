using Godot;

namespace Ares.Ui.MainMenu;

public partial class MainMenu : Control
{
	public void OnStartPressed()
	{
		const string introScene = "res://scenes/intro/intro.tscn";
		if (ResourceLoader.Exists(introScene))
			GetTree().ChangeSceneToFile(introScene);
		else
			GD.PushWarning("Start: intro scene not implemented yet.");
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

using Godot;

namespace Ares.Ui.MainMenu;

public partial class ControlsMenu : Control
{
	public void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn");
	}
}

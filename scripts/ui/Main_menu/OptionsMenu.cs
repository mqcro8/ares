using Godot;

namespace Ares.Ui.MainMenu;

public partial class OptionsMenu : Control
{
	public void OnVolumePressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Volume_menu.tscn");
	}

	public void OnControlsPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Control_menu.tscn");
	}

	public void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn");
	}
}

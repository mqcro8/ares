using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class ControlsMenu : Control
{
	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
	}

	public void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn");
	}
}

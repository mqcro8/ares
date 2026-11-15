using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class OptionsMenu : Control, ares.scripts.systems.IDevSceneSkipTarget
{
	public bool CanSkipWithDevCode => IsInsideTree();
	private readonly FeedbackSounds _sounds = new();
	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		UiNavigationSounds.Attach(this);
	}

	public void OnVolumePressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/Volume_menu.tscn") == Error.Ok
			? SoundIds.UiConfirm : SoundIds.UiDenied, 300, true);
	}

	public void OnControlsPressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/Control_menu.tscn") == Error.Ok
			? SoundIds.UiConfirm : SoundIds.UiDenied, 300, true);
	}

	public void OnBackPressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn") == Error.Ok
			? SoundIds.UiBack : SoundIds.UiDenied, 300, true);
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		OnBackPressed();
		return true;
	}
}

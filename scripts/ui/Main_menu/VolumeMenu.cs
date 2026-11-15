using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class VolumeMenu : Control, ares.scripts.systems.IDevSceneSkipTarget
{
	private const double SliderMax = 100.0;
	private readonly FeedbackSounds _sounds = new();
	public bool CanSkipWithDevCode => IsInsideTree();

	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		SyncSlider("Panel/Panel2/MasterSlider", "Master");
		SyncSlider("Panel/Panel2/MusicSlider", "Music");
		SyncSlider("Panel/Panel2/VFXSlider", "SFX");
		UiNavigationSounds.Attach(this);
	}

	public void OnMasterVolumeChanged(double value)
	{
		SetBusVolume("Master", value);
	}

	public void OnMusicVolumeChanged(double value)
	{
		SetBusVolume("Music", value);
	}

	public void OnSfxVolumeChanged(double value)
	{
		SetBusVolume("SFX", value);
	}

	public void OnBackPressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn") == Error.Ok
			? SoundIds.UiBack : SoundIds.UiDenied, 300, true);
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		OnBackPressed();
		return true;
	}

	private static void SetBusVolume(string busName, double sliderValue)
	{
		int bus = AudioServer.GetBusIndex(busName);
		if (bus == -1)
		{
			GD.PushWarning($"VolumeMenu: audio bus \"{busName}\" not found.");
			return;
		}

		AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb((float)(sliderValue / SliderMax)));
	}

	private void SyncSlider(string sliderPath, string busName)
	{
		if (GetNodeOrNull<HSlider>(sliderPath) is not { } slider)
		{
			GD.PushWarning($"VolumeMenu: slider \"{sliderPath}\" not found.");
			return;
		}

		int bus = AudioServer.GetBusIndex(busName);
		if (bus == -1)
		{
			GD.PushWarning($"VolumeMenu: audio bus \"{busName}\" not found.");
			return;
		}

		slider.Value = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(bus)) * SliderMax;
	}
}

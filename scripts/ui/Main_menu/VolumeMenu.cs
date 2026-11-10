using Ares.Systems.Audio;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class VolumeMenu : Control
{
	private const double SliderMax = 100.0;

	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		SyncSlider("Panel/Panel2/MasterSlider", "Master");
		SyncSlider("Panel/Panel2/MusicSlider", "Music");
		SyncSlider("Panel/Panel2/VFXSlider", "SFX");
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
		GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn");
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

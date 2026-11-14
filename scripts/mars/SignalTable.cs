namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;

/// <summary>
/// Large signal table in the third building. Opens the waveform/frequency
/// calibration mini-game once; the completed state is stored in MissionState.
/// </summary>
public partial class SignalTable : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/FrequencyBoxMinigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _calibrated;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_calibrated = MissionState.Instance?.SignalTableRepaired ?? false;
		ApplyState();
	}

	protected override void Interact()
	{
		if (_open || _calibrated)
			return;

		var overlay = MinigameOverlay.Open(this, NearbyPlayer, MinigameScene, "SIGNAL CALIBRATION",
			"Match the target waveform with the three knobs.  1/2/3 selects, arrows adjust.",
			"Calibrated!");
		if (overlay == null)
			return;

		_open = true;
		HidePrompt();
		overlay.Finished += solved =>
		{
			_open = false;
			if (solved)
				Calibrate();
			else
				ShowPrompt();
		};
	}

	private void Calibrate()
	{
		_calibrated = true;
		Enabled = false;
		HidePrompt();
		_sprite.Stop();
		_sprite.Modulate = new Color(0.72f, 1f, 0.8f);
		MissionState.Instance?.MarkSignalTableRepaired();
	}

	private void ApplyState()
	{
		Enabled = !_calibrated;
		if (_calibrated)
		{
			HidePrompt();
			_sprite.Stop();
			_sprite.Modulate = new Color(0.72f, 1f, 0.8f);
			return;
		}

		_sprite.Play("default");
	}
}

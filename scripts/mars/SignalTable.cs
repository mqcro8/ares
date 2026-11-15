namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;

/// <summary>
/// Large signal table in the third building. Opens the waveform/frequency
/// calibration mini-game once; the completed state is stored in MissionState.
/// Without a frequency analyzer the first successful run must be repeated.
/// </summary>
public partial class SignalTable : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/FrequencyBoxMinigame.tscn";

	[Export] public DialogueSequence RetryDialogue { get; set; }

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _calibrated;

	public override void _Ready()
	{
		base._Ready();
		RetryDialogue ??= GD.Load<DialogueSequence>("res://data/dialogue/mars_retry_frequency.tres");
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_calibrated = MissionState.Instance?.SignalTableRepaired ?? false;
		ApplyState();
	}

	protected override void Interact() => BeginRepair(NearbyPlayer);

	/// <summary>Starts the in-world calibration repair for the given player.</summary>
	public void BeginRepair(Node2D player)
	{
		if (_open || _calibrated)
			return;

		_open = true;
		HidePrompt();
		MarsRepairFlow.Begin(this, player,
			MarsRepairTask.Frequency, ToolEffects.FrequencyAnalyzer,
			MinigameScene, "EARTH UPLINK CALIBRATION",
			"Match the target waveform with the three knobs.  1/2/3 selects, arrows adjust.",
			"Calibrated!", RetryDialogue,
			onSolved: () =>
			{
				_open = false;
				Calibrate();
			},
			onClosed: () =>
			{
				_open = false;
				if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Frequency) == true)
					PromptText = "Press E to retry signal calibration";
				ShowPrompt();
			});
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

		if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Frequency) == true)
			PromptText = "Press E to retry signal calibration";
		_sprite.Play("default");
	}
}

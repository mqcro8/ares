namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;

/// <summary>
/// Generator the player repairs through the fuse-box mini-game.
/// Solving it switches the generator from the idle frame to the looping on frames.
/// Without spare fuses the first successful run must be repeated.
/// </summary>
public partial class Generator : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string RepairMinigame { get; set; } = "res://scenes/minigames/FuseBoxMinigame.tscn";

	[Export] public DialogueSequence RetryDialogue { get; set; }

	private AnimatedSprite2D _sprite;
	private bool _on;
	private bool _repairing;

	public override void _Ready()
	{
		base._Ready();
		RetryDialogue ??= GD.Load<DialogueSequence>("res://data/dialogue/mars_retry_generator.tres");
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_on = MissionState.Instance?.GeneratorRepaired ?? false;
		Enabled = !_on;
		if (_on)
			HidePrompt();
		else if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Generator) == true)
			PromptText = "Press E to retry generator repair";
		_sprite.Play(_on ? "on" : "off");
	}

	protected override void Interact() => BeginRepair(NearbyPlayer);

	/// <summary>Starts the in-world repair flow for the given player.</summary>
	public void BeginRepair(Node2D player)
	{
		if (_on || _repairing)
			return;

		_repairing = true;
		HidePrompt();
		MarsRepairFlow.Begin(this, player,
			MarsRepairTask.Generator, ToolEffects.SpareFuses,
			RepairMinigame, "GENERATOR REPAIR",
			"Turn all six switches ON within four presses.  Keys 1-6 or click.",
			"Repaired!", RetryDialogue,
			onSolved: () =>
			{
				_repairing = false;
				PowerOn();
			},
			onClosed: () =>
			{
				_repairing = false;
				if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Generator) == true)
					PromptText = "Press E to retry generator repair";
				ShowPrompt();
			});
	}

	private void PowerOn()
	{
		_on = true;
		Enabled = false;
		HidePrompt();
		_sprite.Play("on");
		MissionState.Instance?.MarkGeneratorRepaired();
	}
}

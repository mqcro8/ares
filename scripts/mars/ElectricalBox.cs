namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;

/// <summary>Electrical box that opens the wire minigame in place.</summary>
public partial class ElectricalBox : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/wireminigame.tscn";

	[Export] public DialogueSequence RetryDialogue { get; set; }

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _solved;

	public override void _Ready()
	{
		base._Ready();
		RetryDialogue ??= GD.Load<DialogueSequence>("res://data/dialogue/mars_retry_wires.tres");
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_solved = MissionState.Instance?.WireBoxRepaired ?? false;
		_sprite.Play(_solved ? "open" : "closed");
		if (_solved)
		{
			Enabled = false;
			HidePrompt();
		}
		else if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Wires) == true)
			PromptText = "Press E to retry wiring repair";
	}

	protected override void Interact() => BeginRepair(NearbyPlayer);

	/// <summary>Starts the in-world wiring repair for the given player.</summary>
	public void BeginRepair(Node2D player)
	{
		if (_open || _solved)
			return;

		_open = true;
		HidePrompt();
		MarsRepairFlow.Begin(this, player,
			MarsRepairTask.Wires, ToolEffects.WirePliers,
			MinigameScene, "ELECTRICAL BOX",
			"Drag matching colors from left to right.  Keyboard: 1-4, Up/Down, Enter.",
			"Repaired!", RetryDialogue,
			onSolved: () =>
			{
				_open = false;
				_solved = true;
				Enabled = false;
				_sprite.Play("open");
				HidePrompt();
				MissionState.Instance?.MarkWireBoxRepaired();
			},
			onClosed: () =>
			{
				_open = false;
				if (MissionState.Instance?.MarsRepairNeedsRetry(MarsRepairTask.Wires) == true)
					PromptText = "Press E to retry wiring repair";
				ShowPrompt();
			});
	}
}

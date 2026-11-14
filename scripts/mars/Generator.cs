namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;

/// <summary>
/// Generator the player repairs through the fuse-box mini-game.
/// Solving it switches the generator from the idle frame to the looping on frames.
/// </summary>
public partial class Generator : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string RepairMinigame { get; set; } = "res://scenes/minigames/FuseBoxMinigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _on;
	private bool _repairing;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_on = MissionState.Instance?.GeneratorRepaired ?? false;
		Enabled = !_on;
		if (_on)
			HidePrompt();
		_sprite.Play(_on ? "on" : "off");
	}

	protected override void Interact()
	{
		if (_on || _repairing)
			return;

		var overlay = MinigameOverlay.Open(this, NearbyPlayer, RepairMinigame, "GENERATOR REPAIR",
			"Turn all six switches ON within four presses.  Keys 1-6 or click.");
		if (overlay == null)
			return;

		_repairing = true;
		HidePrompt();
		overlay.Finished += solved =>
		{
			_repairing = false;
			if (solved)
				PowerOn();
			else
				ShowPrompt();
		};
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

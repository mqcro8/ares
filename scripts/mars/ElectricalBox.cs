namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;

/// <summary>Electrical box that opens the wire minigame in place.</summary>
public partial class ElectricalBox : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/wireminigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _solved;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_solved = MissionState.Instance?.WireBoxRepaired ?? false;
		_sprite.Play(_solved ? "open" : "closed");
	}

	protected override void Interact()
	{
		if (_open)
			return;

		var overlay = MinigameOverlay.Open(this, NearbyPlayer, MinigameScene, "ELECTRICAL BOX",
			"Drag matching colors from left to right.  Keyboard: 1-4, Up/Down, Enter.");
		if (overlay == null)
			return;

		_open = true;
		HidePrompt();
		overlay.Finished += solved =>
		{
			_open = false;
			if (solved)
			{
				_solved = true;
				_sprite.Play("open");
				MissionState.Instance?.MarkWireBoxRepaired();
			}
			ShowPrompt();
		};
	}
}

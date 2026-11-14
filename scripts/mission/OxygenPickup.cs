namespace ares.scripts.mission;

using Godot;
using ares.scripts.player;

/// <summary>World pickup that refills the suit reserve when the astronaut presses E nearby.</summary>
public partial class OxygenPickup : Area2D
{
	/// <summary>Seconds of oxygen carried by this canister. Ignored when refilling to full.</summary>
	[Export(PropertyHint.Range, "0,600,1")] public float Seconds { get; set; } = 60f;
	/// <summary>Refills the reserve completely instead of adding a fixed amount.</summary>
	[Export] public bool RefillToFull { get; set; }
	[Export] public string PickupName { get; set; } = "OXYGEN CANISTER";
	[Export] public Sprite2D Sprite { get; set; }
	/// <summary>Distance at which the astronaut can reach the pickup.</summary>
	[Export(PropertyHint.Range, "8,160,1")] public float InteractionRadius { get; set; } = 44f;

	public bool PlayerNearby { get; private set; }
	public bool Collected { get; private set; }
	public string LastFeedback { get; private set; } = "";

	private Label _prompt;
	private Player _player;

	public override void _Ready()
	{
		if (Sprite == null) Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		_prompt = new Label { Name = "Prompt", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_prompt.AddThemeFontSizeOverride("font_size", 9);
		_prompt.AddThemeColorOverride("font_color", new Color(0.46f, 0.89f, 0.96f));
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		_prompt.AddThemeConstantOverride("outline_size", 3);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) _prompt.AddThemeFontOverride("font", font);
		AddChild(_prompt);
	}

	public override void _PhysicsProcess(double delta)
	{
		_player ??= FindPlayer();
		if (_player == null || !GodotObject.IsInstanceValid(_player)) return;
		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
		bool nearby = distance <= InteractionRadius && !Collected;
		if (nearby == PlayerNearby) return;
		PlayerNearby = nearby;
		UpdatePrompt();
	}

	private Player FindPlayer()
	{
		var found = GetTree().GetFirstNodeInGroup("player");
		if (found is Player grouped) return grouped;
		for (Node node = GetParent(); node != null; node = node.GetParent())
			if (node.GetNodeOrNull<Player>("Player") is Player child) return child;
		return null;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (Collected || !PlayerNearby) return;
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.PhysicalKeycode != Key.E && key.Keycode != Key.E) return;
		GetViewport().SetInputAsHandled();
		MissionState.Instance?.SetOxygenMessage(Collect());
	}

	/// <summary>Tries to take the oxygen. Returns the feedback message for the HUD.</summary>
	public string Collect()
	{
		if (Collected) return "ALREADY COLLECTED.";
		var mission = MissionState.Instance;
		if (mission == null) return "OXYGEN SYSTEM OFFLINE.";
		if (RefillToFull)
		{
			if (!mission.TryRefillOxygen(out string refillError))
			{
				LastFeedback = refillError;
				return refillError;
			}
			LastFeedback = "OXYGEN REFILLED.";
			return LastFeedback;
		}

		if (!mission.TryCollectOxygenCanister(Seconds, out string error, out double added))
		{
			LastFeedback = error;
			return error;
		}
		LastFeedback = $"OXYGEN +{added:F0}s";
		Finish();
		return LastFeedback;
	}

	private void Finish()
	{
		Collected = true;
		if (Sprite != null) Sprite.Visible = false;
		if (_prompt != null) _prompt.Visible = false;
	}

	private void OnBodyEntered(Node body)
	{
		if (body is not Player) return;
		PlayerNearby = true;
		UpdatePrompt();
	}

	private void OnBodyExited(Node body)
	{
		if (body is not Player) return;
		PlayerNearby = false;
		UpdatePrompt();
	}

	private void UpdatePrompt()
	{
		if (_prompt == null) return;
		_prompt.Visible = PlayerNearby && !Collected;
		if (!_prompt.Visible) return;
		_prompt.Text = $"E — {PickupName}";
		_prompt.Position = new Vector2(-48, -34);
	}
}

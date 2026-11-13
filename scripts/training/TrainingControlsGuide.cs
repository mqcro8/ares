namespace ares.scripts.training;

using Godot;
using ares.scripts.player;

/// <summary>Shows short world-following controls only when a new action is nearby.</summary>
public partial class TrainingControlsGuide : CanvasLayer
{
	public Player Player { get; set; }
	public TrainingIntro Intro { get; set; }
	public PhysicalTraining Course { get; set; }
	public TrainingStations Stations { get; set; }
	public string CurrentHint { get; private set; } = "";
	public string CurrentKeys { get; private set; } = "";
	public string CurrentMessage { get; private set; } = "";

	private enum HintKind { None, Move, Jump, Rest, Interact }
	private PanelContainer _bubble;
	private HBoxContainer _row;
	private HBoxContainer _keyHost;
	private Label _message;
	private FontFile _font;
	private HintKind _shownKind;
	private string _shownMessage = "";
	private bool _movementLearned;
	private bool _jumpLearned;
	private float _distanceMoved;
	private Vector2 _lastPlayerPosition;

	public override void _Ready()
	{
		Layer = 9;
		_font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		_lastPlayerPosition = Player.GlobalPosition;
		CreatePrompt();
	}

	public override void _Process(double delta)
	{
		if (!GodotObject.IsInstanceValid(Player) || !GodotObject.IsInstanceValid(Stations))
		{
			HidePrompt();
			return;
		}

		if (Stations.InteractionLocked || (Intro?.BriefingActive ?? false))
		{
			_lastPlayerPosition = Player.GlobalPosition;
			HidePrompt();
			return;
		}

		Vector2 playerPosition = Player.GlobalPosition;
		bool gym = Intro != null && Course != null && Player.TrainingMovement != null;
		if (gym) _distanceMoved += playerPosition.DistanceTo(_lastPlayerPosition);
		_lastPlayerPosition = playerPosition;
		if (gym && (Course.ClearedCount > 0 || _distanceMoved >= 40))
			_movementLearned = true;

		if (Stations?.HasContextTarget == true)
		{
			string prompt = Stations.ContextPrompt;
			if (prompt.StartsWith("E — ")) prompt = prompt.Substring(4);
			ShowPrompt(HintKind.Interact, "E", prompt, Stations.ContextPosition + Vector2.Up * 20);
			return;
		}

		if (!gym)
		{
			HidePrompt();
			return;
		}

		var movement = Player.TrainingMovement;
		if (!movement.Grounded && movement.Height > 0 && IsNearHurdle(playerPosition, 112, out _))
			_jumpLearned = true;

		if (!_movementLearned)
		{
			ShowPrompt(HintKind.Move, "WASD", "MOVE AROUND", playerPosition + Vector2.Up * 20);
			return;
		}

		if (movement != null && movement.Grounded && movement.Stamina < 20)
		{
			ShowPrompt(HintKind.Rest, "REST", "STAND STILL TO RECOVER", playerPosition + Vector2.Up * 20);
			return;
		}

		if (!_jumpLearned && movement != null && movement.Grounded &&
			IsNearHurdle(playerPosition, 112, out Vector2 hurdleCenter))
		{
			ShowPrompt(HintKind.Jump, "SPACE", "JUMP OVER THE HURDLE", hurdleCenter + Vector2.Up * 44);
			return;
		}

		HidePrompt();
	}

	private void CreatePrompt()
	{
		_bubble = new PanelContainer
		{
			Name = "ContextControlHint",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_bubble.AddThemeStyleboxOverride("panel", MakeStyle(new Color("101c2a"), new Color("75e4ff"), 1));
		AddChild(_bubble);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 6);
		_bubble.AddChild(margin);

		_row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_row.AddThemeConstantOverride("separation", 7);
		margin.AddChild(_row);

		_keyHost = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_row.AddChild(_keyHost);
		_message = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(115, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		_message.AddThemeColorOverride("font_color", Colors.White);
		_message.AddThemeFontSizeOverride("font_size", 8);
		if (_font != null) _message.AddThemeFontOverride("font", _font);
		_row.AddChild(_message);
	}

	private void ShowPrompt(HintKind kind, string keys, string message, Vector2 worldAnchor)
	{
		if (_shownKind != kind || _shownMessage != message)
		{
			RebuildKeys(kind, keys);
			_message.Text = message;
			_shownKind = kind;
			_shownMessage = message;
		}

		CurrentHint = kind.ToString().ToUpperInvariant();
		CurrentKeys = keys;
		CurrentMessage = message;
		_bubble.Visible = true;
		_bubble.Size = _bubble.GetCombinedMinimumSize();

		Vector2 screenAnchor = GetViewport().GetCanvasTransform() * worldAnchor;
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 position = screenAnchor - new Vector2(_bubble.Size.X / 2, _bubble.Size.Y + 8);
		position.X = Mathf.Clamp(position.X, 8, Mathf.Max(8, viewportSize.X - _bubble.Size.X - 8));
		position.Y = Mathf.Clamp(position.Y, 8, Mathf.Max(8, viewportSize.Y - _bubble.Size.Y - 8));
		_bubble.Position = position;
	}

	private void RebuildKeys(HintKind kind, string keys)
	{
		foreach (Node child in _keyHost.GetChildren())
		{
			_keyHost.RemoveChild(child);
			child.QueueFree();
		}

		if (kind == HintKind.Move)
		{
			var pad = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			pad.AddThemeConstantOverride("separation", 1);
			_keyHost.AddChild(pad);
			var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			pad.AddChild(top);
			top.AddChild(MakeKeycap("W", 18));
			var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			bottom.AddThemeConstantOverride("separation", 2);
			pad.AddChild(bottom);
			foreach (string key in new[] { "A", "S", "D" }) bottom.AddChild(MakeKeycap(key, 18));
			return;
		}

		var holder = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_keyHost.AddChild(holder);
		holder.AddChild(MakeKeycap(keys, keys == "SPACE" ? 48 : 34));
	}

	private PanelContainer MakeKeycap(string text, float width)
	{
		var cap = new PanelContainer
		{
			CustomMinimumSize = new Vector2(width, 19),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cap.AddThemeStyleboxOverride("panel", MakeStyle(new Color("172b3a"), new Color("f7c30c"), 1));
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", new Color("f7c30c"));
		label.AddThemeFontSizeOverride("font_size", 7);
		if (_font != null) label.AddThemeFontOverride("font", _font);
		cap.AddChild(label);
		return cap;
	}

	private static StyleBoxFlat MakeStyle(Color background, Color border, int borderWidth) => new()
	{
		BgColor = background,
		BorderColor = border,
		BorderWidthLeft = borderWidth,
		BorderWidthRight = borderWidth,
		BorderWidthTop = borderWidth,
		BorderWidthBottom = borderWidth,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerRadiusBottomRight = 3
	};

	private bool IsNearHurdle(Vector2 position, float distanceLimit, out Vector2 center)
	{
		center = Vector2.Zero;
		float nearest = float.MaxValue;
		foreach (CollisionShape2D block in Course.Blocks)
		{
			Rect2 rect = PhysicalTraining.WorldRect(block);
			Vector2 closest = new(
				Mathf.Clamp(position.X, rect.Position.X, rect.End.X),
				Mathf.Clamp(position.Y, rect.Position.Y, rect.End.Y));
			float distance = position.DistanceTo(closest);
			if (distance >= nearest) continue;
			nearest = distance;
			center = rect.GetCenter();
		}
		return nearest <= distanceLimit;
	}

	private void HidePrompt()
	{
		if (_bubble != null) _bubble.Visible = false;
		CurrentHint = "";
		CurrentKeys = "";
		CurrentMessage = "";
	}
}

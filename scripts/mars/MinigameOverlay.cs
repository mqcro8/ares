namespace ares.scripts.mars;

using Godot;
using System;

/// <summary>
/// Reusable full-screen mini-game overlay. Freezes the player, runs the mini-game,
/// then releases the player in the exact same spot without unloading the building.
/// </summary>
public partial class MinigameOverlay : CanvasLayer
{
	/// <summary>True when the player solved the mini-game, false when they backed out.</summary>
	public event Action<bool> Finished;

	private Control _game;
	private Node2D _player;
	private Label _title;
	private bool _closed;
	private bool _solved;
	private int _session;

	public static MinigameOverlay Open(Node owner, Node2D player, string scenePath, string title, string hint)
	{
		var packed = GD.Load<PackedScene>(scenePath);
		if (packed == null)
		{
			GD.PushError($"MinigameOverlay: could not load {scenePath}");
			return null;
		}

		var overlay = new MinigameOverlay { Layer = 50 };
		owner.AddChild(overlay);
		overlay.Build(packed, player, title, hint);
		return overlay;
	}

	private void Build(PackedScene packed, Node2D player, string title, string hint)
	{
		_player = player;
		_player?.SetPhysicsProcess(false);

		_game = packed.Instantiate<Control>();
		if (_game is WireMinigame wire)
		{
			wire.AttachPracticeUi = false;
			wire.Completed += OnCompleted;
		}
		else if (_game is FuseBoxMinigame fuse)
		{
			fuse.AttachPracticeUi = false;
			fuse.Completed += OnCompleted;
		}

		AddChild(_game);
		_game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		if (_game is WireMinigame && _game.GetNodeOrNull<Panel>("background") is { } background)
			background.Visible = true;

		_title = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_title.AddThemeFontSizeOverride("font_size", 22);
		_title.AddThemeColorOverride("font_outline_color", Colors.Black);
		_title.AddThemeConstantOverride("outline_size", 6);
		AddChild(_title);
		_title.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_title.OffsetLeft = -300;
		_title.OffsetRight = 300;
		_title.OffsetTop = 18;
		_title.OffsetBottom = 52;

		var hintLabel = new Label
		{
			Text = hint,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hintLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		hintLabel.AddThemeConstantOverride("outline_size", 4);
		AddChild(hintLabel);
		hintLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		hintLabel.OffsetLeft = -360;
		hintLabel.OffsetRight = 360;
		hintLabel.OffsetTop = 56;
		hintLabel.OffsetBottom = 92;

		var back = new Button { Text = "Back (Esc)" };
		back.Pressed += () => Close(false);
		AddChild(back);
		back.Position = new Vector2(16, 16);

		GetViewport().SizeChanged += FitLayout;
		FitLayout();
	}

	// Keep both mini-games usable on smaller windows.
	private void FitLayout()
	{
		if (!IsInstanceValid(_game))
			return;

		Vector2 size = GetViewport().GetVisibleRect().Size;
		if (_game is WireMinigame wire)
		{
			var left = wire.GetNode<Control>("LeftTerminals");
			var right = wire.GetNode<Control>("RightTerminals");
			left.OffsetLeft = size.X * 0.15f;
			left.OffsetRight = left.OffsetLeft + 125;
			right.OffsetRight = size.X * 0.85f;
			right.OffsetLeft = right.OffsetRight - 125;
			wire.RefreshWireLayout();
		}
		else if (_game is FuseBoxMinigame)
		{
			var board = _game.GetNode<Control>("Box");
			float scale = Mathf.Min(1, Mathf.Min((size.Y - 140) / 500, (size.X - 32) / 360));
			board.Scale = Vector2.One * scale;
			board.Position = new Vector2((size.X - 360 * scale) / 2, 120 + (size.Y - 140 - 500 * scale) / 2);
		}
	}

	private async void OnCompleted()
	{
		if (_closed || _solved)
			return;

		_solved = true;
		if (IsInstanceValid(_game))
			_game.SetProcessInput(false);
		if (_title != null)
			_title.Text = "Repaired!";

		int session = _session;
		await ToSignal(GetTree().CreateTimer(0.9f), SceneTreeTimer.SignalName.Timeout);
		if (session == _session)
			Close(true);
	}

	private void Close(bool solved)
	{
		if (_closed)
			return;

		_closed = true;
		_session++;
		GetViewport().SizeChanged -= FitLayout;

		if (IsInstanceValid(_game))
		{
			if (_game is WireMinigame wire)
				wire.Completed -= OnCompleted;
			else if (_game is FuseBoxMinigame fuse)
				fuse.Completed -= OnCompleted;
			_game.SetProcessInput(false);
			_game = null;
		}

		if (IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
		_player = null;

		Finished?.Invoke(solved);
		QueueFree();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_closed)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			GetViewport().SetInputAsHandled();
			Close(false);
		}
	}

	public override void _ExitTree()
	{
		// Scene left while the overlay was open: do not leave the player frozen.
		if (!_closed && IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
	}
}

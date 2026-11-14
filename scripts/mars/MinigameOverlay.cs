namespace ares.scripts.mars;

using Godot;
using System;
using ares.scripts.training;
using Ares.Systems.Controllers;

/// <summary>
/// Reusable full-screen mini-game overlay. Freezes the player, runs the mini-game,
/// then releases the player in the exact same spot without unloading the building.
/// </summary>
public partial class MinigameOverlay : CanvasLayer, IControllerCommandSink
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
		AddChild(_game);
		_game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		// Mini-game scenes now always attach the training PracticeReturn wrapper.
		// An in-world overlay runs the board directly instead: drop that wrapper,
		// re-enable the board, and republish the controller context it cleared.
		StripTrainingWrapper();
		SetGameActive(true);
		BeginControllerContext();

		switch (_game)
		{
			case WireMinigame wire:
				wire.Completed += OnCompleted;
				break;
			case FuseBoxMinigame fuse:
				fuse.Completed += OnCompleted;
				break;
			case FrequencyBoxMinigame frequency:
				frequency.Completed += OnCompleted;
				break;
			case HullRepairMinigame hull:
				hull.Completed += OnCompleted;
				break;
		}

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

		ControllerCommandRouter.Register(this, this);
		GetViewport().SizeChanged += FitLayout;
		FitLayout();
	}

	/// <summary>Removes the automatically attached training instructions layer.</summary>
	private void StripTrainingWrapper()
	{
		var practice = _game.GetNodeOrNull<PracticeReturn>("PracticeReturn");
		if (practice == null)
			return;

		// RemoveChild runs PracticeReturn._ExitTree, which disables the board again.
		_game.RemoveChild(practice);
		practice.QueueFree();
	}

	private void SetGameActive(bool active)
	{
		if (!IsInstanceValid(_game))
			return;

		_game.SetProcess(active);
		_game.SetPhysicsProcess(active);
		_game.SetProcessInput(active);
	}

	private void BeginControllerContext()
	{
		ControllerContextMode mode = _game switch
		{
			WireMinigame => ControllerContextMode.Wires,
			FuseBoxMinigame => ControllerContextMode.Fuses,
			FrequencyBoxMinigame => ControllerContextMode.Frequency,
			HullRepairMinigame => ControllerContextMode.Hull,
			_ => ControllerContextMode.None
		};

		ControllerContext.Begin(_game, mode);
		(_game as IControllerContextSource)?.PublishControllerContext();
	}

	// Keep every board usable on smaller windows, mirroring PracticeReturn's layout.
	private void FitLayout()
	{
		if (!IsInstanceValid(_game))
			return;

		Vector2 size = GetViewport().GetVisibleRect().Size;
		if (_game is WireMinigame wire)
		{
			var left = wire.GetNode<Control>("LeftTerminals");
			var right = wire.GetNode<Control>("RightTerminals");
			float half = Mathf.Max(80f, Mathf.Min(300f, size.X * 0.5f - 24f));
			left.OffsetLeft = -half + 20;
			left.OffsetRight = -half + 80;
			right.OffsetLeft = half - 80;
			right.OffsetRight = half - 20;
			var leftPlate = wire.GetNodeOrNull<Control>("LeftPlate");
			if (leftPlate != null) { leftPlate.OffsetLeft = -half; leftPlate.OffsetRight = -half + 100; }
			var rightPlate = wire.GetNodeOrNull<Control>("RightPlate");
			if (rightPlate != null) { rightPlate.OffsetLeft = half - 100; rightPlate.OffsetRight = half; }
			wire.RefreshWireLayout();
		}
		else if (_game is FuseBoxMinigame)
		{
			var board = _game.GetNode<Control>("Box");
			float scale = Mathf.Min(1, Mathf.Min((size.Y - 140) / 500, (size.X - 32) / 360));
			board.Scale = Vector2.One * scale;
			board.Position = new Vector2((size.X - 360 * scale) / 2, 120 + (size.Y - 140 - 500 * scale) / 2);
		}
		else if (_game is FrequencyBoxMinigame)
		{
			FitCentered(_game.GetNode<Control>("Box"), new Vector2(320, 520), size, 32);
		}
		else if (_game is HullRepairMinigame)
		{
			FitCentered(_game.GetNode<Control>("Frame"), new Vector2(720, 460), size, 32);
		}
	}

	private static void FitCentered(Control control, Vector2 nativeSize, Vector2 viewport, float padding)
	{
		float scale = Mathf.Min(1f,
			Mathf.Min((viewport.X - padding) / nativeSize.X, (viewport.Y - padding) / nativeSize.Y));
		control.PivotOffset = nativeSize / 2;
		control.Scale = Vector2.One * Mathf.Max(0.35f, scale);
	}

	private async void OnCompleted()
	{
		if (_closed || _solved)
			return;

		_solved = true;
		SetGameActive(false);
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
		ControllerCommandRouter.Unregister(this);

		if (IsInstanceValid(_game))
		{
			switch (_game)
			{
				case WireMinigame wire:
					wire.Completed -= OnCompleted;
					break;
				case FuseBoxMinigame fuse:
					fuse.Completed -= OnCompleted;
					break;
				case FrequencyBoxMinigame frequency:
					frequency.Completed -= OnCompleted;
					break;
				case HullRepairMinigame hull:
					hull.Completed -= OnCompleted;
					break;
			}

			SetGameActive(false);
			ControllerContext.End(_game);
			_game = null;
		}

		if (IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
		_player = null;

		Finished?.Invoke(solved);
		QueueFree();
	}

	public bool HandleControllerCommand(ControllerCommand command)
	{
		switch (command.Name)
		{
			case ControllerCommands.MinigameCancel:
				Close(false);
				return true;
			case ControllerCommands.MinigameStart:
				// The in-world board is already running.
				return true;
			default:
				return false;
		}
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
		ControllerCommandRouter.Unregister(this);

		// Scene left while the overlay was open: do not leave the player frozen.
		if (!_closed)
		{
			if (IsInstanceValid(_player))
				_player.SetPhysicsProcess(true);
			if (IsInstanceValid(_game))
				ControllerContext.End(_game);
		}
	}
}

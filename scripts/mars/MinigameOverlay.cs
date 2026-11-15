namespace ares.scripts.mars;

using Godot;
using System;
using ares.scripts.dialogue;
using ares.scripts.training;
using Ares.Systems.Controllers;

/// <summary>
/// Reusable full-screen mini-game overlay. Freezes the player, runs the mini-game,
/// then releases the player in the exact same spot without unloading the building.
/// A board can also request one automatic retry after a successful run, which keeps
/// the overlay open while its dialogue is shown.
/// </summary>
public partial class MinigameOverlay : CanvasLayer, IControllerCommandSink
{
	/// <summary>A successful run that must be followed by another attempt.</summary>
	public sealed record RetryPlan(DialogueSequence Dialogue);

	/// <summary>True when the player solved the mini-game, false when they backed out.</summary>
	public event Action<bool> Finished;

	private PackedScene _packed;
	private Control _game;
	private Node2D _player;
	private Label _title;
	private Label _hintLabel;
	private Button _back;
	private string _titleText;
	private string _hintText;
	private string _completedTitle;
	private ColorRect _retryShade;
	private DialogueView _retryDialogue;
	private bool _retrying;
	private bool _closed;
	private bool _solved;
	private bool _exiting;
	private int _session;

	/// <summary>Called after a successful run. Returning a plan restarts the board instead of completing.</summary>
	public Func<RetryPlan> RetryAfterSolve { get; set; }

	/// <summary>How long the retry dialogue stays up before the fresh board starts.</summary>
	public float RetryMessageSeconds { get; set; } = 4f;

	/// <summary>Test/telemetry hook for the currently running board.</summary>
	public Control CurrentGame => _game;

	/// <summary>True while the retry dialogue is covering an inactive board.</summary>
	public bool IsRetryDialogueVisible => _retrying && _retryShade?.Visible == true;

	public static MinigameOverlay Open(Node owner, Node2D player, string scenePath, string title, string hint,
		string completedTitle = "Repaired!")
	{
		var packed = GD.Load<PackedScene>(scenePath);
		if (packed == null)
		{
			GD.PushError($"MinigameOverlay: could not load {scenePath}");
			return null;
		}

		var overlay = new MinigameOverlay { Layer = 50, Name = "MinigameOverlay" };
		owner.AddChild(overlay);
		overlay.Build(packed, player, title, hint, completedTitle);
		return overlay;
	}

	private void Build(PackedScene packed, Node2D player, string title, string hint, string completedTitle)
	{
		_packed = packed;
		_player = player;
		_titleText = title;
		_hintText = hint;
		_completedTitle = completedTitle;
		_player?.SetPhysicsProcess(false);

		SpawnBoard();

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

		_hintLabel = new Label
		{
			Text = hint,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_hintLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_hintLabel.AddThemeConstantOverride("outline_size", 4);
		AddChild(_hintLabel);
		_hintLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_hintLabel.OffsetLeft = -360;
		_hintLabel.OffsetRight = 360;
		_hintLabel.OffsetTop = 56;
		_hintLabel.OffsetBottom = 92;

		_back = new Button { Text = "Back (Esc)" };
		_back.Pressed += () => Close(false);
		AddChild(_back);
		_back.Position = new Vector2(16, 16);

		ControllerCommandRouter.Register(this, this);
		GetViewport().SizeChanged += FitLayout;
		FitLayout();
	}

	/// <summary>Instantiates the board and publishes its controller context.</summary>
	private void SpawnBoard()
	{
		_game = _packed.Instantiate<Control>();
		AddChild(_game);
		_game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		// Mini-game scenes now always attach the training PracticeReturn wrapper.
		// An in-world overlay runs the board directly instead: drop that wrapper,
		// re-enable the board, and republish the controller context it cleared.
		StripTrainingWrapper();
		SetGameActive(true);
		BeginControllerContext();
		HookCompleted(_game);
	}

	private void HookCompleted(Control game)
	{
		switch (game)
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
	}

	private void UnhookCompleted(Control game)
	{
		switch (game)
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
		if (_closed || _exiting || _solved)
			return;

		_solved = true;
		SetGameActive(false);
		if (_back != null)
			_back.Disabled = true;

		RetryPlan retry = RetryAfterSolve?.Invoke();
		if (retry?.Dialogue != null && retry.Dialogue.Lines.Count > 0)
		{
			int retrySession = _session;
			ShowRetryDialogue(retry.Dialogue);
			await ToSignal(GetTree().CreateTimer(Mathf.Max(0.05f, RetryMessageSeconds)),
				SceneTreeTimer.SignalName.Timeout);
			if (retrySession == _session && !_closed && !_exiting)
				RestartGame();
			return;
		}

		if (_title != null)
			_title.Text = _completedTitle;

		int session = _session;
		await ToSignal(GetTree().CreateTimer(0.9f), SceneTreeTimer.SignalName.Timeout);
		if (session == _session && !_closed && !_exiting)
			Close(true);
	}

	/// <summary>Replaces the finished board with a fresh randomized one.</summary>
	private void RestartGame()
	{
		if (_closed || _exiting)
			return;

		HideRetryDialogue(true);
		DetachGame();
		_session++;

		if (_title != null)
			_title.Text = _titleText;
		if (_hintLabel != null)
			_hintLabel.Text = _hintText;
		if (_back != null)
			_back.Disabled = false;

		SpawnBoard();
		_solved = false;
		FitLayout();
	}

	private void ShowRetryDialogue(DialogueSequence sequence)
	{
		if (_retryShade == null)
		{
			_retryShade = new ColorRect
			{
				Name = "RetryShade",
				Color = new Color(0.01f, 0.015f, 0.025f, 0.55f),
				MouseFilter = Control.MouseFilterEnum.Stop,
				Visible = false,
			};
			_retryShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			AddChild(_retryShade);

			_retryDialogue = new DialogueView { Name = "RetryDialogue", Visible = false };
			AddChild(_retryDialogue);
		}

		_retryShade.Visible = true;
		_retryDialogue.ShowLine(sequence.Lines[0]);
		_retrying = true;
		if (IsInstanceValid(_game))
			ControllerContext.End(_game);
		if (_title != null) _title.Visible = false;
		if (_hintLabel != null) _hintLabel.Visible = false;
	}

	private void HideRetryDialogue(bool restoreChrome)
	{
		_retrying = false;
		if (_retryShade != null) _retryShade.Visible = false;
		_retryDialogue?.Clear();
		if (restoreChrome)
		{
			if (_title != null) _title.Visible = true;
			if (_hintLabel != null) _hintLabel.Visible = true;
		}
	}

	/// <summary>Detaches the old board so it cannot emit another completion.</summary>
	private void DetachGame()
	{
		if (!IsInstanceValid(_game))
		{
			_game = null;
			return;
		}

		UnhookCompleted(_game);
		SetGameActive(false);
		ControllerContext.End(_game);
		RemoveChild(_game);
		_game.QueueFree();
		_game = null;
	}

	private void Close(bool solved)
	{
		if (_closed)
			return;

		_closed = true;
		_session++;
		GetViewport().SizeChanged -= FitLayout;
		ControllerCommandRouter.Unregister(this);
		HideRetryDialogue(false);
		DetachGame();

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
				if (_solved && !_retrying)
					return true;
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
		if (_closed || (_solved && !_retrying))
			return;

		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;

		if (_retrying && key.Keycode is Key.E or Key.Space or Key.Enter)
		{
			GetViewport().SetInputAsHandled();
			RestartGame();
			return;
		}

		if (key.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			Close(false);
		}
	}

	public override void _ExitTree()
	{
		_exiting = true;
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

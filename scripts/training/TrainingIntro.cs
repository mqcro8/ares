namespace ares.scripts.training;

using Godot;

/// <summary>Scene-local briefing; owns only the player's temporary physics lock.</summary>
public partial class TrainingIntro : Node2D
{
	[Export] public string AstronautName { get; set; } = "Commander";
	[Export] public PhysicalTraining Course { get; set; }
	[Export] public NodePath PlayerPath { get; set; } = "Tilemaps_layers/Player";
	[Export] public NodePath PanelPath { get; set; } = "IntroUI/Panel";
	[Export] public NodePath SpeakerPath { get; set; } = "IntroUI/Panel/Margin/VBox/Speaker";
	[Export] public NodePath TextPath { get; set; } = "IntroUI/Panel/Margin/VBox/Text";
	[Export] public NodePath ContinuePath { get; set; } = "IntroUI/Panel/Margin/VBox/Continue";

	private CharacterBody2D _player;
	private PanelContainer _panel;
	private Label _speaker;
	private Label _text;
	private Button _continue;
	private bool _previousPhysicsProcessing;
	private bool _ownsPhysicsLock;
	private bool _active;
	private bool _completion;
	private bool _completionShown;
	private int _line;
	private ulong _lastAdvanceFrame = ulong.MaxValue;

	public override void _Ready()
	{
		_player = GetNodeOrNull<CharacterBody2D>(PlayerPath);
		_panel = GetNodeOrNull<PanelContainer>(PanelPath);
		_speaker = GetNodeOrNull<Label>(SpeakerPath);
		_text = GetNodeOrNull<Label>(TextPath);
		_continue = GetNodeOrNull<Button>(ContinuePath);
		if (_player == null || _panel == null || _speaker == null || _text == null || _continue == null)
		{
			GD.PushError("TrainingIntro: missing player or briefing UI; movement was not locked.");
			_panel?.Hide();
			return;
		}

		AcquirePhysicsLock();
		_continue.Pressed += Advance;
		if (Course != null) Course.Completed += QueueCompletion;
		_active = true;
		ShowLine();
		_panel.Show();
		_continue.GrabFocus();
	}

	private void QueueCompletion() => Callable.From(ShowCompletion).CallDeferred();

	public void ShowCompletion()
	{
		if (!IsInsideTree() || _active || _completionShown || _panel == null || _player == null) return;
		_completionShown = true;
		_completion = true;
		AcquirePhysicsLock();
		_active = true;
		_lastAdvanceFrame = Engine.GetProcessFrames();
		_speaker.Text = "MISSION CONTROL";
		_text.Text = $"Excellent work, {AstronautName}. You've cleared all three obstacles. Keep practicing at your own pace.";
		_continue.Text = "Continue";
		_panel.Show();
		_continue.GrabFocus();
	}

	private void ShowLine()
	{
		_speaker.Text = _line == 1 ? AstronautName.ToUpperInvariant() : "MISSION CONTROL";
		_text.Text = _line switch
		{
			0 => "Welcome, [Name]. Our Gateway has been silent for two weeks, and you're going to bring it back online. Before you head out, you need to master your movement, your equipment, and your ship."
				.Replace("[Name]", AstronautName),
			1 => "Understood. Let's get started.",
			_ => "Here, you'll learn how to move. These exercises are just practice, with no lasting consequences."
		};
		_continue.Text = _line == 2 ? "Start training" : "Continue";
	}

	private void Advance()
	{
		// The focused Button is the only input route: no parallel ui_accept handler.
		ulong frame = Engine.GetProcessFrames();
		if (!_active || frame == _lastAdvanceFrame)
			return;
		_lastAdvanceFrame = frame;
		_line++;
		if (!_completion && _line < 3)
		{
			ShowLine();
			return;
		}

		_active = false;
		_continue.ReleaseFocus();
		_panel.Hide();
		RestorePhysics();
	}

	private void AcquirePhysicsLock()
	{
		_previousPhysicsProcessing = _player.IsPhysicsProcessing();
		_ownsPhysicsLock = true;
		_player.Velocity = Vector2.Zero;
		_player.SetPhysicsProcess(false);
	}

	private void RestorePhysics()
	{
		if (!_ownsPhysicsLock)
			return;
		if (GodotObject.IsInstanceValid(_player))
		{
			if (_player is ares.scripts.player.Player player) player.TrainingMovement?.SuppressDialogueJump();
			_player.SetPhysicsProcess(_previousPhysicsProcessing);
		}
		_ownsPhysicsLock = false;
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(Course)) Course.Completed -= QueueCompletion;
		if (GodotObject.IsInstanceValid(_continue))
			_continue.Pressed -= Advance;
		_active = false;
		RestorePhysics();
	}
}

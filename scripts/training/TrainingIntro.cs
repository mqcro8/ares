namespace ares.scripts.training;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;

/// <summary>Scene-local briefing; owns only the player's temporary physics lock.</summary>
public partial class TrainingIntro : Node2D
{
	[Export] public string AstronautName { get; set; } = "Ares";
	[Export] public DialogueSequence Briefing { get; set; }
	[Export] public DialogueSequence CompletionNotice { get; set; }
	[Export] public PhysicalTraining Course { get; set; }
	[Export] public Marker2D LabReturnMarker { get; set; }
	[Export] public Control LabDoorZone { get; set; }
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
	private TrainingStations _stations;
	public bool BriefingActive => _active;
	public bool CompletionShown => _completionShown;

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

		if (Briefing == null)
			Briefing = GD.Load<DialogueSequence>("res://data/dialogue/training_intro.tres");
		if (CompletionNotice == null)
			CompletionNotice = GD.Load<DialogueSequence>("res://data/dialogue/training_completion.tres");
		_continue.Pressed += Advance;
		if (Course != null) Course.Completed += QueueCompletion;
		_stations = new TrainingStations { Name = "PracticeStations", Intro = this, Player = (ares.scripts.player.Player)_player };
		AddChild(_stations);
		_stations.Targets.Add(new("Enter repair lab", LabDoorZone, PracticeSession.LabScene, "lab.from_gym"));
		var arrival = PracticeSession.Arrive(PracticeSession.TrainingScene);
		if (arrival != null)
		{
			var saved = PracticeSession.Saved;
			_player.GlobalPosition = LabReturnMarker.GlobalPosition;
			PracticeSession.RestoreFacing(PracticeSession.TrainingScene, (ares.scripts.player.Player)_player);
			if (saved != null)
			{
				Course.Restore(saved.Course);
				((ares.scripts.player.Player)_player).TrainingMovement.Restore(saved.Movement);
				_completionShown = saved.CompletionShown || Course.ClearedCount >= 3;
				if (_completionShown)
				{
					MissionState.Instance.TrainingCourseComplete = true;
					MissionState.Instance.EvaluateTrainingCompletion();
					if (TrainingCompletionDialog.Open(this, (ares.scripts.player.Player)_player,
						() => _stations.InteractionLocked = false))
						_stations.InteractionLocked = true;
				}
				_line = 3;
				_panel.Hide();
				return;
			}
		}
		// A first visit from the lab has no gym snapshot, but still owns its repairs.
		if (arrival == null) PracticeSession.Clear();
		AcquirePhysicsLock();
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
		MissionState.Instance.TrainingCourseComplete = true;
		MissionState.Instance.EvaluateTrainingCompletion();
		if (!MissionState.Instance.TakeTrainingCompletionNotification()) return;
		_completion = true;
		AcquirePhysicsLock();
		_active = true;
		_lastAdvanceFrame = Engine.GetProcessFrames();
		DialogueLine line = null;
		if (CompletionNotice != null && CompletionNotice.Lines.Count > 0)
			line = CompletionNotice.Lines[0];
		_speaker.Text = SpeakerFor(line);
		_text.Text = line?.Text ?? "All training is complete.";
		_continue.Text = "Continue";
		_panel.Show();
		_continue.GrabFocus();
	}

	private void ShowLine()
	{
		DialogueLine line = null;
		if (Briefing != null && _line >= 0 && _line < Briefing.Lines.Count)
			line = Briefing.Lines[_line];
		_speaker.Text = SpeakerFor(line);
		_text.Text = (line?.Text ?? "").Replace("[Name]", AstronautName);
		_continue.Text = _line == 2 ? "Start training" : "Continue";
	}

	private string SpeakerFor(DialogueLine line)
	{
		if (line == null || string.IsNullOrEmpty(line.Speaker)) return "MISSION CONTROL";
		return line.Speaker == "ARES" ? AstronautName.ToUpperInvariant() : line.Speaker;
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

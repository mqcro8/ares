namespace ares.scripts.training;

using Ares.Systems.Audio;
using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;
using ares.scripts.ui.mission_monitor;

/// <summary>Scene-local briefing; owns only the player's temporary physics lock.</summary>
public partial class TrainingIntro : Node2D
{
	[Export] public DialogueSequence Briefing { get; set; }
	[Export] public DialogueSequence CompletionNotice { get; set; }
	[Export] public PhysicalTraining Course { get; set; }
	[Export] public MissionMonitorView Monitor { get; set; }
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
	private readonly TrainingPlayerLock _physicsLock = new();
	private bool _active;
	private bool _completion;
	private bool _completionShown;
	private int _line;
	private ulong _lastAdvanceFrame = ulong.MaxValue;
	private TrainingStations _stations;
	private TrainingControlsGuide _controlsGuide;
	public bool BriefingActive => _active;
	public bool CompletionShown => _completionShown;
	public TrainingStations Stations => _stations;
	public TrainingControlsGuide ControlsGuide => _controlsGuide;

	public override void _Process(double delta)
	{
		if (Monitor == null || Course == null || _player is not ares.scripts.player.Player player) return;
		var mission = MissionState.Instance;
		int progress = mission.TrainingCourseComplete ? 3 : Course.ClearedCount;
		Monitor.Render(MissionMonitorPresenter.CaptureTraining(mission,
			inLab: false,
			stamina: mission.Stamina,
			circuitProgress: progress,
			circuitComplete: mission.TrainingCourseComplete || Course.Congratulated,
			hasRepairKit: player.Inventory?.HasCapability(ItemCapability.Repair) == true,
			prompt: _stations?.MonitorPrompt,
			feedback: _stations?.MonitorFeedback,
			visible: !_active));
	}

	public override void _Ready()
	{
		AudioManager.StopMusic(1.0);
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
		_controlsGuide = new TrainingControlsGuide
		{
			Name = "ContextControlsGuide",
			Player = (ares.scripts.player.Player)_player,
			Intro = this,
			Course = Course,
			Stations = _stations
		};
		AddChild(_controlsGuide);
		var arrival = PracticeSession.Arrive(PracticeSession.TrainingScene);
		if (arrival != null)
		{
			var saved = PracticeSession.Saved;
			_player.GlobalPosition = LabReturnMarker.GlobalPosition;
			PracticeSession.RestoreFacing(PracticeSession.TrainingScene, (ares.scripts.player.Player)_player);
			if (saved != null)
			{
				Course.Restore(saved.Course);
				((ares.scripts.player.Player)_player).TrainingMovement.Restore(saved.Movement with
				{
					Stamina = MissionState.Instance.Stamina,
					Rest = MissionState.Instance.StaminaRestSeconds,
				});
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
		DialogueLine line = TrainingCompletionDialog.CompletionLine(CompletionNotice);
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
		_text.Text = line?.Text ?? "";
		_continue.Text = _line == 2 ? "Start training" : "Continue";
	}

	private static string SpeakerFor(DialogueLine line)
	{
		if (line == null || string.IsNullOrEmpty(line.Speaker)) return "MISSION CONTROL";
		return line.Speaker;
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
		_physicsLock.Acquire(_player);
	}

	private void RestorePhysics()
	{
		_physicsLock.Release();
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

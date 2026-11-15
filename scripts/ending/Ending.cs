namespace ares.scripts.ending;

using Godot;
using System;
using System.Linq;
using Ares.Systems.Controllers;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems;
using Ares.Systems.Audio;

/// <summary>Plays the latched good/bad ending and shows an honest results placeholder.</summary>
public partial class Ending : Control, IControllerCommandSink, IControllerContextSource, IDevSceneSkipTarget
{
	private enum PlaybackPhase { Unavailable, InitialFade, Crossfade, Dialogue, FinalFade, Results }

	[Export] public CinematicSequence GoodSequence { get; set; }
	[Export] public CinematicSequence BadSequence { get; set; }
	[Export] public Texture2D ResultsBackground { get; set; }
	[Export] public string MainMenuScene { get; set; } = "res://scenes/main_menu/main_menu_take3.tscn";
	internal Func<string, Error> SceneLoader { get; set; }

	public MissionEnding Outcome { get; private set; }
	public string SequenceId => _sequence?.Id ?? "";
	public string CurrentPhase => _phase.ToString();
	public int CurrentBeat => _beatIndex;
	public bool IsResultsVisible => _phase == PlaybackPhase.Results;
	public bool IsPaused => _pauseManager?.IsOpen == true;
	public bool CanSkipWithDevCode => IsInsideTree() && _phase is not PlaybackPhase.Results and not PlaybackPhase.Unavailable;
	public string ResultsMessage { get; private set; } = "";

	private MissionState _mission;
	private PauseManager _pauseManager;
	private CinematicSequence _sequence;
	private PlaybackPhase _phase;
	private TextureRect _backdropA, _backdropB;
	private TextureRect _resultsBackground;
	private ColorRect _black;
	private DialogueView _dialogue;
	private Label _scoreText;
	private Button _skipButton, _pauseButton, _returnButton;
	private int _beatIndex;
	private bool _aIsFront = true;
	private double _phaseElapsed;
	private double _phaseDuration;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Pausable;
		BuildUi();
		_skipButton.Pressed += SkipToResults;
		_pauseButton.Pressed += TogglePause;
		_returnButton.Pressed += ReturnToMissionControl;
		_mission = MissionState.Instance;
		_pauseManager = GetNodeOrNull<PauseManager>("/root/PauseManager");
		if (_mission == null)
		{
			ShowUnavailable("MISSION OUTCOME UNAVAILABLE");
			return;
		}

		Outcome = _mission.Ending;
		_mission.Stage = MissionStage.Ending;
		_sequence = Outcome switch
		{
			MissionEnding.Good => GoodSequence ?? GD.Load<CinematicSequence>("res://data/cinematics/ending_good.tres"),
			MissionEnding.Bad => BadSequence ?? GD.Load<CinematicSequence>("res://data/cinematics/ending_bad.tres"),
			_ => null,
		};
		if (_mission.ShowScoreImmediately)
		{
			ControllerCommandRouter.Register(this, this);
			ControllerContext.Begin(this, ControllerContextMode.Ending);
			if (_pauseManager != null) _pauseManager.PauseChanged += OnPauseChanged;
			AudioManager.StopMusic(1.0);
			ShowResults();
			return;
		}
		AudioManager.PlayMusic(Outcome == MissionEnding.Good ? SoundIds.endingMusic : SoundIds.TensionMusic, 1.5);
		if (_sequence?.Beats == null || _sequence.Beats.Count == 0 ||
			_sequence.Beats.Any(beat => beat?.Image == null || string.IsNullOrWhiteSpace(beat.Text)))
		{
			ShowUnavailable("ENDING SEQUENCE UNAVAILABLE");
			return;
		}

		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Ending);
		if (_pauseManager != null) _pauseManager.PauseChanged += OnPauseChanged;

		_beatIndex = 0;
		_backdropA.Texture = _sequence.Beats[0].Image;
		_backdropA.Visible = true;
		_backdropA.Modulate = Colors.White;
		_black.Color = Colors.Black;
		_phase = PlaybackPhase.InitialFade;
		_phaseElapsed = 0;
		_phaseDuration = 0.8;
		PublishControllerContext();
	}

	public override void _Process(double delta)
	{
		if (IsPaused) return;
		_phaseElapsed += Math.Max(0, delta);
		switch (_phase)
		{
			case PlaybackPhase.InitialFade:
				_black.Color = new Color(0, 0, 0, 1f - (float)PhaseProgress());
				if (PhaseFinished()) ShowBeatDialogue();
				break;
			case PlaybackPhase.Crossfade:
				UpdateCrossfade();
				if (PhaseFinished()) ShowBeatDialogue();
				break;
			case PlaybackPhase.Dialogue:
				if (PhaseFinished()) AdvanceBeat();
				break;
			case PlaybackPhase.FinalFade:
				_black.Color = new Color(0, 0, 0, (float)PhaseProgress());
				if (PhaseFinished()) ShowResults();
				break;
		}
	}

	private void BuildUi()
	{
		MouseFilter = MouseFilterEnum.Stop;
		_backdropA = MakeBackdrop("BackdropA", 0);
		_backdropB = MakeBackdrop("BackdropB", 1);
		AddChild(_backdropA);
		AddChild(_backdropB);

		_dialogue = new DialogueView { Name = "Dialogue", Visible = false, ZIndex = 5 };
		AddChild(_dialogue);

		_skipButton = new Button
		{
			Name = "Skip",
			Text = "SKIP",
			ZIndex = 8,
			CustomMinimumSize = new Vector2(120, 36),
		};
		_skipButton.SetAnchorsPreset(LayoutPreset.TopRight);
		_skipButton.OffsetLeft = -140;
		_skipButton.OffsetTop = 18;
		_skipButton.OffsetRight = -20;
		_skipButton.OffsetBottom = 54;
		StyleActionButton(_skipButton, new Color(0.91f, 0.73f, 0.39f));
		AddChild(_skipButton);

		_pauseButton = new Button { Name = "Pause", Text = "PAUSE", ZIndex = 8, CustomMinimumSize = new Vector2(120, 36) };
		_pauseButton.SetAnchorsPreset(LayoutPreset.TopRight);
		_pauseButton.OffsetLeft = -270;
		_pauseButton.OffsetTop = 18;
		_pauseButton.OffsetRight = -150;
		_pauseButton.OffsetBottom = 54;
		StyleActionButton(_pauseButton, new Color(0.48f, 0.85f, 0.9f));
		AddChild(_pauseButton);

		_black = new ColorRect
		{
			Name = "BlackFade",
			Color = Colors.Black,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 10,
		};
		_black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_black);

		_resultsBackground = new TextureRect
		{
			Name = "ResultsBackground",
			Texture = ResultsBackground ?? GD.Load<Texture2D>("res://art/cinematics/ending_score_bg.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 11,
		};
		_resultsBackground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_resultsBackground);

		var resultsOverlay = new Control { Name = "ResultsOverlay", Visible = false, ZIndex = 12 };
		resultsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(resultsOverlay);
		_scoreText = MakeOverlayLabel("", 9, new Color(0.83f, 0.67f, 0.4f));
		_scoreText.Name = "Score";
		_scoreText.HorizontalAlignment = HorizontalAlignment.Center;
		_scoreText.SetAnchorsPreset(LayoutPreset.Center);
		_scoreText.OffsetLeft = -430;
		_scoreText.OffsetRight = 430;
		_scoreText.OffsetTop = -120;
		_scoreText.OffsetBottom = 30;
		_scoreText.AutowrapMode = TextServer.AutowrapMode.Word;
		resultsOverlay.AddChild(_scoreText);
		_returnButton = new Button { Name = "MissionControl", Text = "RETURN TO MISSION CONTROL", CustomMinimumSize = new Vector2(300, 42) };
		_returnButton.SetAnchorsPreset(LayoutPreset.BottomRight);
		_returnButton.OffsetLeft = -340;
		_returnButton.OffsetTop = -78;
		_returnButton.OffsetRight = -24;
		_returnButton.OffsetBottom = -34;
		StyleActionButton(_returnButton, new Color(0.48f, 0.85f, 0.9f));
		resultsOverlay.AddChild(_returnButton);
		_resultsOverlay = resultsOverlay;
	}

	private Control _resultsOverlay;
	private static TextureRect MakeBackdrop(string name, int z)
	{
		var texture = new TextureRect
		{
			Name = name,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = z,
			Visible = false,
		};
		texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return texture;
	}

	private static Label MakeOverlayLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, ClipText = true };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 4);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) label.AddThemeFontOverride("font", font);
		return label;
	}

	private static void StyleActionButton(Button button, Color accent)
	{
		button.AddThemeFontSizeOverride("font_size", 10);
		button.AddThemeColorOverride("font_color", accent);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		var normal = new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.045f, 0.09f, 0.94f),
			BorderColor = accent,
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		};
		var hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = new Color(0.08f, 0.12f, 0.19f, 1f);
		var pressed = (StyleBoxFlat)normal.Duplicate();
		pressed.BgColor = new Color(0.12f, 0.18f, 0.23f, 1f);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) button.AddThemeFontOverride("font", font);
	}

	private void ShowBeatDialogue()
	{
		_black.Color = new Color(0, 0, 0, 0);
		var beat = _sequence.Beats[_beatIndex];
		_dialogue.ShowLine(new DialogueLine { Speaker = beat.Speaker, Text = beat.Text });
		_phase = PlaybackPhase.Dialogue;
		_phaseElapsed = 0;
		_phaseDuration = Math.Max(1, beat.HoldSeconds);
		PublishControllerContext();
	}

	private void AdvanceBeat()
	{
		_dialogue.Clear();
		if (_beatIndex + 1 >= _sequence.Beats.Count)
		{
			_phase = PlaybackPhase.FinalFade;
			_phaseElapsed = 0;
			_phaseDuration = 1.2;
			_black.Color = new Color(0, 0, 0, 0);
			PublishControllerContext();
			return;
		}

		_beatIndex++;
		var incoming = _aIsFront ? _backdropB : _backdropA;
		var outgoing = _aIsFront ? _backdropA : _backdropB;
		incoming.Texture = _sequence.Beats[_beatIndex].Image;
		incoming.Modulate = new Color(1, 1, 1, 0);
		incoming.Show();
		outgoing.Show();
		_phase = PlaybackPhase.Crossfade;
		_phaseElapsed = 0;
		_phaseDuration = Math.Max(0.05, _sequence.Beats[_beatIndex].CrossfadeSeconds);
		PublishControllerContext();
	}

	private void UpdateCrossfade()
	{
		float progress = (float)PhaseProgress();
		var incoming = _aIsFront ? _backdropB : _backdropA;
		var outgoing = _aIsFront ? _backdropA : _backdropB;
		incoming.Modulate = new Color(1, 1, 1, progress);
		outgoing.Modulate = new Color(1, 1, 1, 1 - progress);
		if (!PhaseFinished()) return;
		outgoing.Hide();
		incoming.Modulate = Colors.White;
		_aIsFront = !_aIsFront;
	}

	private double PhaseProgress() => _phaseDuration <= 0 ? 1 : Math.Clamp(_phaseElapsed / _phaseDuration, 0, 1);
	private bool PhaseFinished() => _phaseElapsed >= _phaseDuration;

	private void BeginFinalFade()
	{
		if (_phase is PlaybackPhase.Results or PlaybackPhase.Unavailable or PlaybackPhase.FinalFade) return;
		_dialogue.Clear();
		_phase = PlaybackPhase.FinalFade;
		_phaseElapsed = 0;
		_phaseDuration = 1.2;
		_black.Color = new Color(0, 0, 0, 0);
		PublishControllerContext();
	}

	private void ShowResults()
	{
		_phase = PlaybackPhase.Results;
		_dialogue.Clear();
		_backdropA.Hide();
		_backdropB.Hide();
		_black.Color = Colors.Black;
		_resultsBackground.Visible = true;
		_resultsOverlay.Visible = true;
		_skipButton.Hide();
		_pauseButton.Hide();
		var score = _mission.Scoring;
		string status = Outcome == MissionEnding.Bad
			? "MISSION FAILED — SCORE RECORDED"
			: !score.AllMissionsWon ? "MISSION COMPLETE — NOT ALL MISSIONS WON"
			: score.Passed ? "MISSION COMPLETE — PASS" : "MISSION COMPLETE — BELOW 80%";
		ResultsMessage = $"{status}\n" +
			$"OVERALL {score.Overall:0}% (PASS: {MissionScoring.PassingGrade:0}%)\n" +
			$"TRAINING {score.Training:0}%  LAUNCH {score.Launch:0}%  REPAIRS {score.Repairs:0}%\n" +
			$"ROVER {score.Rover:0}%  RESOURCES {score.Resources:0}%\n" +
			$"ALL MISSIONS WON: {(score.AllMissionsWon ? "YES" : "NO")}\n" +
			$"OXYGEN RETURN RESERVE: {score.FinalOxygenSeconds:0}s / " +
			$"{_mission.Oxygen.ReturnReserveSeconds:0}s " +
			(score.HasReturnReserve ? "SECURED" : "FAILED");
		_scoreText.Text = ResultsMessage;
		_returnButton.GrabFocus();
		PublishControllerContext();
	}

	private void ShowUnavailable(string message)
	{
		_phase = PlaybackPhase.Unavailable;
		_backdropA?.Hide();
		_backdropB?.Hide();
		_dialogue?.Clear();
		_black?.Show();
		_black.Color = Colors.Black;
		_resultsBackground.Visible = true;
		_resultsOverlay.Visible = true;
		_scoreText.Text = "NO MISSION RESULT RECORDED";
		_skipButton?.Hide();
		_pauseButton?.Hide();
		_returnButton?.GrabFocus();
	}

	public void SkipToResults() => BeginFinalFade();
	public bool SkipWithDevCode()
	{
		if (IsResultsVisible || _phase == PlaybackPhase.Unavailable) return false;
		SkipToResults();
		return true;
	}

	private void TogglePause()
	{
		if (_pauseManager == null) return;
		if (_pauseManager.IsOpen) _pauseManager.Close();
		else _pauseManager.OpenPauseMenu();
	}

	private void OnPauseChanged(bool paused) => PublishControllerContext();

	private void ReturnToMissionControl()
	{
		var error = SceneLoader != null ? SceneLoader(MainMenuScene) : GetTree().ChangeSceneToFile(MainMenuScene);
		if (error != Error.Ok) ShowUnavailable("COULD NOT RETURN TO MISSION CONTROL");
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (IsPaused || _phase is PlaybackPhase.Results or PlaybackPhase.Unavailable) return;
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode is Key.Space or Key.Enter)
		{
			GetViewport().SetInputAsHandled();
			SkipToResults();
		}
	}

	public bool HandleControllerCommand(ControllerCommand command)
	{
		if (command.Name == ControllerCommands.EndingPause)
		{
			TogglePause();
			return true;
		}
		if (IsPaused) return true;
		switch (command.Name)
		{
			case ControllerCommands.EndingSkip:
				SkipToResults();
				return true;
			case ControllerCommands.EndingContinue:
				if (IsResultsVisible) ReturnToMissionControl();
				return true;
			default:
				return false;
		}
	}

	public void PublishControllerContext()
	{
		if (_phase == PlaybackPhase.Unavailable) return;
		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["phase"] = _phase.ToString().ToLowerInvariant(),
			["outcome"] = Outcome.ToString().ToLowerInvariant(),
			["beat"] = _beatIndex + 1,
			["beat_count"] = _sequence?.Beats?.Count ?? 0,
			["paused"] = IsPaused,
			["can_skip"] = _phase is not PlaybackPhase.Results,
			["can_continue"] = IsResultsVisible,
		});
	}

	public override void _ExitTree()
	{
		ControllerCommandRouter.Unregister(this);
		ControllerContext.End(this);
		if (GodotObject.IsInstanceValid(_pauseManager)) _pauseManager.PauseChanged -= OnPauseChanged;
	}
}

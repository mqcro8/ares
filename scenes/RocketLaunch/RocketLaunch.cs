using Godot;
using System;
using ares.scripts.mission;
using ares.scripts.training;
using ares.scripts.systems;

/// <summary>Runs launch practice and the real center-hold launch sequence.</summary>
public partial class RocketLaunch : Node2D
{
	public enum Phase { Ready, Ignition, Active, Result, Ascending, Complete }
	public Phase State { get; private set; }
	public bool Paused { get; private set; }
	public bool Practice { get; private set; }
	public LaunchHoldModel Model { get; private set; } = new();
	public LaunchIgnitionModel Ignition { get; private set; } = new();

	private static readonly LaunchIgnitionConfig IgnitionConfig = new(
		TargetClicks: 25, TimeLimitSeconds: 5, MinimumRatio: 0.85);
	private static readonly LaunchHoldConfig HoldConfig = new(
		Initial: 50, Rise: 15, Fall: 15, Low: 45, High: 55, GoalSeconds: 10);
	private static readonly LaunchTargetConfig MovingTargetConfig = new(
		StartCenter: 50, MinCenter: 5, MaxCenter: 95,
		StartSpeed: 3, MaxSpeed: 8, SpeedRampSeconds: 10,
		StartWidth: 10, EndWidth: 8);

	/// <summary>Seed for the moving target path. Zero means a new random path each attempt.</summary>
	[Export] public int TargetSeed { get; set; }
	private LaunchIgnitionResult _ignitionResult = new(0, 25, 22, LaunchIgnitionOutcome.Pending);
	private Rocket rocket;
	private Background background;
	private AnimatedSprite2D fire;
	private Label text, stats, briefingText, briefingSpeaker;
	private TextureRect briefingTutorial;
	private Button briefingStart, briefingBack, pause, back;
	private Control gauge, actions, hud, briefing;
	private ProgressBar ignitionProgress, centeredProgress;
	private TextureRect rail, band, handle;
	private bool holding, spaceHeld, mouseHeld, inputBlocked;
	private bool fromTraining;
	private bool launchAllowed;
	private bool accessDenied;
	private bool ignitionFailed;
	private bool practiceReturnPending;
	private PauseManager pauseManager;
	internal Func<string, Error> SceneLoader { get; set; }
	private Vector2 returnPosition;
	private float ascentOffset, ascentTravel;
	private double ascentProgress;
	private long sequence;
	private string backError;
	public Func<double> Clock { get; set; } = () => Time.GetTicksUsec() / 1_000_000.0;
	private double lastTimestamp;
	private const string Ui = "IntroUI/Panel/Margin/VBox/";
	private const string BriefingUi = "IntroUI/Briefing/Margin/VBox/";

	public override void _Ready()
	{
		lastTimestamp = Clock();
		pauseManager = GetNodeOrNull<PauseManager>("/root/PauseManager");
		if (pauseManager != null) pauseManager.PauseChanged += OnPauseChanged;
		rocket = GetNode<Rocket>("Rocket"); background = GetNode<Background>("Background");
		fire = rocket.GetNode<AnimatedSprite2D>("Fire");
		fire.AnimationFinished += OnAnimationFinished;
		text = GetNode<Label>(Ui + "Text"); stats = GetNode<Label>(Ui + "Stats");
		pause = GetNode<Button>(Ui + "Actions/Pause"); back = GetNode<Button>(Ui + "Actions/Back");
		actions = GetNode<Control>(Ui + "Actions");
		hud = GetNode<Control>("IntroUI/Panel"); briefing = GetNode<Control>("IntroUI/Briefing");
		briefingText = GetNode<Label>(BriefingUi + "Text");
		briefingSpeaker = GetNode<Label>(BriefingUi + "Speaker");
		briefingTutorial = GetNode<TextureRect>(BriefingUi + "Tutorial");
		briefingStart = GetNode<Button>(BriefingUi + "Actions/Start");
		briefingBack = GetNode<Button>(BriefingUi + "Actions/Back");
		gauge = GetNode<Control>(Ui + "Gauge");
		ignitionProgress = GetNode<ProgressBar>("HUD/ProgressBar");
		centeredProgress = GetNode<ProgressBar>(Ui + "CenteredProgress");
		rail = gauge.GetNode<TextureRect>("Rail"); band = gauge.GetNode<TextureRect>("Band");
		handle = gauge.GetNode<TextureRect>("Handle");
		briefingStart.Pressed += BeginIgnitionFromReady; briefingBack.Pressed += Back;
		pause.Pressed += TogglePause; back.Pressed += Back;
		var request = PracticeSession.ConsumeLaunch();
		if (request != null)
		{
			Practice = request.Mode == PracticeSession.LaunchMode.Practice;
			returnPosition = request.ReturnPosition;
			fromTraining = true;
		sequence = request.MissionSequence;
			launchAllowed = Practice || (MissionState.Instance.LaunchPadUnlocked &&
				sequence == MissionState.Instance.LaunchSequence);
		}
		else
		{
			sequence = MissionState.Instance.LaunchSequence;
			launchAllowed = false;
		}
		accessDenied = !launchAllowed;
		if (!Practice && MissionState.Instance.LaunchRecord != null) accessDenied = false;
		Model = CreateHoldModel();
		if (accessDenied || (!Practice && MissionState.Instance.LaunchRecord != null)) State = Phase.Result;
		background.Configure(Practice);
		SetGaugeBand();
		LayoutRocket();
		Refresh();
	}

	private void SetGaugeBand()
	{
		LayoutGaugeBand();
	}

	private LaunchHoldModel CreateHoldModel()
	{
		int seed = TargetSeed != 0 ? TargetSeed : Random.Shared.Next(int.MaxValue);
		var target = MovingTargetConfig with { Seed = seed, RandomDestinations = true };
		return new LaunchHoldModel(HoldConfig, target);
	}

	private void LayoutGaugeBand()
	{
		if (band == null || Model == null) return;
		band.AnchorLeft = (float)Model.TargetLow / 100;
		band.AnchorRight = (float)Model.TargetHigh / 100;
		band.OffsetLeft = band.OffsetRight = 0;
	}

	private void LayoutRocket()
	{
		var size = GetViewportRect().Size;
		float scale = Math.Min(size.X / 1152f, size.Y / 800f);
		rocket.Scale = Vector2.One * scale;
		var body = rocket.GetNode<Sprite2D>("Rocket");
		float foot = body.Position.Y + body.Texture.GetHeight() * body.Scale.Y / 2;
		rocket.Position = background.LaunchGround - new Vector2(594, foot) * scale + new Vector2(0, ascentOffset);
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(pauseManager)) pauseManager.PauseChanged -= OnPauseChanged;
		fire.AnimationFinished -= OnAnimationFinished;
		briefingStart.Pressed -= BeginIgnitionFromReady; briefingBack.Pressed -= Back;
		pause.Pressed -= TogglePause; back.Pressed -= Back;
	}

	private void Back()
	{
		if (fromTraining) { ReturnToLab(); return; }
		if (GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn") != Error.Ok)
		{
			backError = "Unable to return to Mission Control. Please try again.";
			Refresh();
		}
	}

	private void ReturnToLab()
	{
		PracticeSession.Prepare(SceneFilePath, PracticeSession.LabScene, "lab.launch.return", returnPosition);
		var error = SceneLoader != null ? SceneLoader(PracticeSession.LabScene) : GetTree().ChangeSceneToFile(PracticeSession.LabScene);
		if (error == Error.Ok) return;
		PracticeSession.Abort();
		practiceReturnPending = false;
		backError = "Unable to return to the lab. Please try again.";
		Refresh();
	}

	private void BeginIgnitionFromReady()
	{
		if (!launchAllowed || State != Phase.Ready) return;
		if (!Practice && (sequence != MissionState.Instance.LaunchSequence ||
			!MissionState.Instance.LaunchPadUnlocked || MissionState.Instance.LaunchRecord != null))
		{
			accessDenied = true; State = Phase.Result; Refresh(); return;
		}
		BeginIgnition();
	}

	private void BeginIgnition()
	{
		backError = null;
		accessDenied = false;
		ignitionFailed = false;
		practiceReturnPending = false;
		Ignition = new(IgnitionConfig);
		Model = CreateHoldModel();
		State = Phase.Ignition;
		spaceHeld = false;
		mouseHeld = false;
		inputBlocked = false;
		lastTimestamp = Clock();
		ascentOffset = 0;
		ascentProgress = 0;
		holding = false;
		rocket.StartIgnition();
		Refresh();
	}

	private void BeginHoldPhase()
	{
		if (State != Phase.Ignition) return;
		State = Phase.Active;
		Model = CreateHoldModel();
		lastTimestamp = Clock();
		holding = (spaceHeld || mouseHeld) && !inputBlocked;
		rocket.BeginThrust();
		Refresh();
	}

	private void RegisterIgnitionClick()
	{
		if (Paused || State != Phase.Ignition) return;
		Ignition.RegisterClick();
		ignitionProgress.Value = Ignition.Clicks;
		if (Ignition.Complete)
		{
			_ignitionResult = Ignition.Outcome();
			BeginHoldPhase();
		}
		Refresh();
	}

	private void FailIgnition()
	{
		if (State != Phase.Ignition) return;
		_ignitionResult = Ignition.Outcome();
		if (_ignitionResult.CanProceed)
		{
			// A partial ignition still lifts the rocket, but it leaves faults behind.
			BeginHoldPhase();
			return;
		}
		ignitionFailed = true;
		holding = false;
		State = Phase.Result;
		rocket.LaunchFailed();
		Refresh();
	}

	private void TogglePause()
	{
		if (pauseManager != null)
		{
			if (pauseManager.IsOpen) pauseManager.Close();
			else pauseManager.OpenPauseMenu();
			return;
		}
		SetPaused(!Paused);
	}

	private void OnPauseChanged(bool value) => SetPaused(value);

	private void SetPaused(bool value)
	{
		if (Paused == value) return;
		if (value) Synchronize();
		Paused = value;
		lastTimestamp = Clock();
		holding = false;
		spaceHeld = Input.IsPhysicalKeyPressed(Key.Space);
		mouseHeld = Input.IsMouseButtonPressed(MouseButton.Left);
		inputBlocked = spaceHeld || mouseHeld;
		fire.SpeedScale = value ? 0 : 1;
		LayoutRocket(); Refresh();
		if (!value) TryBeginAscent(true);
	}

	public override void _Input(InputEvent ev)
	{
		if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			if (mouse.Pressed && !Paused && !inputBlocked)
			{
				Synchronize();
				mouseHeld = true;
				if (State == Phase.Active)
					holding = true;
			}
			else if (!mouse.Pressed)
			{
				Synchronize();
				mouseHeld = false;
				if (!spaceHeld) holding = false;
				if (!spaceHeld && !mouseHeld) inputBlocked = false;
			}
			return;
		}
		if (ev is not InputEventKey key) return;
		var code = key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode;
		if (code != Key.Space) return;
		GetViewport().SetInputAsHandled();
		if (!key.Pressed)
		{
			Synchronize();
			spaceHeld = false;
			if (!mouseHeld) holding = false;
			if (!spaceHeld && !mouseHeld) inputBlocked = false;
			return;
		}
		if (key.Echo) return;
		Synchronize();
		bool canStart = State == Phase.Ready;
		if (canStart && !inputBlocked) BeginIgnitionFromReady();
		spaceHeld = true;
		if (inputBlocked) return;
		if (State == Phase.Ignition)
			RegisterIgnitionClick();
		else if (State == Phase.Active)
			holding = true;
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (State != Phase.Ignition || Paused || inputBlocked ||
			ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
		Synchronize();
		RegisterIgnitionClick();
		GetViewport().SetInputAsHandled();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusOut && IsNodeReady() &&
			(State == Phase.Ignition || State == Phase.Active || State == Phase.Ascending) && !Paused) TogglePause();
	}

	public override void _Process(double delta)
	{
		Synchronize();
		if (State == Phase.Ascending)
		{
			if (!Paused)
				ascentProgress = Math.Min(1, ascentProgress + Math.Max(0, delta) / 2.8);
			float eased = 1 - Mathf.Pow(1 - (float)ascentProgress, 3);
			ascentOffset = -ascentTravel * eased;
			if (ascentProgress >= 1 && State == Phase.Ascending)
			{
				State = Phase.Complete;
				if (Practice && practiceReturnPending && (GetTree().CurrentScene == this || SceneLoader != null))
				{
					practiceReturnPending = false;
					Callable.From(ReturnToLab).CallDeferred();
				}
			}
		}
		LayoutRocket();
		if (State == Phase.Active && !Paused) rocket.SetThrust(Model.Heat);
		Refresh();
	}

	private void Synchronize()
	{
		double timestamp = Clock();
		if (!double.IsFinite(timestamp) || timestamp < lastTimestamp)
			throw new InvalidOperationException("Launch clock must be finite and monotonic.");
		double delta = timestamp - lastTimestamp;
		lastTimestamp = timestamp;
		if (Paused) return;
		if (State == Phase.Ignition)
		{
			Ignition.Advance(delta);
			if (Ignition.TimedOut) FailIgnition();
			return;
		}
		if (State != Phase.Active) return;
		Model.Advance(delta, holding);
		if (Model.Complete) FinishRun();
	}

	private void FinishRun()
	{
		holding = false;
		State = Phase.Result;
		if (sequence != MissionState.Instance.LaunchSequence)
		{
			launchAllowed = false; accessDenied = true;
			rocket.StopFire();
			return;
		}
		var result = BuildLaunchResult();
		RunResult = result;
		if (Practice)
		{
			MissionState.Instance.TrainingLaunchPracticeComplete = true;
			MissionState.Instance.EvaluateTrainingCompletion();
			practiceReturnPending = true;
		}
		else if (!MissionState.Instance.RecordLaunch(sequence, result))
		{
			launchAllowed = false;
			accessDenied = true;
			rocket.StopFire();
			Refresh();
			return;
		}
		rocket.LaunchSuccess();
		Refresh();
	}

	/// <summary>Result of the current run, including the ignition faults it carries.</summary>
	public LaunchResult RunResult { get; private set; }

	/// <summary>Combines the hold result with the ignition outcome so faults travel with the launch.</summary>
	private LaunchResult BuildLaunchResult()
	{
		var hold = Model.Result;
		if (hold == null) return null;
		return hold with
		{
			IgnitionClicks = _ignitionResult.Clicks,
			IgnitionTargetClicks = _ignitionResult.TargetClicks,
			IgnitionFaults = _ignitionResult.Faults,
		};
	}

	private void OnAnimationFinished()
	{
		TryBeginAscent(false);
	}

	private void TryBeginAscent(bool requireStopped)
	{
		if (!IsInsideTree() || Paused || State != Phase.Result || fire.Animation != "success" ||
			(requireStopped && fire.IsPlaying()) || !Model.Complete || sequence != MissionState.Instance.LaunchSequence) return;
		State = Phase.Ascending;
		ascentProgress = 0; ascentOffset = 0;
		ascentTravel = GetViewportRect().Size.Y * .72f;
		rocket.StartAscending(); Refresh();
	}

	private void Refresh()
	{
		if (text == null) return;
		var result = RunResult ?? Model.Result ?? (!Practice ? MissionState.Instance.LaunchRecord : null);
		bool hidePracticeAscentHud = Practice && !Paused && backError == null && (State == Phase.Ascending || State == Phase.Complete);
		briefing.Visible = State == Phase.Ready;
		hud.Visible = State != Phase.Ready && !hidePracticeAscentHud;
		if (backError != null) text.Text = backError;
		else if (accessDenied) text.Text = "LAUNCH PAD LOCKED — complete all training requirements and enter through the marked pad.";
		else if (Paused) text.Text = "PAUSED — launch progress frozen. Resume to continue.";
		else text.Text = State switch
		{
			Phase.Ready => Practice
				? $"LAUNCH SIMULATOR — tap {Ignition.Config.TargetClicks} times to ignite (at least {Ignition.Config.MinimumClicks} to launch), then hold the marker in the green area for 10 seconds."
				: $"LAUNCH SEQUENCE — tap {Ignition.Config.TargetClicks} times to ignite (at least {Ignition.Config.MinimumClicks} to launch), then hold the marker in the green area for 10 seconds.",
			Phase.Ignition => $"IGNITION — tap SPACE or click {Ignition.Config.TargetClicks} times within {Ignition.Config.TimeLimitSeconds:F0} seconds. Minimum {Ignition.Config.MinimumClicks} to launch.",
			Phase.Active => ActiveMessage(),
			Phase.Ascending => Practice ? "SIMULATOR COMPLETE — watch the rocket ascend." : "LAUNCH COMPLETE — ascending.",
			Phase.Complete => Practice ? "SIMULATOR COMPLETE — returning to the lab." : "ASCENT COMPLETE — waiting for the next scene.",
			_ => ResultMessage()
		};
		briefingSpeaker.Text = Practice ? "LAUNCH SIMULATOR" : "LAUNCH PAD AUTHORIZED";
		briefingText.Text = Practice
			? $"Tap SPACE or click {Ignition.Config.TargetClicks} times within {Ignition.Config.TimeLimitSeconds:F0} seconds to ignite. {Ignition.Config.MinimumClicks} taps still lift the rocket. Then hold SPACE or mouse to raise and stay in the green band for 10 seconds."
			: $"Tap SPACE or click {Ignition.Config.TargetClicks} times within {Ignition.Config.TimeLimitSeconds:F0} seconds to ignite. {Ignition.Config.MinimumClicks} taps still lift the rocket. Then hold in the green band for 10 seconds.";
		briefingTutorial.Visible = Practice;
		if (Practice)
		{
			float tutorialWidth = Math.Min(480, GetViewportRect().Size.X * .64f);
			briefingTutorial.CustomMinimumSize = new Vector2(tutorialWidth, tutorialWidth * .4f);
			briefing.OffsetTop = -180; briefing.OffsetBottom = 180;
		}
		else
		{
			briefingTutorial.CustomMinimumSize = Vector2.Zero;
			briefing.OffsetTop = -96; briefing.OffsetBottom = 96;
		}
		briefingStart.Text = Practice ? "Begin simulator" : "Begin ignition";
		briefingBack.Text = fromTraining ? "Return to lab" : "Back to Mission Control";
		if (ignitionFailed) stats.Text = IgnitionStats();
		else if ((State == Phase.Result || State == Phase.Ascending || State == Phase.Complete) && result != null) stats.Text = ResultStats(result);
		else stats.Text = ProgressStats();
		ignitionProgress.Value = Ignition.Clicks;
		ignitionProgress.Visible = !accessDenied && !hidePracticeAscentHud &&
			(State == Phase.Ignition || State == Phase.Active || ignitionFailed || Ignition.Clicks > 0);
		centeredProgress.Value = Model.Progress * 100.0;
		briefingStart.Disabled = !launchAllowed;
		actions.Visible = State == Phase.Ignition || State == Phase.Active || Paused || (!practiceReturnPending &&
			(State == Phase.Result || State == Phase.Ascending || State == Phase.Complete));
		hud.OffsetBottom = actions.Visible ? 140 : 100;
		pause.Visible = State == Phase.Ignition || State == Phase.Active || (Paused && State == Phase.Ascending);
		back.Visible = Paused || (!practiceReturnPending &&
			(State == Phase.Result || State == Phase.Ascending || State == Phase.Complete));
		pause.Text = Paused ? "Resume" : "Pause";
		back.Text = fromTraining ? "Return to lab" : "Back to main menu";
		LayoutGauge();
	}

	private string ActiveMessage() => Model.InBand
		? "IN CENTER — KEEP IT THERE."
		: "MOVE THE MARKER INTO THE GREEN AREA.";

	private string ProgressStats() => State == Phase.Ignition
		? IgnitionStats()
		: $"CENTERED {Model.CenteredTime:F1}/{Model.Config.GoalSeconds:F0}s | MARKER {Model.Heat:F0}";

	private string IgnitionStats() =>
		$"IGNITION {Ignition.Clicks}/{Ignition.Config.TargetClicks} (MIN {Ignition.Config.MinimumClicks}) | {Ignition.Remaining:F1}s";

	private string ResultStats(LaunchResult result)
	{
		string faults = result.IgnitionFaults > 0
			? $" | IGNITION FAULTS {result.IgnitionFaults}"
			: " | IGNITION PERFECT";
		return $"CENTERED {result.CenteredSeconds:F0}/{Model.Config.GoalSeconds:F0}s{faults}";
	}

	private string ResultMessage()
	{
		if (ignitionFailed)
			return fromTraining ? "IGNITION FAILED — return to the lab when you are ready to try again."
				: "IGNITION FAILED — return to Mission Control.";
		string faults = _ignitionResult.Faults > 0
			? $" IGNITION FAULTS: {_ignitionResult.Faults}."
			: string.Empty;
		return Practice
			? $"SIMULATOR COMPLETE — launch practice requirement cleared.{faults} Return to the lab and walk to LAUNCH PAD."
			: $"LAUNCH COMPLETE — waiting for the next scene.{faults}";
	}

	private void LayoutGauge()
	{
		if (gauge == null || handle == null) return;
		LayoutGaugeBand();
		float width = Math.Max(1, gauge.Size.X);
		float halfHandle = handle.Size.X / 2;
		float center = width <= handle.Size.X ? width / 2
			: Mathf.Clamp((float)Model.Heat / 100 * width, halfHandle, width - halfHandle);
		handle.Position = new Vector2(center - halfHandle, (gauge.Size.Y - handle.Size.Y) / 2);
		handle.Modulate = Model.InBand ? Colors.White : new Color(1, .65f, .35f);
		rail.Modulate = Paused ? new Color(1, 1, 1, .5f) : Colors.White;
	}
}

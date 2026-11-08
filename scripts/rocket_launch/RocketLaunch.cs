using Godot;
using System;
using ares.scripts.mission;
using ares.scripts.training;

/// <summary>Runs launch practice and the real center-hold launch sequence.</summary>
public partial class RocketLaunch : Node2D
{
	public enum Phase { Ready, Active, Result, Ascending, Complete }
	public Phase State { get; private set; }
	public bool Paused { get; private set; }
	public bool Practice { get; private set; }
	public LaunchHoldModel Model { get; private set; } = new();

	private static readonly LaunchHoldConfig HoldConfig = new(
		Initial: 50, Rise: 15, Fall: 15, Low: 45, High: 55, GoalSeconds: 10);
	private Rocket rocket;
	private Background background;
	private AnimatedSprite2D fire;
	private Label text, stats, briefingText, briefingSpeaker;
	private TextureRect briefingTutorial;
	private Button start, briefingStart, briefingBack, pause, back;
	private Control gauge, actions, hud, briefing;
	private TextureRect rail, band, handle;
	private bool holding, spaceHeld, mouseHeld, inputBlocked;
	private bool fromTraining;
	private bool launchAllowed;
	private bool accessDenied;
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
		rocket = GetNode<Rocket>("Rocket"); background = GetNode<Background>("Background");
		fire = rocket.GetNode<AnimatedSprite2D>("Fire");
		fire.AnimationFinished += OnAnimationFinished;
		text = GetNode<Label>(Ui + "Text"); stats = GetNode<Label>(Ui + "Stats");
		start = GetNode<Button>(Ui + "Actions/Continue");
		pause = GetNode<Button>(Ui + "Actions/Pause"); back = GetNode<Button>(Ui + "Actions/Back");
		actions = GetNode<Control>(Ui + "Actions");
		hud = GetNode<Control>("IntroUI/Panel"); briefing = GetNode<Control>("IntroUI/Briefing");
		briefingText = GetNode<Label>(BriefingUi + "Text");
		briefingSpeaker = GetNode<Label>(BriefingUi + "Speaker");
		briefingTutorial = GetNode<TextureRect>(BriefingUi + "Tutorial");
		briefingStart = GetNode<Button>(BriefingUi + "Actions/Start");
		briefingBack = GetNode<Button>(BriefingUi + "Actions/Back");
		gauge = GetNode<Control>(Ui + "Gauge");
		rail = gauge.GetNode<TextureRect>("Rail"); band = gauge.GetNode<TextureRect>("Band");
		handle = gauge.GetNode<TextureRect>("Handle");
		start.Pressed += StartOrRetry; briefingStart.Pressed += StartOrRetry; briefingBack.Pressed += Back;
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
		Model = new(HoldConfig);
		if (accessDenied || (!Practice && MissionState.Instance.LaunchRecord != null)) State = Phase.Result;
		background.Configure(Practice);
		SetGaugeBand();
		LayoutRocket();
		Refresh();
	}

	private void SetGaugeBand()
	{
		band.AnchorLeft = (float)Model.Config.Low / 100;
		band.AnchorRight = (float)Model.Config.High / 100;
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
		fire.AnimationFinished -= OnAnimationFinished;
		briefingStart.Pressed -= StartOrRetry; briefingBack.Pressed -= Back;
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
		var error = GetTree().ChangeSceneToFile(PracticeSession.LabScene);
		if (error == Error.Ok) return;
		PracticeSession.Abort();
		backError = "Unable to return to the lab. Please try again.";
		Refresh();
	}

	private void StartOrRetry()
	{
		bool retryable = launchAllowed && (State == Phase.Ready || (Practice && State == Phase.Result));
		if (!retryable) return;
		if (!Practice && (sequence != MissionState.Instance.LaunchSequence ||
			!MissionState.Instance.LaunchPadUnlocked || MissionState.Instance.LaunchRecord != null))
		{
			accessDenied = true; State = Phase.Result; Refresh(); return;
		}
		backError = null; lastTimestamp = Clock();
		accessDenied = false;
		Model = new(HoldConfig); State = Phase.Active;
		ascentOffset = 0; ascentProgress = 0;
		rocket.StopFire();
		rocket.BeginThrust();
		holding = (spaceHeld || mouseHeld) && !inputBlocked;
		Refresh();
	}

	private void TogglePause() => SetPaused(!Paused);

	private void SetPaused(bool value)
	{
		Synchronize();
		Paused = value;
		if (value) { holding = false; inputBlocked = true; }
		else inputBlocked = spaceHeld || mouseHeld;
		fire.SpeedScale = value ? 0 : 1;
		LayoutRocket(); Refresh();
		if (!value) TryBeginAscent(true);
	}

	public override void _Input(InputEvent ev)
	{
		if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			if (mouse.Pressed && !Paused && !inputBlocked && State == Phase.Active)
			{
				Synchronize();
				mouseHeld = true;
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
		if (code == Key.Escape && key.Pressed && !key.Echo)
		{
			if (State == Phase.Active || Paused) TogglePause();
			GetViewport().SetInputAsHandled(); return;
		}
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
		bool canStart = State == Phase.Ready || (Practice && State == Phase.Result);
		if (canStart && !inputBlocked) StartOrRetry();
		spaceHeld = true;
		if (!inputBlocked && State == Phase.Active) holding = true;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusOut && IsNodeReady() && State == Phase.Active) SetPaused(true);
	}

	public override void _Process(double delta)
	{
		Synchronize();
		if (State == Phase.Ascending)
		{
			ascentProgress = Math.Min(1, ascentProgress + Math.Max(0, delta) / 2.8);
			float eased = 1 - Mathf.Pow(1 - (float)ascentProgress, 3);
			ascentOffset = -ascentTravel * eased;
			if (ascentProgress >= 1) State = Phase.Complete;
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
		if (Paused || State != Phase.Active) return;
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
		if (Practice)
		{
			MissionState.Instance.TrainingLaunchPracticeComplete = true;
			MissionState.Instance.EvaluateTrainingCompletion();
		}
		else if (!MissionState.Instance.RecordLaunch(sequence, Model.Result))
		{
			launchAllowed = false;
			accessDenied = true;
			rocket.StopFire();
			Refresh();
			return;
		}
		rocket.LaunchSuccess();
	}

	private void OnAnimationFinished() => TryBeginAscent(false);

	private void TryBeginAscent(bool requireStopped)
	{
		if (!IsInsideTree() || Paused || Practice || State != Phase.Result || fire.Animation != "success" ||
			(requireStopped && fire.IsPlaying()) || !Model.Complete || sequence != MissionState.Instance.LaunchSequence) return;
		State = Phase.Ascending;
		ascentProgress = 0; ascentOffset = 0;
		ascentTravel = GetViewportRect().Size.Y * .72f;
		rocket.StartAscending(); Refresh();
	}

	private void Refresh()
	{
		if (text == null) return;
		var result = Model.Result ?? (!Practice ? MissionState.Instance.LaunchRecord : null);
		briefing.Visible = State == Phase.Ready;
		hud.Visible = State != Phase.Ready;
		if (backError != null) text.Text = backError;
		else if (accessDenied) text.Text = "LAUNCH PAD LOCKED — complete all training requirements and enter through the marked pad.";
		else if (Paused) text.Text = "PAUSED — progress frozen. Resume, then release SPACE or mouse to hold steady.";
		else text.Text = State switch
		{
			Phase.Ready => Practice
				? "LAUNCH SIMULATOR — hold the marker in the green center area for 10 seconds."
				: "LAUNCH SEQUENCE — hold the marker in the green center area for 10 seconds.",
			Phase.Active => ActiveMessage(),
			Phase.Ascending => "LAUNCH COMPLETE — ascending. Waiting for the next scene.",
			Phase.Complete => "ASCENT COMPLETE — waiting for the next scene.",
			_ => ResultMessage()
		};
		briefingSpeaker.Text = Practice ? "LAUNCH SIMULATOR" : "LAUNCH PAD AUTHORIZED";
		briefingText.Text = Practice
			? "Hold SPACE or mouse to raise. Release to lower. Stay in the green band for 10 seconds."
			: "Training complete. Launch sequence ready.";
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
		briefingStart.Text = Practice ? "Begin practice" : "Begin launch";
		briefingBack.Text = fromTraining ? "Return to lab" : "Back to Mission Control";
		if ((State == Phase.Result || State == Phase.Ascending || State == Phase.Complete) && result != null) stats.Text = ResultStats(result);
		else stats.Text = ProgressStats();
		start.Visible = launchAllowed && Practice && State == Phase.Result;
		start.Text = "Repeat practice";
		start.Disabled = Paused;
		briefingStart.Disabled = !launchAllowed;
		actions.Visible = Paused || State == Phase.Result || State == Phase.Ascending || State == Phase.Complete;
		hud.OffsetBottom = actions.Visible ? 140 : 100;
		pause.Visible = Paused && State == Phase.Active;
		back.Visible = Paused || State == Phase.Result || State == Phase.Ascending || State == Phase.Complete;
		pause.Text = Paused ? "Resume" : "Pause (Esc)";
		back.Text = fromTraining ? "Return to lab" : "Back to main menu";
		LayoutGauge();
	}

	private string ActiveMessage() => Model.InBand
		? "IN CENTER — KEEP IT THERE."
		: "MOVE THE MARKER INTO THE GREEN AREA.";

	private string ProgressStats() => $"CENTERED {Model.CenteredTime:F1}/{Model.Config.GoalSeconds:F0}s | MARKER {Model.Heat:F0}";

	private string ResultStats(LaunchResult result) => $"CENTERED {result.CenteredSeconds:F0}/{Model.Config.GoalSeconds:F0}s | COMPLETE";

	private string ResultMessage() => Practice
		? "SIMULATOR COMPLETE — launch practice requirement cleared. Return to the lab and walk to LAUNCH PAD."
		: "LAUNCH COMPLETE — waiting for the next scene.";

	private void LayoutGauge()
	{
		if (gauge == null || handle == null) return;
		float width = Math.Max(1, gauge.Size.X);
		float halfHandle = handle.Size.X / 2;
		float center = width <= handle.Size.X ? width / 2
			: Mathf.Clamp((float)Model.Heat / 100 * width, halfHandle, width - halfHandle);
		handle.Position = new Vector2(center - halfHandle, (gauge.Size.Y - handle.Size.Y) / 2);
		handle.Modulate = Model.InBand ? Colors.White : new Color(1, .65f, .35f);
		rail.Modulate = Paused ? new Color(1, 1, 1, .5f) : Colors.White;
	}
}

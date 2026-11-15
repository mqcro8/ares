using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.mission;
using ares.scripts.training;
using ares.scripts.systems;
using Ares.Systems.Audio;

public partial class RocketLaunchTest : Node
{
    private int assertions;
    private RocketLaunch launch;
    private double now;
    private bool holding;
    private bool Visual => OS.GetCmdlineUserArgs().Contains("--visual");
    private const string Ui = "IntroUI/Panel/Margin/VBox/";
    private const string BriefingUi = "IntroUI/Briefing/Margin/VBox/";

    private void Advance(double seconds) { now += seconds; launch._Process(seconds); }
    private void Check(bool ok, string message)
    { assertions++; if (!ok) throw new Exception(message); }

    public override async void _Ready()
    {
        try
        {
            LaunchIgnitionModelTest.Run(Check);
            LaunchHoldModelTest.Run(Check);
            await Run();
            await PartialIgnitionLaunch();
            GD.Print($"LAUNCH PASS ({assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task Load(PracticeSession.LaunchMode mode = PracticeSession.LaunchMode.Real)
    {
        if (launch != null) { launch.QueueFree(); launch = null; await Frames(); }
        if (PracticeSession.PendingLaunch == null)
            PracticeSession.RequestLaunch(mode, new Vector2(12, 34));
        launch = GD.Load<PackedScene>("res://scenes/RocketLaunch/Launching.tscn").Instantiate<RocketLaunch>();
        now = 0; holding = false; launch.Clock = () => now;
        AddChild(launch); launch.SetProcess(false); await Frames();
    }

    private void SendKey(Key key, bool down, bool echo = false)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = down, Echo = echo });
        Input.FlushBufferedEvents();
    }

    private void SendMouse(bool down)
    {
        var position = new Vector2(400, 300);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position,
            ButtonIndex = MouseButton.Left, Pressed = down
        });
        Input.FlushBufferedEvents();
    }

    private void ClickPath(string path)
    {
        var point = launch.GetNode<Button>(path).GetGlobalRect().GetCenter();
        foreach (bool down in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton
            {
                Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down
            }, true);
    }

    private async Task Capture(string name)
    {
        if (!Visual) return;
        foreach (var size in new[] { new Vector2I(640, 480), new Vector2I(1024, 768), new Vector2I(1280, 720) })
        {
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            GetWindow().ContentScaleSize = size; GetWindow().Size = size;
            await Frames();
            if (launch.Paused)
            {
                launch.GetNode<Button>(Ui + "Actions/Pause").EmitSignal(BaseButton.SignalName.Pressed);
            }
            launch._Process(0);
            Control overlay = launch.State == RocketLaunch.Phase.Ready
                ? launch.GetNode<Control>("IntroUI/Briefing") : launch.GetNode<Control>("IntroUI/Panel");
            Check(GetViewport().GetVisibleRect().Encloses(overlay.GetGlobalRect()), "Overlay fits " + size);
            if (launch.State == RocketLaunch.Phase.Active)
            {
                Check(overlay.Size.Y < size.Y * .32f, "Active slider HUD with progress and pause stays compact at " + size);
                var targetBand = launch.GetNode<TextureRect>(Ui + "Gauge/Band");
                Check(targetBand.AnchorLeft >= 0 && targetBand.AnchorRight <= 1 &&
                    targetBand.AnchorLeft < targetBand.AnchorRight,
                    "Moving target stays within the gauge at " + size);
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/launch-{name}-{size.X}x{size.Y}.png") == Error.Ok, "Capture");
        }
    }

    private void Start()
    {
        ClickPath(BriefingUi + "Actions/Start");
        Check(launch.State == RocketLaunch.Phase.Ignition, "Start begins the ignition phase");
    }

    private void CompleteIgnition()
    {
        for (int i = 0; i < launch.Ignition.Config.TargetClicks; i++)
        {
            SendKey(Key.Space, true);
            SendKey(Key.Space, false);
        }
        Check(launch.State == RocketLaunch.Phase.Active && launch.Ignition.Complete,
            "Twenty-five ignition taps begin the hold phase");
    }

    private void Hold(bool down)
    {
        if (holding == down) return;
        SendKey(Key.Space, down); holding = down;
    }

    private static void CompleteCoreTraining(MissionState mission)
    {
        mission.TrainingCourseComplete = true;
        mission.TrainingWiresComplete = true;
        mission.TrainingFusesComplete = true;
    }

    private static void CompleteAllTraining(MissionState mission)
    {
        CompleteCoreTraining(mission);
        mission.TrainingLaunchPracticeComplete = true;
    }

    private void DriveToComplete()
    {
        while (!launch.Model.Complete && now < 50)
        {
            Hold(launch.Model.Heat < launch.Model.TargetCenter);
            Advance(.01);
        }
        Hold(false);
        Check(launch.Model.Complete, "Feedback hold completes ten centered seconds");
    }

    private async Task WaitFor(RocketLaunch.Phase phase)
    {
        for (int i = 0; i < 150 && launch.State != phase; i++)
            await ToSignal(GetTree().CreateTimer(.02), SceneTreeTimer.SignalName.Timeout);
        Check(launch.State == phase, "State reaches " + phase);
    }

    private async Task Run()
    {
        var mission = MissionState.Instance;
        AudioManager.PlayMusic(SoundIds.MenuMusic, 0, true);
        mission.BeginLaunchSequence(); mission.Energy = 50;
        CompleteAllTraining(mission);
        Check(!mission.RecordLaunch(mission.LaunchSequence, new LaunchResult(.5, 10)),
            "Real launch rejects a partial center hold");
        await Load(); await Capture("ready");
        Check(launch.GetNode<Background>("Background").Texture.ResourcePath.EndsWith("launching_background.jpeg"),
            "Launch-site background is used");
        Check(launch.GetNode<Sprite2D>("Rocket/Rocket").Texture.ResourcePath.EndsWith("rocket_sprite.png"),
            "Existing rocket sprite is preserved");
        Check(launch.GetNode<Control>("IntroUI/Briefing").Visible && !launch.GetNode<Control>("IntroUI/Panel").Visible,
            "Launch Pad shows a compact ready dialog");
        var launchBriefing = launch.GetNode<PanelContainer>("IntroUI/Briefing");
        Check(Mathf.IsEqualApprox(launchBriefing.AnchorLeft, 0.08f) &&
            Mathf.IsEqualApprox(launchBriefing.AnchorRight, 0.92f) &&
            launchBriefing.CustomMinimumSize.Y == ares.scripts.dialogue.DialoguePanelStyle.StandardHeight &&
            launch.GetNode<Label>(BriefingUi + "Speaker").GetThemeFontSize("font_size") == ares.scripts.dialogue.DialoguePanelStyle.SpeakerSize &&
            launch.GetNode<Label>(BriefingUi + "Text").GetThemeFontSize("font_size") == ares.scripts.dialogue.DialoguePanelStyle.BodySize,
            "Launch briefing uses the shared dialogue style and responsive width");
        Check(!launch.GetNode<TextureRect>(BriefingUi + "Tutorial").Visible &&
            !launch.GetNode<Label>(BriefingUi + "Text").Text.Contains("Hold SPACE"),
            "Real launch does not repeat the simulator tutorial");
        Check(launch.Model.Config.GoalSeconds == 10 && launch.Model.Config.Low == 45 && launch.Model.Config.High == 55,
            "One shared ten-second center band is used");
        Check(launch.Model.Result == null, "No event schedule, precision score or countdown");
        var band = launch.GetNode<TextureRect>(Ui + "Gauge/Band");
        var ignitionProgress = launch.GetNode<ProgressBar>("HUD/ProgressBar");
        var centeredProgress = launch.GetNode<ProgressBar>(Ui + "CenteredProgress");
        Check(launch.Ignition.Config.TargetClicks == 25 && launch.Ignition.Config.TimeLimitSeconds == 5 &&
            ignitionProgress.MaxValue == 25 && ignitionProgress.Value == 0,
            "Black-and-yellow ignition bar starts at zero with twenty-five taps and five seconds");
        Check(centeredProgress.MaxValue == 100 && centeredProgress.Value == 0,
            "Centered-hold progress starts empty");
        Check(Math.Abs(band.AnchorLeft - .45) < .001 && Math.Abs(band.AnchorRight - .55) < .001,
            "Slider target is centered");

        Start();
        var pauseButton = launch.GetNode<Button>(Ui + "Actions/Pause");
        Check(launch.GetNode<Control>(Ui + "Actions").Visible && pauseButton.Visible && pauseButton.Text == "Pause",
            "Ignition exposes a working pause button");
        await Capture("ignition");

        if (Visual)
        {
            for (int i = 0; i < 40 && launch.Ignition.Clicks < 21; i++)
            {
                SendKey(Key.Space, true);
                SendKey(Key.Space, false);
            }
        }
        else
        {
            SendMouse(true); SendMouse(false);
            for (int i = 0; i < 20; i++)
            {
                SendKey(Key.Space, true);
                SendKey(Key.Space, false);
            }
        }
        Check(launch.State == RocketLaunch.Phase.Ignition && launch.Ignition.Clicks == 21 &&
            ignitionProgress.Value == 21,
            $"Ignition bar tracks each tap and waits for the final tap ({launch.State}, {launch.Ignition.Clicks}, {ignitionProgress.Value})");
        Check(launch.Ignition.Config.MinimumClicks == 22 && !launch.Ignition.MeetsMinimum,
            "Ignition exposes the eighty-five percent minimum and reports it is not reached yet");
        await Capture("ignition-filled");

        launch.Notification((int)Node.NotificationApplicationFocusOut);
        double ignitionElapsed = launch.Ignition.Elapsed;
        Advance(2);
        Check(launch.Paused && launch.Ignition.Elapsed == ignitionElapsed,
            "Focus pause freezes the ignition timer");
        pauseButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!launch.Paused, "Ignition can resume after focus pause");
        SendKey(Key.Escape, true); SendKey(Key.Escape, false);
        double beforeGlobalPause = launch.Ignition.Elapsed;
        now += 8;
        SendKey(Key.Escape, true); SendKey(Key.Escape, false);
        Advance(.1);
        Check(!launch.Ignition.Failed && Math.Abs(launch.Ignition.Elapsed - beforeGlobalPause - .1) < .0001,
            "Long global pause does not consume the five-second ignition window");
        Advance(5);
        Check(launch.State == RocketLaunch.Phase.Result && launch.Ignition.Failed &&
            launch.GetNode<AnimatedSprite2D>("Rocket/Fire").Animation == "failed",
            "Ignition timeout below the minimum shows its failure animation");
        Check(launch.GetNodeOrNull<Button>(Ui + "Actions/Retry") == null &&
            launch.GetNode<Button>(Ui + "Actions/Back").Visible,
            "Failed ignition offers a way out but no direct retry");
        SendKey(Key.Space, true); SendKey(Key.Space, false);
        Check(launch.State == RocketLaunch.Phase.Result && launch.Ignition.Clicks == 21,
            "Space cannot restart a failed ignition");
        launch.QueueFree(); launch = null; await Frames();
        await Load();
        Check(launch.State == RocketLaunch.Phase.Ready,
            "A new simulator entry remains available after leaving a failed attempt");
        ignitionProgress = launch.GetNode<ProgressBar>("HUD/ProgressBar");
        centeredProgress = launch.GetNode<ProgressBar>(Ui + "CenteredProgress");
        band = launch.GetNode<TextureRect>(Ui + "Gauge/Band");
        pauseButton = launch.GetNode<Button>(Ui + "Actions/Pause");
        Start();
        CompleteIgnition();
        AudioManager.PlayMusic(SoundIds.MenuMusic, 0, true);
        Check(ignitionProgress.Value == 25 && centeredProgress.Value == 0,
            "Ignition completes before centered hold progress begins");
        Check(pauseButton.Visible && pauseButton.Text == "Pause",
            "Active hold phase keeps the pause button available");
        await Capture("active");
        double initialBandLeft = band.AnchorLeft;
        Advance(.1);
        Check(launch.Model.TargetCenter != 50 && band.AnchorLeft != initialBandLeft,
            "The rendered target band moves along the gauge during the active hold");
        Check(launch.Model.TargetConfig.RandomDestinations && launch.Model.TargetConfig.Seed != 0,
            "The active hold uses a random seeded target path");

        SendKey(Key.Escape, true); SendKey(Key.Escape, false);
        Check(GetTree().Paused && launch.Paused && !AudioManager.IsMusicPaused &&
            !AudioManager.IsActiveMusicStreamPaused && AudioManager.Instance?.CurrentMusicId == SoundIds.MenuMusic,
            "Escape pauses launch but keeps music playing through the global pause manager");
        double pausedProgressTime = launch.Model.CenteredTime;
        double pausedMarker = launch.Model.Heat;
        double pausedTargetCenter = launch.Model.TargetCenter;
        now += 12;
        SendKey(Key.Escape, true); SendKey(Key.Escape, false);
        Check(!launch.Paused && launch.Model.CenteredTime == pausedProgressTime &&
            launch.Model.Heat == pausedMarker && launch.Model.TargetCenter == pausedTargetCenter,
            "Resume rebases the hold clock without advancing heat or target during the menu");
        Check(!GetTree().Paused && !AudioManager.IsMusicPaused && AudioManager.Instance?.CurrentMusicId == null,
            "Closing pause removes music through the global pause manager");

        AudioManager.PlayMusic(SoundIds.MenuMusic, 0, true);
        pauseButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(GetTree().Paused && ((PauseManager)GetNode("/root/PauseManager")).IsOpen &&
            !AudioManager.IsMusicPaused && !AudioManager.IsActiveMusicStreamPaused &&
            AudioManager.Instance?.CurrentMusicId == SoundIds.MenuMusic,
            "Pause button opens the real pause menu and keeps music playing");
        double menuPausedHeat = launch.Model.Heat;
        Check(launch.Model.Heat == menuPausedHeat && launch.State == RocketLaunch.Phase.Active,
            "Pause button freezes launch state until resumed");
        SendKey(Key.Escape, true); SendKey(Key.Escape, false);
        Check(!GetTree().Paused && !AudioManager.IsMusicPaused && AudioManager.Instance?.CurrentMusicId == null,
            "Closing button-opened pause removes music");

        SendKey(Key.Space, true); Advance(.2);
        double raisedHeat = launch.Model.Heat;
        Check(raisedHeat > 50, "Holding SPACE raises the marker");
        SendKey(Key.Space, false); Advance(.6);
        Check(launch.Model.Heat < raisedHeat, "Releasing lowers the marker");
        Check(launch.Model.InBand ==
            (launch.Model.Heat >= launch.Model.TargetLow && launch.Model.Heat <= launch.Model.TargetHigh),
            "Band membership follows the moving target instead of a fixed center");
        double frozenProgress = launch.Model.CenteredTime;
        bool wasInBand = launch.Model.InBand;
        Advance(1);
        Check(wasInBand || launch.Model.CenteredTime == frozenProgress,
            "Progress pauses outside the moving center band");
        SendMouse(true); Advance(.2); double mouseHeat = launch.Model.Heat;
        SendMouse(false); Advance(.2);
        Check(mouseHeat > launch.Model.Heat, "Holding and releasing mouse controls the marker");

        SendMouse(true); Advance(.1);
        launch.Notification((int)Node.NotificationApplicationFocusOut);
        double pausedHeat = launch.Model.Heat, pausedProgress = launch.Model.CenteredTime;
        double pausedTarget = launch.Model.TargetCenter;
        Advance(2);
        Check(launch.Paused && launch.Model.Heat == pausedHeat &&
            launch.Model.CenteredTime == pausedProgress && launch.Model.TargetCenter == pausedTarget,
            "Pause freezes marker, target, and progress");
        Check(launch.GetNode<AnimatedSprite2D>("Rocket/Fire").SpeedScale == 0, "Pause freezes thrust animation");
        launch.GetNode<Button>(Ui + "Actions/Pause").EmitSignal(BaseButton.SignalName.Pressed);
        Check(!launch.Paused, "Resume clears pause");
        SendMouse(false); Advance(.1);
        double releasedHeat = launch.Model.Heat;
        SendMouse(true); Advance(.1); SendMouse(false);
        Check(launch.Model.Heat > releasedHeat, "Mouse unlocks after pause and can control thrust again");

        DriveToComplete();
        Check(centeredProgress.Value == 100, "Progress bar fills at the ten-second hold goal");
        Check(Math.Abs((band.AnchorRight - band.AnchorLeft) - .08) < .001,
            "The target band narrows slightly by completion");
        Check(launch.Model.Result.CenteredSeconds == 10 && mission.LaunchRecord?.CenteredSeconds == 10,
            "Real launch records completion without a score");
        Check(mission.LaunchRecord?.PerfectIgnition == true && mission.PendingLaunchFaults == 0,
            "A perfect ignition records no faults");
        Check(mission.Energy == 50 && mission.TrainingLaunchPracticeComplete,
            "Real launch has no energy penalty and keeps prior practice completion");
        var successFire = launch.GetNode<AnimatedSprite2D>("Rocket/Fire"); successFire.SpeedScale = 0;
        await Capture("success"); successFire.SpeedScale = 1;
        await WaitFor(RocketLaunch.Phase.Ascending);
        float ascentStartY = launch.GetNode<Sprite2D>("Rocket/Rocket").GlobalPosition.Y;
        launch.SetProcess(true);
        await ToSignal(GetTree().CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
        Check(launch.IsInsideTree() && launch.State == RocketLaunch.Phase.Complete &&
            launch.GetNode<Sprite2D>("Rocket/Rocket").GlobalPosition.Y < ascentStartY,
            $"Rocket visibly ascends and remains in scene without auto-return ({launch.State}, {ascentStartY:F2}->{launch.GetNode<Sprite2D>("Rocket/Rocket").GlobalPosition.Y:F2})");
        launch.QueueFree(); launch = null; await Frames();

        mission.BeginLaunchSequence(); mission.Energy = 50;
        CompleteCoreTraining(mission);
        PracticeSession.RequestLaunch(PracticeSession.LaunchMode.Practice, new Vector2(12, 34));
        await Load(); await Capture("practice-ready");
        Check(launch.GetNode<Background>("Background").Texture.ResourcePath.EndsWith("launching_background.jpeg"),
            "Practice falls back to the launch-site background when no practice texture is assigned");
        Check(launch.GetNode<TextureRect>(BriefingUi + "Tutorial").Visible,
            "Simulator keeps visual launch instructions");
        Check(!mission.LaunchPadUnlocked, "Practice remains the final pad-unlock requirement");
        Check(launch.Practice && launch.Model.Config == new LaunchHoldConfig(), "Practice uses the exact launch requirement");
        Check(launch.Model.TargetConfig.StartSpeed == 3 && launch.Model.TargetConfig.MaxSpeed == 8 &&
            launch.Model.TargetConfig.EndWidth == 8,
            "Practice shares the moving, gradually narrowing target profile");
        Start(); CompleteIgnition(); DriveToComplete();
        Check(launch.GetNode<ProgressBar>(Ui + "CenteredProgress").Value == 100,
            "Simulator progress bar also completes at its hold goal");
        Check(Math.Abs(launch.Model.TargetWidth - 8) < .01,
            "Simulator target also narrows by the end of the hold");
        Check(mission.TrainingLaunchPracticeComplete && mission.LaunchPadUnlocked,
            "Completing practice fulfills the final training requirement");
        Check(mission.LaunchRecord == null && mission.Energy == 50 && launch.State == RocketLaunch.Phase.Result,
            "Practice clears training without recording or launching the mission");
        var practiceResult = launch.Model;
        SendKey(Key.Space, true); SendKey(Key.Space, false);
        Check(launch.State == RocketLaunch.Phase.Result && ReferenceEquals(launch.Model, practiceResult),
            "Space cannot restart a completed simulator run");
        Check(launch.GetNodeOrNull<Button>(Ui + "Actions/Retry") == null,
            "Completed practice has no retry button");
        await WaitFor(RocketLaunch.Phase.Ascending);
        float practiceAscentStartY = launch.GetNode<Sprite2D>("Rocket/Rocket").GlobalPosition.Y;
        launch.Notification((int)Node.NotificationApplicationFocusOut);
        Check(GetTree().Paused && launch.Paused && GetNode<PauseManager>("/root/PauseManager").IsOpen,
            "Focus loss during practice ascent opens an accessible pause menu");
        GetNode<PauseManager>("/root/PauseManager").Close();
        Check(!launch.Paused && !GetTree().Paused, "Visible resume clears the ascent pause");
        launch.SceneLoader = _ => Error.CantOpen;
        launch.SetProcess(true);
        await ToSignal(GetTree().CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
        Check(launch.IsInsideTree() && launch.State == RocketLaunch.Phase.Complete &&
            launch.GetNode<Sprite2D>("Rocket/Rocket").GlobalPosition.Y < practiceAscentStartY,
            "Practice visibly completes ascent before returning to the gym");
        Check(launch.GetNodeOrNull<Button>(Ui + "Actions/Continue") == null,
            "Repeat practice button is removed");
        Check(launch.GetNode<Button>(Ui + "Actions/Back").IsVisibleInTree() &&
            launch.GetNode<Label>(Ui + "Text").Text.Contains("Unable to return"),
            "Failed automatic return exposes a visible manual recovery action");
        int returns = 0;
        launch.SceneLoader = _ => { returns++; return Error.Ok; };
        launch.GetNode<Button>(Ui + "Actions/Back").EmitSignal(BaseButton.SignalName.Pressed);
        Check(returns == 1 && mission.LaunchRecord == null,
            "Manual return retries travel without replaying or recording the simulator");
        PracticeSession.Abort();
        await Capture("practice-complete");

        mission.BeginLaunchSequence();
        Check(!mission.LaunchPadUnlocked && !mission.TrainingLaunchPracticeComplete,
            "New sequence resets all training requirements");
        PracticeSession.RequestLaunch(PracticeSession.LaunchMode.Practice, Vector2.Zero);
        mission.BeginLaunchSequence();
        await Load();
        Check(!launch.Practice && launch.State == RocketLaunch.Phase.Result &&
            launch.GetNode<Label>(Ui + "Text").Text.Contains("LAUNCH PAD LOCKED"),
            "Launch request from an old mission is rejected");
    }

    private async Task PartialIgnitionLaunch()
    {
        var mission = MissionState.Instance;
        mission.BeginLaunchSequence();
        CompleteAllTraining(mission);
        await Load();
        Start();
        int minimum = launch.Ignition.Config.MinimumClicks;
        for (int i = 0; i < minimum; i++)
        {
            SendKey(Key.Space, true);
            SendKey(Key.Space, false);
        }
        Check(launch.Ignition.MeetsMinimum && !launch.Ignition.Complete,
            "The eighty-five percent minimum is reached without a perfect ignition");
        Advance(5);
        Check(launch.State == RocketLaunch.Phase.Active,
            "A partial ignition still lifts the rocket into the hold phase");
        Check(launch.GetNode<AnimatedSprite2D>("Rocket/Fire").Animation != "failed",
            "A partial ignition does not play the failure animation");
        DriveToComplete();
        Check(launch.RunResult != null && launch.RunResult.IgnitionFaults == 3 &&
            Math.Abs(launch.RunResult.IgnitionRatio - .88) < .0001,
            $"A partial launch records the missing taps as faults ({launch.RunResult?.IgnitionFaults})");
        Check(mission.LaunchRecord?.IgnitionFaults == 3 && mission.PendingLaunchFaults == 3,
            "The mission keeps the pending faults for later repair work");
        Check(launch.GetNode<Label>(Ui + "Stats").Text.Contains("IGNITION FAULTS"),
            "The launch summary shows the ignition faults");
        launch.QueueFree(); launch = null; await Frames();
        mission.ResetEquipment();
    }
}

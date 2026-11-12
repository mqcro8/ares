using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.training;
using Ares.Systems.Audio;

/// <summary>Real scene swaps, input, collision routes and puzzle solutions. --visual saves rendered frames.</summary>
public partial class TrainingPracticeTest : Node
{
    private int _assertions;
    private Vector2 _returnPoint;
    private long _exerciseTicket, _exerciseSession;
    private bool Visual => OS.GetCmdlineUserArgs().Contains("--visual");
    private Node Scene => GetTree().CurrentScene;
    private TrainingIntro Gym => (TrainingIntro)Scene;
    private Player Player => Scene is TrainingLab lab ? lab.Player : Gym.GetNode<Player>(Gym.PlayerPath);
    public override async void _Ready()
    {
        try
        {
            GetTree().CurrentScene = null;
            await Frames(2); await Run();
            GD.Print($"PRACTICE INTEGRATION PASS ({_assertions} assertions)"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Check(bool condition, string message)
    {
        _assertions++; if (!condition) throw new InvalidOperationException(message);
    }
    private async Task Frames(int count = 4)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Capture(string name)
    {
        if (!Visual) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/corrected-{name}.png") == Error.Ok, "Screenshot saved");
    }
    private void EnglishUi(Node node)
    {
        bool valid = true;
        void Inspect(Node parent)
        {
            foreach (Node child in parent.GetChildren())
            {
                string text = child is Label label ? label.Text : child is Button button ? button.Text : "";
                valid &= !new[] { "Bienvenido", "Iniciar", "Cancelar", "CABLES", "FUSIBLES", "Pulsaciones", "DOBLE", "Práctica", "Continuar", "Par " }.Any(text.Contains);
                Inspect(child);
            }
        }
        Inspect(node); Check(valid, "No previous Spanish UI remains");
    }
    private void KeyPress(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void KeyState(Key key, bool down)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down });
        Input.FlushBufferedEvents();
    }
    private void CompleteLaunchPractice(RocketLaunch launch)
    {
        double now = Time.GetTicksUsec() / 1_000_000.0;
        launch.Clock = () => now;
        launch.GetNode<Button>("IntroUI/Briefing/Margin/VBox/Actions/Start").EmitSignal(BaseButton.SignalName.Pressed);
        for (int i = 0; i < launch.Ignition.Config.TargetClicks; i++) KeyPress(Key.Space);
        Check(launch.State == RocketLaunch.Phase.Active && launch.Ignition.Complete,
            "Simulator ignition taps unlock the hold phase");
        bool held = false;
        for (int i = 0; i < 4000 && !launch.Model.Complete; i++)
        {
            bool want = launch.Model.Heat < 50;
            if (want != held) { KeyState(Key.Space, want); held = want; }
            now += .01;
            launch._Process(.01);
        }
        if (held) KeyState(Key.Space, false);
        Check(launch.State == RocketLaunch.Phase.Result && launch.Model.Result?.CenteredSeconds == 10,
            "Practice finishes after ten centered seconds");
        Check(ares.scripts.mission.MissionState.Instance.TrainingLaunchPracticeComplete,
            "Completed simulator marks its training requirement");
    }
    private static T Find<T>(Node node, Func<T, bool> predicate) where T : Node
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is T match && predicate(match)) return match;
            var nested = Find(child, predicate); if (nested != null) return nested;
        }
        return null;
    }
    private async Task Start()
    {
        Check(!Scene.IsProcessingInput(), "Board inactive before Start");
        KeyPress(Key.Key1);
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
        Input.FlushBufferedEvents();
        await Frames(); Check(!Scene.IsProcessingInput(), "Held Start input cannot reach board");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }); Input.FlushBufferedEvents();
        await Frames(); Check(Scene.IsProcessingInput(), "Board enabled after Start release");
    }
    private async Task Enter(PracticeSession.Exercise exercise)
    {
        var lab = (TrainingLab)Scene;
        var zone = exercise switch
        {
            PracticeSession.Exercise.Wires => lab.WiresZone,
            PracticeSession.Exercise.Fuses => lab.FusesZone,
            PracticeSession.Exercise.Frequency => lab.FrequencyZone,
            PracticeSession.Exercise.HullRepair => lab.HullRepairZone,
            _ => throw new ArgumentOutOfRangeException(nameof(exercise), exercise, null)
        };
        if (exercise is PracticeSession.Exercise.Fuses or PracticeSession.Exercise.HullRepair)
            await WalkTo(new Vector2(TrainingLab.RoomWidth / 2, TrainingLab.RoomHeight / 2));
        await WalkTo(zone.GetGlobalTransform() * (zone.Size / 2));
        _returnPoint = Player.GlobalPosition;
        await Frames(); await Capture("lab-front-" + exercise); KeyPress(Key.E);
        _exerciseTicket = PracticeSession.Pending?.Id ?? 0; _exerciseSession = PracticeSession.SessionId;
        Check(_exerciseTicket != 0, "Exercise has explicit travel ticket");
        await Frames(8);
        Check(Scene.SceneFilePath == PracticeSession.SceneFor(exercise), "E opens original minigame directly");
        EnglishUi(Scene);
        var tutorial = Find<TextureRect>(Scene.GetNode<PracticeReturn>("PracticeReturn"), node => node.Name == "Tutorial");
        string expectedTutorial = exercise switch
        {
            PracticeSession.Exercise.Wires => "wires/cable_connection_tutorial.png",
            PracticeSession.Exercise.Fuses => "fuses/fuse_switch_tutorial.png",
            PracticeSession.Exercise.Frequency => "frequency/frequency_tutorial.png",
            PracticeSession.Exercise.HullRepair => "hull_repair/hull_repair_tutorial.png",
            _ => throw new ArgumentOutOfRangeException(nameof(exercise), exercise, null)
        };
        Check(tutorial?.Texture?.ResourcePath.EndsWith(expectedTutorial) == true && tutorial.Visible,
            "Practice instructions show the matching reference image");
        if (Visual)
        {
            var window = GetWindow();
            var oldSize = window.ContentScaleSize;
            var oldMode = window.ContentScaleMode;
            window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            window.ContentScaleSize = new Vector2I(640, 480);
            await Frames(8);
            var bounds = GetViewport().GetVisibleRect();
            bool Fits(Control c) => bounds.Encloses(c.GetGlobalRect());
            Check(Fits(Find<PanelContainer>(Scene.GetNode("PracticeReturn"), _ => true)), "Instructions fit 640x480 logical viewport");
            Check(Fits(Find<TextureRect>(Scene.GetNode<PracticeReturn>("PracticeReturn"), node => node.Name == "Tutorial")),
                "Tutorial reference fits small viewport");
            if (Scene is WireMinigame)
                Check(Fits(Scene.GetNode<Control>("LeftTerminals")) && Fits(Scene.GetNode<Control>("RightTerminals")), "Both wire columns fit small viewport");
            else if (Scene is FuseBoxMinigame || Scene is FrequencyBoxMinigame)
                Check(Fits(Scene.GetNode<Control>("Box")), "Minigame board fits small viewport");
            else if (Scene is HullRepairMinigame)
                Check(Fits(Scene.GetNode<Control>("Frame")), "Hull-repair board fits small viewport");
            await Capture(exercise + "-640x480");
            window.ContentScaleSize = oldSize; window.ContentScaleMode = oldMode;
            await Frames(8);
        }
        await Capture(exercise + "-instructions");
        await Start(); await Capture(exercise + "-board");
    }
    private async Task Back(bool success)
    {
        await Frames(10);
        Check(Scene is TrainingLab, "Return to lab, not gym");
        Check(Player.GlobalPosition.DistanceTo(_returnPoint) < 0.1f,
            $"Return to exact original station-side position: got {Player.GlobalPosition}, ticket {_returnPoint}");
        Check(PracticeSession.Pending == null, "Travel consumed once");
        Check(!PracticeSession.Finish(_exerciseSession, _exerciseTicket, true), "Consumed completion ticket rejects duplicate/late callback");
        Check(PracticeSession.Feedback.StartsWith(success ? "Practice complete." : "Practice cancelled."), "Success/cancel feedback");
    }
    private async Task CancelActivePractice()
    {
        var practiceReturn = Scene.GetNode<PracticeReturn>("PracticeReturn");
        var cancel = Find<Button>(practiceReturn, button => button.Visible &&
            (button.Text.StartsWith("Cancel and return") || button.Text.StartsWith("Cancel / restart")));
        Check(cancel != null, "Active minigame exposes an explicit cancel button");
        cancel.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(8);
    }
    private async Task Run()
    {
        var mission = ares.scripts.mission.MissionState.Instance;
        var initialMission = (mission.Stage, mission.MissionTime, mission.Budget, mission.Mass,
            mission.Energy, mission.Communications, mission.SpareParts, mission.RoverImpacts, mission.RoverCollisions);
        Check(GetTree().ChangeSceneToFile(PracticeSession.TrainingScene) == Error.Ok, "Load gym"); await Frames(8);
        Check(Gym.GetNode("Tilemaps_layers/Furniture").Owner == Gym && Gym.Course.Owner == Gym,
            "Furniture and course are editable gym-owned nodes, not wrapper instances");
        Check(Gym.Course.IsConfigured && Gym.Monitor != null && Player.TrainingMovement.IsConfigured,
            "Inline course, monitor and movement exports resolve");
        Check(Gym.LabReturnMarker.Owner == Gym && Gym.LabDoorZone.Owner == Gym,
            "Gym door zone and return marker are scene-owned");
        Check(Gym.BriefingActive && !Gym.Monitor.IsDisplayed, "Welcome hides the mission monitor");
        Check(Find<Label>(Scene, l => l.Text.StartsWith("CIRCUIT") && l.IsVisibleInTree()) == null,
            "Welcome hides monitor objectives");
        await Capture("gym-welcome");
        EnglishUi(Scene);
        for (int i = 0; i < 3; i++) { KeyPress(Key.Enter); await Frames(); }
        Check(!Gym.BriefingActive, "Briefing closes");
        Check(Gym.Monitor.IsDisplayed && Gym.Monitor.IsExpanded, "Monitor starts expanded in the gym");
        Check(Gym.Monitor.PromptText.Contains("WASD: move") && Gym.Monitor.PromptText.Contains("E: interact"),
            "Gym repeats movement and interaction controls after briefing");
        KeyPress(Key.Q); await Frames();
        Check(!Gym.Monitor.IsExpanded, "Q collapses the expanded objective monitor");
        KeyPress(Key.Q); await Frames();
        Check(Gym.Monitor.IsExpanded, "Q expands the objective monitor again");
        await Capture("gym-monitor-expanded");
        await WalkTo(Gym.LabReturnMarker.GlobalPosition); await Capture("gym-door");
        var shadow = Player.GetNode<Polygon2D>("JumpShadow");
        Vector2 shadowOrigin = shadow.Position;
        Check(shadow.Visible, "Gym shadow visible at rest");
        Check(Player.GetNode<Node2D>("AnimatedSprite2D").Scale == Vector2.One * 1.5f && Player.Scale == Vector2.One,
            "Gym reduces visual only, not physics body");
        await ScreenSizeChecks("gym");
        Input.ActionPress("training_jump");
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("training_jump");
        Check(!Player.TrainingMovement.Grounded, "Real jump starts");
        Check(shadow.Visible && shadow.Position == shadowOrigin, "Jump shadow stays on ground");
        await Frames(); KeyPress(Key.E); await Frames(); Check(Scene is TrainingIntro, "Airborne travel forbidden");
        for (int i = 0; i < 120 && !Player.TrainingMovement.Grounded; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Cross(2); Check(Gym.Course.ClearedCount == 1, "Partial reverse circuit");
        var visual = Player.GetNode<PlayerAnimation>("AnimatedSprite2D"); visual.RestoreFacing("side", true);
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true }); Input.FlushBufferedEvents();
        await Frames(8);
        Check(Scene is TrainingLab, "Gym door enters separate lab");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true, Echo = true }); Input.FlushBufferedEvents();
        await Frames(8); Check(Scene is TrainingLab, "Held E cannot immediately return through entrance");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = false }); Input.FlushBufferedEvents();
        var saved = PracticeSession.Saved;
        Check(saved.Course.Direction != 0 && saved.Course.Cleared == 1 && saved.Movement.Stamina < 100, "Partial course and stamina saved");
        Check(Player.Position.DistanceTo(TrainingLab.DefaultSpawn) < 1, "Room-specific entrance, never gym position");
        Check(TrainingLab.RoomWidth == 768f && TrainingLab.RoomHeight == 432f,
            "Lab matches the gym spawn-room footprint");
        var lab = (TrainingLab)Scene;
        Check(Player.GetNode<Polygon2D>("GroundShadow").Visible &&
            Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Scale == Vector2.One * 1.5f &&
            Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Position == new Vector2(1.5f, -16.5f) &&
            lab.Camera.Zoom == Vector2.One * 1.5f,
            "Lab uses the same player transform and fixed camera zoom as the gym");
        Check(!lab.HasNode("Decoration") && Find<Node>(lab, n => n.Name.ToString().StartsWith("Supervision")) == null &&
            lab.Locker != null && lab.Locker.Contents != null && lab.Locker.Contents.Contains(lab.Locker.StarterKit),
            "No orphan decoration; supply locker starts with the starter kit");
        Check(lab.GetNode<NinePatchRect>("Floor").RegionRect == new Rect2(192, 192, 16, 16)
            && lab.GetNode<NinePatchRect>("Floor").Material == null, "Floor uses gym atlas, no checker shader");
        Check(lab.GetNode<Node2D>("FuseCabinet").Position == new Vector2(648, 184),
            "Fuse cabinet stays in its earlier upper-right position");
        foreach (string wall in new[] { "North", "South", "West", "East" })
        {
            var body = lab.GetNode<StaticBody2D>("Walls/" + wall);
            Check(body.GetChildCount() == 2 && body.GetNode<NinePatchRect>("Visual").Texture.ResourcePath.EndsWith("Room_Builder_free_16x16.png")
                && body.GetNode<CollisionShape2D>("CollisionShape2D").Shape is RectangleShape2D, "Atlas wall retains single collision: " + wall);
        }
        await ScreenSizeChecks("lab");
        Check(lab.Player.Owner == lab && lab.Camera.Owner == lab && lab.GetNode("Floor").Owner == lab,
            "Lab player, camera and compact floor exist in the editable scene");
        Check(new[] { lab.WiresZone, lab.FusesZone, lab.FrequencyZone, lab.HullRepairZone,
            lab.GymZone, lab.SimulatorZone, lab.LaunchPadZone }.All(zone => zone.Owner == lab)
            && lab.HasNode("lab_from_gym"), "Lab interaction zones and gym entrance marker resolve");
        Check(lab.Monitor.IsDisplayed && lab.Monitor.StaminaVisible && lab.Monitor.IsExpanded &&
            lab.Monitor.ObjectiveLabels.Any(label => label == "MAINTENANCE KIT") &&
            lab.GetNode<Label>("Player/HotbarLayer/CargoStatus") != null,
            "Lab monitor shows preserved stamina/objectives and cargo remains beside the hotbar");
        Check(lab.Monitor.GetNode<NinePatchRect>("Frame").SelfModulate.A < 1f,
            "Expanded mission panel lets the room show through");
        await Capture("lab-entry");
        EnglishUi(Scene);
        if (Visual)
        {
            var oldSize = GetWindow().Size;
            var oldScaleSize = GetWindow().ContentScaleSize;
            var oldScaleMode = GetWindow().ContentScaleMode;
            // The tiling desktop can ignore OS window sizes. Render the root viewport
            // at each requested resolution instead (no embedded game viewport).
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1024, 768), new Vector2I(640, 480) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Frames(8);
                Check(GetViewport().GetTexture().GetSize() == (Vector2)size, "Requested render resolution applied");
                var view = GetViewport().GetVisibleRect(); var transform = ((Node2D)Scene).GetCanvasTransform();
                Check(lab.Camera.Zoom == Vector2.One * 1.5f &&
                    view.HasPoint(transform * new Vector2(TrainingLab.RoomWidth / 2, TrainingLab.RoomHeight / 2)),
                    "Lab keeps the gym camera zoom centered on the room after resize");
                Color fill = lab.GetNode<ColorRect>("Background/Fill").Color;
                Check(fill.R + fill.G + fill.B > 0.6f, "Lab exterior fill is not black");
                await Capture($"lab-{size.X}x{size.Y}");
            }
            GetWindow().ContentScaleSize = oldScaleSize; GetWindow().ContentScaleMode = oldScaleMode;
            GetWindow().Size = oldSize; await Frames(8);
        }
        // Verify cabinet bases with actual collision queries, not only visual rectangles.
        var labScene = (TrainingLab)Scene;
        var workbenchBase = labScene.GetNode<StaticBody2D>("WiringWorkbench/Base");
        var fuseBase = labScene.GetNode<StaticBody2D>("FuseCabinet/Base");
        Check(Player.TestMove(new Transform2D(0, workbenchBase.GlobalPosition + new Vector2(0, 56)), new Vector2(0, -70)), "Workbench base is solid");
        Check(Player.TestMove(new Transform2D(0, fuseBase.GlobalPosition + new Vector2(0, 56)), new Vector2(0, -70)), "Fuse cabinet base is solid");
        Player.GlobalPosition = labScene.GetNode<Node2D>("WiringWorkbench").GlobalPosition + new Vector2(0, -30);
        await Frames(); KeyPress(Key.E); await Frames();
        Check(Scene is TrainingLab, "Station unavailable from behind");
        Player.GlobalPosition = labScene.WiresZone.GetGlobalTransform() * (labScene.WiresZone.Size / 2); await Frames();
        KeyPress(Key.E); await Frames();
        Check(Scene is TrainingLab, "Repair station rejects a loadout without the maintenance kit");
        var lockerZone = labScene.Locker.FrontZone;
        await WalkTo(lockerZone.GetGlobalTransform() * (lockerZone.Size / 2)); await Frames();
        KeyPress(Key.E); await Frames();
        Check(Player.Inventory.Contains(labScene.Locker.StarterKit), "Supply locker transfers the maintenance kit");
        Check(Player.SelectedStack?.Item == labScene.Locker.StarterKit, "Maintenance kit is selected in the hotbar");
        var kitSlot = Player.Hotbar.GetChild<HotbarSlot>(0);
        var kitIcon = kitSlot.GetNode<TextureRect>("Icon");
        Check(kitIcon.Texture == labScene.Locker.StarterKit.Icon && kitIcon.Size == new Vector2(12, 12),
            "Hotbar centers the kit icon inside its inset slot bounds");
        Check(ares.scripts.mission.MissionState.Instance.CargoUsed == labScene.Locker.StarterKit.CargoCost,
            "Kit consumes cargo capacity, not an extra hotbar slot");
        Player.GlobalPosition = labScene.WiresZone.GetGlobalTransform() * (labScene.WiresZone.Size / 2); await Frames();
        KeyPress(Key.Key2); await Frames(); KeyPress(Key.E); await Frames();
        Check(Scene is TrainingLab, "Repair station rejects an unselected maintenance kit");
        KeyPress(Key.Key1); await Frames();
        Check(Player.SelectedStack?.Item == labScene.Locker.StarterKit, "Hotbar selection returns to the kit");
        await Enter(PracticeSession.Exercise.Wires); await CancelActivePractice(); await Back(false);
        Check(!PracticeSession.Wires, "Cancel does not complete wires");
        await Enter(PracticeSession.Exercise.Wires); SolveWires(); await Back(true);
        Check(PracticeSession.Wires, "Real keyboard wire solution completes");
        Check(Player.Inventory.Contains(((TrainingLab)Scene).Locker.StarterKit) &&
            ares.scripts.mission.MissionState.Instance.CargoUsed == ((TrainingLab)Scene).Locker.StarterKit.CargoCost,
            "Kit and cargo survive the minigame scene swap");
        await Enter(PracticeSession.Exercise.Wires); await CancelActivePractice(); await Back(false);
        Check(PracticeSession.Wires, "Repeat cancel keeps prior completion");
        await Enter(PracticeSession.Exercise.Fuses);
        for (int i = 0; i < 4; i++) { KeyPress(Key.Key1); await Frames(2); }
        await CancelActivePractice(); await Back(false);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        Check(!PracticeSession.Fuses, "Reset timer cancellation safe");
        await Enter(PracticeSession.Exercise.Fuses); await SolveFuses(); await CancelActivePractice(); await Back(false);
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        Check(!PracticeSession.Fuses, "Success timer cancellation safe");
        await Enter(PracticeSession.Exercise.Fuses); await SolveFuses();
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout); await Back(true);
        Check(PracticeSession.Fuses && PracticeSession.Wires, "Both repairs completed");

        await Enter(PracticeSession.Exercise.Frequency);
        var frequency = (FrequencyBoxMinigame)Scene;
        Check(frequency.MatchThreshold <= 0.9f && frequency.HoldSeconds <= 0.35f && frequency.KeyAdjustSpeed >= 1f,
            "Frequency calibration uses forgiving alignment and adjustment settings");
        KeyPress(Key.Key3); await Frames();
        Check(frequency.SelectedKnob == 2, "Number keys select calibration controls directly");
        float phaseValue = frequency.SelectedValue;
        KeyState(Key.Right, true); await Frames(10); KeyState(Key.Right, false); await Frames();
        Check(frequency.SelectedValue > phaseValue, "Arrow keys adjust selected calibration control without Enter");
        KeyPress(Key.Escape);
        Check(GetTree().Paused && Scene is FrequencyBoxMinigame && !AudioManager.IsMusicPaused &&
            !AudioManager.IsActiveMusicStreamPaused,
            "Escape pauses the game but keeps music playing during frequency calibration");
        KeyPress(Key.Escape);
        Check(!GetTree().Paused && Scene is FrequencyBoxMinigame && !AudioManager.IsMusicPaused &&
            AudioManager.Instance?.CurrentMusicId == null,
            "Escape removes music when pause closes without cancelling its ticket");
        await CancelActivePractice(); await Back(false);
        Check(!mission.TrainingFrequencyComplete, "Cancelling frequency calibration keeps it incomplete");

        await Enter(PracticeSession.Exercise.HullRepair);
        await CancelActivePractice(); await Back(false);
        Check(!mission.TrainingHullRepairComplete, "Cancelling hull repair keeps it incomplete");

        await WalkTo(new Vector2(TrainingLab.RoomWidth / 2, TrainingLab.RoomHeight / 2));
        var simulatorZone = ((TrainingLab)Scene).SimulatorZone;
        await WalkTo(simulatorZone.GetGlobalTransform() * (simulatorZone.Size / 2));
        await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is RocketLaunch && ((RocketLaunch)Scene).Practice && PracticeSession.Pending == null, "Simulator opens the practice launch without a stale travel ticket");
        CompleteLaunchPractice((RocketLaunch)Scene);
        for (int i = 0; i < 90 && Scene is RocketLaunch; i++) await Frames();
        Check(Scene is TrainingLab, "Completed launch practice returns to the lab automatically");
        Check(((TrainingLab)Scene).GetNode<Sprite2D>("LaunchSimulator/Sprite2D").Texture.ResourcePath ==
            "res://art/training/stations/launch_simulator_console.png", "Simulator uses the new front-facing console sprite");
        var lockedPadZone = ((TrainingLab)Scene).LaunchPadZone;
        await WalkTo(lockedPadZone.GetGlobalTransform() * (lockedPadZone.Size / 2));
        await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingLab && !mission.LaunchPadUnlocked, "Launch pad stays locked until the course is complete");
        await Capture("lab-completed-repairs");
        Check(PracticeSession.Saved == saved, "Gym stamina/rest and course frozen outside gym");
        var labStations = ((TrainingLab)Scene).GetNode<TrainingStations>("PracticeStations");
        var gymTarget = labStations.Targets.First(target => target.Name == "Return to gym");
        await WalkTo(gymTarget.Center); await Frames();
        KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && !Gym.BriefingActive, "Return to gym skips welcome");
        Check(Player.Position.DistanceTo(Gym.LabReturnMarker.GlobalPosition) < 1, "Gym return marker");
        Check(Gym.Course.Capture() == saved.Course, "Count and direction restored");
        Check(Mathf.Abs(Player.TrainingMovement.Stamina - saved.Movement.Stamina) < 1, "Stamina restored");
        Check(Player.GetNode<PlayerAnimation>("AnimatedSprite2D").Facing == "side" && Player.GetNode<PlayerAnimation>("AnimatedSprite2D").FlipH, "Orientation restored");
        await WalkTo(new(400, 510)); await Frames(); await Capture("gym-circuit-hint");
        Check(Gym.Monitor.PromptText.Contains("SPACE: jump"), "Circuit prompt explains the jump control");
        await WalkTo(Gym.LabReturnMarker.GlobalPosition); await Frames();
        Cross(1); Check(Gym.Course.ClearedCount == 2, "Reverse direction survives travel");
        Cross(0); await Frames(); Check(Gym.CompletionShown && Gym.Course.Congratulated, "Final crossing congratulates once");
        Check(Gym.GetNode<Label>(Gym.TextPath).Text.Contains("All training is complete"),
            "Mission Control completion notice waits for the final requirement");
        await Capture("gym-all-training-complete");
        Gym.GetNode<Button>(Gym.ContinuePath).EmitSignal(BaseButton.SignalName.Pressed); await Frames(8);
        Check(Scene is TrainingIntro && !Gym.BriefingActive, "Course completion does not launch automatically");
        await WalkTo(Gym.LabReturnMarker.GlobalPosition); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingLab && mission.LaunchPadUnlocked, "All four training requirements unlock the pad");
        Check(((TrainingLab)Scene).SimulatorZone != null && ((TrainingLab)Scene).LaunchPadZone != null, "Lab exposes launch stations");
        await Capture("lab-launch-unlocked");
        var launchZone = ((TrainingLab)Scene).LaunchPadZone;
        await WalkTo(launchZone.GetGlobalTransform() * (launchZone.Size / 2)); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is RocketLaunch && !((RocketLaunch)Scene).Practice && PracticeSession.Pending == null, "Unlocked launch pad opens the real launch without a stale travel ticket");
        Check(!((RocketLaunch)Scene).GetNode<TextureRect>("IntroUI/Briefing/Margin/VBox/Tutorial").Visible,
            "Real launch omits the completed practice tutorial");
        await Capture("launch-pad-open");
        Find<Button>(Scene, b => b.Text.StartsWith("Return to lab") && b.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed); await Frames(8);
        Check(Scene is TrainingLab, "Manual launch returns to the lab");
        await WalkTo(new Vector2(TrainingLab.RoomWidth / 2, TrainingLab.RoomHeight / 2));
        var gymZone = ((TrainingLab)Scene).GymZone;
        await WalkTo(gymZone.GetGlobalTransform() * (gymZone.Size / 2)); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && Gym.CompletionShown && !Gym.BriefingActive,
            $"Completion greeting not repeated (scene={Scene.SceneFilePath}, shown={(Scene is TrainingIntro intro ? intro.CompletionShown : false)}, active={(Scene is TrainingIntro activeIntro ? activeIntro.BriefingActive : false)}, saved={PracticeSession.Saved?.CompletionShown})");
        Check(GetTree().ChangeSceneToFile(PracticeSession.SceneFor(PracticeSession.Exercise.Wires)) == Error.Ok, "Standalone wire load"); await Frames();
        Check(PracticeSession.Saved == null && PracticeSession.Wires, "External exit clears session but keeps repairs");
        await Start();
        var standalone = (WireMinigame)Scene; int completions = 0; standalone.Completed += () => completions++;
        foreach (ColorRect left in standalone.GetNode("LeftTerminals").GetChildren())
        {
            var target = standalone.GetNode("RightTerminals").GetChildren().Cast<ColorRect>().First(r => r.Color == left.Color);
            Mouse(left.GetGlobalRect().GetCenter(), true); await Frames(2); Mouse(target.GetGlobalRect().GetCenter(), false); await Frames(2);
        }
        Check(completions == 1 && Scene == standalone, "Standalone mouse solve emits once without training travel");
        GetTree().ChangeSceneToFile(PracticeSession.SceneFor(PracticeSession.Exercise.Fuses)); await Frames(); await Start();
        var standaloneFuse = (FuseBoxMinigame)Scene; int fuseCompletions = 0; standaloneFuse.Completed += () => fuseCompletions++;
        await SolveFuses(); await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        Check(fuseCompletions == 1 && Scene == standaloneFuse, "Standalone fuse completion independent");
        Check(!PracticeSession.Finish(PracticeSession.SessionId - 1, 1, true), "Late callback rejected");
        Check(!PracticeSession.Finish(PracticeSession.SessionId, 0, true), "Duplicate callback rejected");
        // An external scene change between a callback and its deferred return must
        // invalidate even an already-prepared return ticket.
        GetTree().ChangeSceneToFile(PracticeSession.LabScene); await Frames(8);
        await Enter(PracticeSession.Exercise.Wires);
        Scene.EmitSignal(WireMinigame.SignalName.Completed);
        Check(PracticeSession.Pending?.Target == PracticeSession.LabScene, "Completion prepares return ticket");
        GetTree().ChangeSceneToFile(PracticeSession.TrainingScene); await Frames(8);
        Check(Gym.BriefingActive && PracticeSession.Pending == null && PracticeSession.Wires, "External exit discards deferred return but keeps repairs");
        // First visit to the gym from a directly launched lab has no saved circuit.
        GetTree().ChangeSceneToFile(PracticeSession.LabScene); await Frames(8);
        await Enter(PracticeSession.Exercise.Wires);
        var returnUi = Scene.GetNode<PracticeReturn>("PracticeReturn");
        returnUi.SceneLoader = _ => Error.CantOpen;
        bool printErrors = Engine.PrintErrorMessages;
        try
        {
            Engine.PrintErrorMessages = false;
            SolveWires(); await Frames(8);
        }
        finally { Engine.PrintErrorMessages = printErrors; }
        Check(Scene is WireMinigame && !Scene.IsProcessingInput(), "Failed return keeps resolved board locked");
        Check(PracticeSession.Wires && PracticeSession.Pending != null, "Failed return preserves success and its ticket");
        long retryTicket = PracticeSession.Pending.Id;
        Check(Find<Button>(Scene, b => b.Text.StartsWith("Return failed") && b.HasFocus()) != null, "Retry button has keyboard focus");
        returnUi.SceneLoader = path =>
        {
            Check(PracticeSession.Pending.Id == retryTicket, "Retry reuses result without completing twice");
            var tree = GetTree();
            Callable.From(() => tree.ChangeSceneToFile(path)).CallDeferred();
            return Error.Ok;
        };
        Find<Button>(Scene, button => button.Text.StartsWith("Return failed") && button.HasFocus())
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Back(true);
        Check(PracticeSession.Saved == null, "Direct lab visit has no gym snapshot");
        long labSession = PracticeSession.SessionId;
        var directGymZone = ((TrainingLab)Scene).GymZone;
        await WalkTo(directGymZone.GetGlobalTransform() * (directGymZone.Size / 2)); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && PracticeSession.SessionId == labSession && PracticeSession.Wires, "First gym arrival preserves lab repairs and session");
        Check(Player.Position.DistanceTo(Gym.LabReturnMarker.GlobalPosition) < 1, "First gym arrival uses door marker");
        Check(Gym.Course.ClearedCount == 0 && Gym.BriefingActive, "First gym visit initializes course and welcome");
        for (int i = 0; i < 3; i++) { KeyPress(Key.Enter); await Frames(); }
        await WalkTo(Gym.LabDoorZone.GetGlobalTransform() * (Gym.LabDoorZone.Size / 2));
        KeyPress(Key.E); await Frames(8); Check(Scene is TrainingLab, "Gym door remains manually accessible");
        var gymReturnZone = ((TrainingLab)Scene).GymZone;
        await WalkTo(gymReturnZone.GetGlobalTransform() * (gymReturnZone.Size / 2));
        KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && !Gym.BriefingActive && PracticeSession.Wires, "Subsequent gym arrival skips welcome and keeps repairs");
        Check(initialMission == (mission.Stage, mission.MissionTime, mission.Budget, mission.Mass,
            mission.Energy, mission.Communications, mission.SpareParts, mission.RoverImpacts, mission.RoverCollisions), "No mission changes");
        await PhysicalRoutes();

        mission.BeginLaunchSequence();
        mission.TrainingCourseComplete = mission.TrainingWiresComplete = mission.TrainingFusesComplete = true;
        mission.EvaluateTrainingCompletion();
        Check(!mission.TrainingCompletionPending, "Simulator remains a required fourth check");
        mission.TrainingLaunchPracticeComplete = true;
        mission.EvaluateTrainingCompletion();
        Check(mission.TrainingCompletionPending, "Final simulator completion queues one mission notice");
        GetTree().ChangeSceneToFile(PracticeSession.LabScene); await Frames(8);
        Check(Scene is TrainingLab && ((TrainingLab)Scene).HasNode("TrainingCompletionDialog") &&
            mission.TrainingCompletionShown && mission.LaunchPadUnlocked,
            "Final lab-side requirement shows the global completion notice");
        await Capture("lab-all-training-complete");
        Find<Button>(Scene, b => b.Text == "Continue" && b.IsVisibleInTree()).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(4);
        Check(Scene is TrainingLab && ((TrainingLab)Scene).GetNode<TrainingStations>("PracticeStations").InteractionLocked == false,
            "Closing completion notice leaves player at lab to approach launch pad manually");

        mission.BeginLaunchSequence();
        PracticeSession.Clear();
        PracticeSession.Prepare("test", PracticeSession.LabScene, null, Vector2.Zero);
        GetTree().ChangeSceneToFile(PracticeSession.LabScene); await Frames(8);
        Check(Scene is TrainingLab && Player.Position.DistanceTo(TrainingLab.DefaultSpawn) < 1,
            "Lab safely uses its default spawn when travel marker is missing");
        Check(!((TrainingLab)Scene).Monitor.StaminaVisible,
            "Direct lab entry hides stamina when there is no valid training snapshot");
        await WalkTo(((TrainingLab)Scene).WiresZone.GetGlobalTransform() * (((TrainingLab)Scene).WiresZone.Size / 2));
        KeyPress(Key.E); await Frames(8);
        Check(Scene.SceneFilePath == PracticeSession.SceneFor(PracticeSession.Exercise.Wires),
            "Lab stations remain interactive after fallback arrival");
    }

    private async Task ScreenSizeChecks(string room)
    {
        var window = GetWindow(); var size = window.Size;
        var scale = window.ContentScaleSize; var mode = window.ContentScaleMode;
        var position = Player.Position;
        var visual = Player.GetNode<PlayerAnimation>("AnimatedSprite2D");
        var facing = visual.Facing; bool flip = visual.FlipH;
        var footprint = Player.GetNode<CollisionShape2D>("CollisionShape2D");
        var shape = footprint.Shape; var collisionTransform = footprint.Transform;
        Player.Position = room == "gym" ? new Vector2(320, 230)
            : new Vector2(TrainingLab.RoomWidth / 2, TrainingLab.RoomHeight / 2);
        window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
        foreach (var resolution in new[] { new Vector2I(1024, 768), new Vector2I(1280, 720), new Vector2I(1024, 768) })
        {
            window.Size = resolution; window.ContentScaleSize = resolution; await Frames(8);
            Player.Velocity = Vector2.Zero; visual.RestoreFacing("down", false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var screen = visual.GetGlobalTransformWithCanvas();
            Check(visual.Animation == "idle_down" && visual.Frame == 0, "Same unobstructed idle frame for size comparison");
            Check(Mathf.IsEqualApprox(screen.X.Length(), 2.25f) && Mathf.IsEqualApprox(screen.Y.Length(), 2.25f), "Equivalent screen art scale at " + resolution);
            Check(Player.Scale == Vector2.One && footprint.Shape == shape && footprint.Transform == collisionTransform, "Resize preserves physics footprint");
            Check(visual.Position.IsEqualApprox(new Vector2(1, -11) * visual.Scale.X), "Feet offset derives from native art without accumulation");
            await Capture($"{room}-size-{resolution.X}x{resolution.Y}");
        }
        window.ContentScaleSize = scale; window.ContentScaleMode = mode; window.Size = size;
        Player.Position = position; visual.RestoreFacing(facing, flip); await Frames(8);
    }

    private async Task PhysicalRoutes()
    {
        foreach (int direction in new[] { 1, -1 })
        {
            GetTree().ChangeSceneToFile(PracticeSession.TrainingScene); await Frames(8);
            for (int i = 0; i < 3; i++) { KeyPress(Key.Enter); await Frames(); }
            for (int step = 0; step < 3; step++)
            {
                int index = direction == 1 ? step : 2 - step;
                var movement = Player.TrainingMovement;
                var block = PhysicalTraining.WorldRect(Gym.Course.Blocks[index]);
                var body = PhysicalTraining.WorldRect(movement.Footprint);
                var offset = body.GetCenter() - Player.GlobalPosition;
                // Set up each approach, then use real input, height and MoveAndSlide across the hurdle.
                Player.GlobalPosition = new Vector2(block.GetCenter().X,
                    direction == 1 ? block.Position.Y - body.Size.Y / 2 - 12 : block.End.Y + body.Size.Y / 2 + 12) - offset;
                Player.Velocity = Vector2.Zero;
                await Frames(8);
                Check(Player.TestMove(Player.GlobalTransform, new Vector2(0, direction * 45)), "Hurdle blocks grounded approach");
                if (direction == 1 && step == 0)
                {
                    await Capture("gym-hurdle-rest");
                    if (Visual)
                    {
                        var window = GetWindow(); var size = window.Size;
                        var scale = window.ContentScaleSize; var mode = window.ContentScaleMode;
                        window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
                        foreach (var resolution in new[] { new Vector2I(1024, 768), new Vector2I(1280, 720) })
                        {
                            window.Size = resolution; window.ContentScaleSize = resolution; await Frames(8);
                            await Capture($"gym-hurdle-{resolution.X}x{resolution.Y}");
                        }
                        window.ContentScaleSize = scale; window.ContentScaleMode = mode; window.Size = size;
                    }
                }
                string action = direction == 1 ? "move_down" : "move_up";
                Input.ActionPress(action); Input.ActionPress("training_jump");
                for (int tick = 0; tick < 40; tick++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (tick == 1) Input.ActionRelease("training_jump");
                    if (tick == 12 && direction == 1 && step == 0) await Capture("gym-hurdle-jump");
                }
                Input.ActionRelease(action); Player.Velocity = Vector2.Zero;
                Check(movement.Grounded && movement.Height == 0, "Hurdle crossing lands on floor");
                Check(Gym.Course.ClearedCount == step + 1, $"Physical crossing {index} counts in direction {direction}");
                Check(Player.GetNode<Polygon2D>("JumpShadow").Visible, "Shadow remains visible after landing");
            }
        }
    }
    private void SolveWires()
    {
        var wire = (WireMinigame)Scene; var right = wire.GetNode<VBoxContainer>("RightTerminals"); int cursor = 0;
        for (int i = 0; i < wire.WireColors.Count; i++)
        {
            int target = Enumerable.Range(0, right.GetChildCount()).First(j => right.GetChild<ColorRect>(j).Color == wire.WireColors[i]);
            KeyPress((Key)((int)Key.Key1 + i));
            while (cursor != target) { KeyPress(Key.Down); cursor = (cursor + 1) % right.GetChildCount(); }
            KeyPress(Key.Enter);
        }
    }
    private async Task SolveFuses()
    {
        var fuse = (FuseBoxMinigame)Scene;
        var status = Find<Label>(fuse, l => l.Text.Contains("DOUBLE:"));
        int partner = int.Parse(status.Text.Split(' ').Last()) - 1;
        var switches = fuse.GetNode("Box/TopRow").GetChildren().Concat(fuse.GetNode("Box/BottomRow").GetChildren()).Cast<Panel>().ToArray();
        int initiallyOn = Array.FindIndex(switches, sw => sw.GetNode<Label>("PracticeNumber").Text.EndsWith("ON"));
        for (int i = 0; i < switches.Length; i++) if (i != partner && i != initiallyOn) { KeyPress((Key)((int)Key.Key1 + i)); await Frames(2); }
        Check(Find<Label>(fuse, l => l.Text == "All switches ON!") != null, $"Fuse solution actually reaches success timer: {status.Text}; on={initiallyOn}, partner={partner}; {string.Join(",", switches.Select(s => s.GetNode<Label>("PracticeNumber").Text))}");
    }
    private void Mouse(Vector2 point, bool pressed) => GetViewport().PushInput(new InputEventMouseButton
        { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
    private async Task WalkTo(Vector2 target)
    {
        string[] actions = { "move_left", "move_right", "move_up", "move_down" };
        for (int tick = 0; tick < 360; tick++)
        {
            Vector2 diff = target - Player.GlobalPosition;
            foreach (string action in actions) Input.ActionRelease(action);
            if (diff.Length() < 5) { Player.Velocity = Vector2.Zero; return; }
            // Slow down near the destination to avoid frame-rate-dependent overshoot.
            if (Mathf.Abs(diff.X) > 0.5f) Input.ActionPress(diff.X < 0 ? "move_left" : "move_right", Mathf.Min(1, Mathf.Abs(diff.X) / 15));
            if (Mathf.Abs(diff.Y) > 0.5f) Input.ActionPress(diff.Y < 0 ? "move_up" : "move_down", Mathf.Min(1, Mathf.Abs(diff.Y) / 15));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        foreach (string action in actions) Input.ActionRelease(action);
        throw new InvalidOperationException($"Route blocked: {Player.GlobalPosition} → {target}");
    }
    private void Cross(int block)
    {
        var course = Gym.Course; var footprint = Player.TrainingMovement.Footprint;
        course.BindFootprint(footprint, Player.GlobalPosition);
        var body = PhysicalTraining.WorldRect(footprint); var offset = body.GetCenter() - Player.GlobalPosition;
        var rect = PhysicalTraining.WorldRect(course.Blocks[block]);
        var start = new Vector2(rect.GetCenter().X, rect.End.Y + body.Size.Y + 2) - offset;
        var end = new Vector2(rect.GetCenter().X, rect.Position.Y - body.Size.Y - 2) - offset;
        course.JumpStarted(start, 0); course.Sweep(start, end); course.Landed(end, 0);
    }
}

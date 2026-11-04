using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.player;
using ares.scripts.training;

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
        Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/{name}.png") == Error.Ok, "Screenshot saved");
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
        await WalkTo(new Vector2(exercise == PracticeSession.Exercise.Wires ? 160 : 416, 202));
        _returnPoint = Player.GlobalPosition;
        await Frames(); await Capture("lab-front-" + exercise); KeyPress(Key.E);
        _exerciseTicket = PracticeSession.Pending?.Id ?? 0; _exerciseSession = PracticeSession.SessionId;
        Check(_exerciseTicket != 0, "Exercise has explicit travel ticket");
        await Frames(8);
        Check(Scene.SceneFilePath == PracticeSession.SceneFor(exercise), "E opens original minigame directly");
        EnglishUi(Scene);
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
            if (Scene is WireMinigame)
                Check(Fits(Scene.GetNode<Control>("LeftTerminals")) && Fits(Scene.GetNode<Control>("RightTerminals")), "Both wire columns fit small viewport");
            else Check(Fits(Scene.GetNode<Control>("Box")), "Fuse board fits small viewport");
            await Capture(exercise + "-640x480");
            window.ContentScaleSize = oldSize; window.ContentScaleMode = oldMode;
            await Frames(8);
        }
        await Capture(exercise + "-instructions");
        await Start(); await Capture(exercise + "-board");
    }
    private async Task Back(bool success, Vector2 position)
    {
        await Frames(10);
        Check(Scene is TrainingLab, "Return to lab, not gym");
        Check(Player.GlobalPosition.DistanceTo(_returnPoint) < 0.1f && Player.GlobalPosition.DistanceTo(position) < 5, "Return to exact original station-side position");
        Check(PracticeSession.Pending == null, "Travel consumed once");
        Check(!PracticeSession.Finish(_exerciseSession, _exerciseTicket, true), "Consumed completion ticket rejects duplicate/late callback");
        Check(PracticeSession.Feedback.StartsWith(success ? "Practice complete." : "Practice cancelled."), "Success/cancel feedback");
    }
    private async Task Run()
    {
        var mission = ares.scripts.mission.MissionState.Instance;
        var initialMission = (mission.Stage, mission.MissionTime, mission.Budget, mission.Mass,
            mission.Energy, mission.Communications, mission.SpareParts, mission.RoverImpacts, mission.RoverCollisions);
        Check(GetTree().ChangeSceneToFile(PracticeSession.TrainingScene) == Error.Ok, "Load gym"); await Frames(8);
        Check(Gym.GetNode("Tilemaps_layers/Furniture").Owner == Gym && Gym.Course.Owner == Gym,
            "Furniture and course are editable gym-owned nodes, not wrapper instances");
        Check(Gym.Course.IsConfigured && Gym.Course.Hud.Stats != null && Player.TrainingMovement.IsConfigured,
            "Inline course, HUD and movement exports resolve");
        Check(Gym.LabReturnMarker.Owner == Gym && Gym.LabDoorZone.Owner == Gym,
            "Gym door zone and return marker are scene-owned");
        Check(Gym.BriefingActive && !Gym.Course.Hud.Visible, "Welcome hides HUD");
        Check(Find<Label>(Scene, l => l.Text.StartsWith("Circuit") && l.IsVisibleInTree()) == null, "Welcome hides checklist");
        await Capture("gym-welcome");
        EnglishUi(Scene);
        for (int i = 0; i < 3; i++) { KeyPress(Key.Enter); await Frames(); }
        Check(!Gym.BriefingActive, "Briefing closes");
        await WalkTo(new(400, 230)); await Capture("gym-door");
        Input.ActionPress("training_jump");
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("training_jump");
        Check(!Player.TrainingMovement.Grounded, "Real jump starts");
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
        Check(Player.Position.DistanceTo(new(288, 292)) < 1, "Room-specific entrance, never gym position");
        var lab = (TrainingLab)Scene;
        Check(lab.Player.Owner == lab && lab.Camera.Owner == lab && lab.GetNode("Floor").Owner == lab,
            "Lab player, camera and compact floor exist in the editable scene");
        Check(new[] { lab.WiresZone, lab.FusesZone, lab.GymZone }.All(zone => zone.Owner == lab)
            && lab.ArrivalMarkers.Count == 3 && lab.ArrivalMarkers.All(marker => marker.Owner == lab),
            "Lab interaction zones and arrival markers resolve to scene-owned nodes");
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
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1024, 768) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Frames(8);
                Check(GetViewport().GetTexture().GetSize() == (Vector2)size, "Requested render resolution applied");
                var view = GetViewport().GetVisibleRect(); var transform = ((Node2D)Scene).GetCanvasTransform();
                Check(view.HasPoint(transform * Vector2.Zero) && view.HasPoint(transform * new Vector2(576, 384)), "Lab camera frames whole room after resize");
                await Capture($"lab-{size.X}x{size.Y}");
            }
            GetWindow().ContentScaleSize = oldScaleSize; GetWindow().ContentScaleMode = oldScaleMode;
            GetWindow().Size = oldSize; await Frames(8);
        }
        // Verify cabinet bases with actual collision queries, not only visual rectangles.
        Check(Player.TestMove(new Transform2D(0, new Vector2(160, 202)), new Vector2(0, -70)), "Workbench base is solid");
        Check(Player.TestMove(new Transform2D(0, new Vector2(416, 202)), new Vector2(0, -70)), "Fuse cabinet base is solid");
        Player.Position = new(160, 105); await Frames(); KeyPress(Key.E); await Frames();
        Check(Scene is TrainingLab, "Station unavailable from behind");
        Player.Position = new(288, 292); await Frames();
        await WalkTo(new(160, 202)); await Frames();
        var stationController = Scene.GetNode<TrainingStations>("PracticeStations");
        var originalTarget = stationController.Targets[0];
        stationController.Targets[0] = originalTarget with { Scene = "res://tests/intentionally_missing_training_scene.tscn" };
        bool printErrors = Engine.PrintErrorMessages;
        try
        {
            Engine.PrintErrorMessages = false; // Deliberate fault injection, not an unexpected engine error.
            await Frames(); KeyPress(Key.E); await Frames(8);
        }
        finally { Engine.PrintErrorMessages = printErrors; }
        Check(Scene is TrainingLab && Player.IsPhysicsProcessing() && PracticeSession.Pending == null, "Failed transition releases player and clears pending travel");
        Check(Find<Label>(Scene, l => l.Text.StartsWith("Could not open")) != null, "Failed transition gives English retry feedback");
        stationController.Targets[0] = originalTarget;
        await Enter(PracticeSession.Exercise.Wires); KeyPress(Key.Escape); await Back(false, new(160, 202));
        Check(!PracticeSession.Wires, "Cancel does not complete wires");
        await Enter(PracticeSession.Exercise.Wires); SolveWires(); await Back(true, new(160, 202));
        Check(PracticeSession.Wires, "Real keyboard wire solution completes");
        await Enter(PracticeSession.Exercise.Wires); KeyPress(Key.Escape); await Back(false, new(160, 202));
        Check(PracticeSession.Wires, "Repeat cancel keeps prior completion");
        await Enter(PracticeSession.Exercise.Fuses);
        for (int i = 0; i < 4; i++) { KeyPress(Key.Key1); await Frames(2); }
        KeyPress(Key.Escape); await Back(false, new(416, 202));
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        Check(!PracticeSession.Fuses, "Reset timer cancellation safe");
        await Enter(PracticeSession.Exercise.Fuses); await SolveFuses(); KeyPress(Key.Escape); await Back(false, new(416, 202));
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        Check(!PracticeSession.Fuses, "Success timer cancellation safe");
        await Enter(PracticeSession.Exercise.Fuses); await SolveFuses();
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout); await Back(true, new(416, 202));
        Check(PracticeSession.Fuses && PracticeSession.Wires, "Both repairs completed");
        await Capture("lab-completed-repairs");
        Check(PracticeSession.Saved == saved, "Gym stamina/rest and course frozen outside gym");
        await WalkTo(new(288, 292)); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && !Gym.BriefingActive, "Return to gym skips welcome");
        Check(Player.Position.DistanceTo(new(400, 230)) < 1, "Gym return marker");
        Check(Gym.Course.Capture() == saved.Course, "Count and direction restored");
        Check(Mathf.Abs(Player.TrainingMovement.Stamina - saved.Movement.Stamina) < 1, "Stamina restored");
        Check(Player.GetNode<PlayerAnimation>("AnimatedSprite2D").Facing == "side" && Player.GetNode<PlayerAnimation>("AnimatedSprite2D").FlipH, "Orientation restored");
        await WalkTo(new(400, 510)); await Frames(); await Capture("gym-circuit-hint");
        Check(Find<Label>(Scene, l => l.Text.StartsWith("WASD: move") && l.IsVisibleInTree()) != null, "Movement and jump hint appears near circuit");
        await WalkTo(new(400, 230)); await Frames();
        Cross(1); Check(Gym.Course.ClearedCount == 2, "Reverse direction survives travel");
        Cross(0); await Frames(); Check(Gym.CompletionShown && Gym.Course.Congratulated, "Final crossing congratulates once");
        Gym.GetNode<Button>(Gym.ContinuePath).EmitSignal(BaseButton.SignalName.Pressed); await Frames();
        KeyPress(Key.E); await Frames(8); Check(Scene is TrainingLab, "Repeat room travel");
        KeyPress(Key.E); await Frames(8); Check(Scene is TrainingIntro && Gym.CompletionShown && !Gym.BriefingActive, "Completion greeting not repeated");
        Check(GetTree().ChangeSceneToFile(PracticeSession.SceneFor(PracticeSession.Exercise.Wires)) == Error.Ok, "Standalone wire load"); await Frames();
        Check(PracticeSession.Saved == null && !PracticeSession.Wires, "External exit clears session");
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
        Check(Gym.BriefingActive && PracticeSession.Pending == null && !PracticeSession.Wires, "External exit discards deferred return and starts a fresh session");
        // First visit to the gym from a directly launched lab has no saved circuit.
        GetTree().ChangeSceneToFile(PracticeSession.LabScene); await Frames(8);
        await Enter(PracticeSession.Exercise.Wires);
        var returnUi = Scene.GetNode<PracticeReturn>("PracticeReturn");
        returnUi.SceneLoader = _ => Error.CantOpen;
        printErrors = Engine.PrintErrorMessages;
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
            return GetTree().ChangeSceneToFile(path);
        };
        KeyPress(Key.Escape); await Back(true, new(160, 202));
        Check(PracticeSession.Saved == null, "Direct lab visit has no gym snapshot");
        long labSession = PracticeSession.SessionId;
        await WalkTo(new(288, 292)); await Frames(); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && PracticeSession.SessionId == labSession && PracticeSession.Wires, "First gym arrival preserves lab repairs and session");
        Check(Player.Position.DistanceTo(new(400, 230)) < 1, "First gym arrival uses door marker");
        Check(Gym.Course.ClearedCount == 0 && Gym.BriefingActive, "First gym visit initializes course and welcome");
        for (int i = 0; i < 3; i++) { KeyPress(Key.Enter); await Frames(); }
        KeyPress(Key.E); await Frames(8); KeyPress(Key.E); await Frames(8);
        Check(Scene is TrainingIntro && !Gym.BriefingActive && PracticeSession.Wires, "Subsequent gym arrival skips welcome and keeps repairs");
        Check(initialMission == (mission.Stage, mission.MissionTime, mission.Budget, mission.Mass,
            mission.Energy, mission.Communications, mission.SpareParts, mission.RoverImpacts, mission.RoverCollisions), "No mission changes");
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

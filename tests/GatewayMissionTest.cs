using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.gateway;
using ares.scripts.training;
using ares.scripts.mission;

public partial class GatewayMissionTest : Node
{
    private int _assertions;

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            SessionMessages();
            await PanelAndMovement();
            GD.Print($"GATEWAY MISSION PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void FinishExercise(PracticeSession.Exercise exercise, string source, bool success)
    {
        PracticeSession.Begin(exercise, source, "test.return", new Vector2(260, 425));
        var board = GD.Load<PackedScene>(PracticeSession.SceneFor(exercise)).Instantiate();
        long ticket = PracticeSession.EnterExercise(board);
        Check(ticket != 0, "Exercise receives a valid travel ticket");
        Check(PracticeSession.Finish(PracticeSession.SessionId, ticket, success), "Exercise result is accepted");
        board.Free();
    }

    private void SessionMessages()
    {
        PracticeSession.Clear();
        Check(GatewayMission.ArrivalMessages().SequenceEqual(new[] { GatewayMission.Start }),
            "Fresh Gateway session shows the exact incoming transmission");
        Check(!GatewayMission.ArrivalMessages().Any(), "Start is not repeated on room return");
        foreach (var exercise in Enum.GetValues<PracticeSession.Exercise>())
        {
            Check(GatewayMission.EnterRoom(exercise) == GatewayMission.Alert(exercise), "Room shows the matching alert");
            Check(GatewayMission.EnterRoom(exercise) == null, "Room alert appears only once");
        }

        // Existing training achievements do not repair the Gateway.
        var mission = MissionState.Instance;
        mission.TrainingWiresComplete = mission.TrainingFusesComplete =
            mission.TrainingFrequencyComplete = mission.TrainingHullRepairComplete = true;
        Check(!GatewayMission.ArrivalMessages().Any(), "Training flags do not complete Gateway repairs");
        FinishExercise(PracticeSession.Exercise.Wires, PracticeSession.LabScene, true);
        Check(!GatewayMission.ArrivalMessages().Any(), "Successful lab exercise does not repair Gateway");

        FinishExercise(PracticeSession.Exercise.Wires, Gateway.ScenePath, false);
        Check(!GatewayMission.ArrivalMessages().Any(), "Cancelled Gateway exercise produces no restoration");
        var exercises = Enum.GetValues<PracticeSession.Exercise>();
        for (int i = 0; i < exercises.Length; i++)
        {
            var exercise = exercises[i];
            FinishExercise(exercise, Gateway.ScenePath, true);
            string[] messages = GatewayMission.ArrivalMessages().ToArray();
            Check(messages[0] == GatewayMission.Restored(exercise), "Success shows the matching restoration verbatim");
            Check(messages.Length == (i == exercises.Length - 1 ? 2 : 1), "Success appears only after all four repairs");
            if (i == exercises.Length - 1)
                Check(messages[1] == GatewayMission.Complete, "Final restoration precedes mission success");
            Check(!GatewayMission.ArrivalMessages().Any(), "Restoration and completion messages do not repeat");
            Check(PracticeSession.Pending.Target == Gateway.ScenePath, "Minigame returns to Gateway");
        }
        FinishExercise(exercises[0], Gateway.ScenePath, true);
        Check(!GatewayMission.ArrivalMessages().Any(), "Repeating a repair does not repeat completion");
        PracticeSession.Clear();
        Check(GatewayMission.ArrivalMessages().Single() == GatewayMission.Start, "New session resets Gateway message history");
    }

    private async Task PanelAndMovement()
    {
        var gateway = GD.Load<PackedScene>(Gateway.ScenePath).Instantiate<Gateway>();
        string nextScene = "";
        AddChild(gateway);
        var panel = gateway.MissionControl;
        panel.SceneLoader = path => { nextScene = path; return Error.Ok; };
        var stations = gateway.GetNode<TrainingStations>("GatewayStations");
        Check(panel.IsOpen && panel.Message == GatewayMission.Start, "Gateway opens mission start panel");
        Check(!gateway.Player.IsPhysicsProcessing() && stations.InteractionLocked, "Transmission locks movement and station interaction");

        var training = GD.Load<PackedScene>(PracticeSession.TrainingScene).Instantiate();
        var expectedStyle = (StyleBoxFlat)training.GetNode<PanelContainer>("IntroUI/Panel")
            .GetThemeStylebox("panel");
        var actualStyle = (StyleBoxFlat)panel.GetNode<PanelContainer>("Panel").GetThemeStylebox("panel");
        Check(actualStyle.BgColor == expectedStyle.BgColor && actualStyle.BorderColor == expectedStyle.BorderColor &&
            actualStyle.BorderWidthLeft == expectedStyle.BorderWidthLeft &&
            actualStyle.CornerRadiusTopLeft == expectedStyle.CornerRadiusTopLeft,
            "Mission Control matches Training intro panel style");
        training.Free();
        panel.Advance();
        Check(!panel.IsOpen && gateway.Player.IsPhysicsProcessing() && !stations.InteractionLocked,
            "Continue restores movement and interaction");

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        gateway.Player.SetPhysicsProcess(false);
        gateway.Player.GlobalPosition = new Vector2(464, 320);
        Check(gateway.Player.MoveAndCollide(new Vector2(100, 0)) != null,
            "Real player movement collides with the main room wall");
        Check(gateway.Player.GlobalPosition.X < 496, "Player remains inside main room boundary");
        gateway.Player.GlobalPosition = new Vector2(464, 432);
        Check(gateway.Player.MoveAndCollide(new Vector2(100, 0)) == null,
            "Real player movement passes through the main doorway");

        var rooms = new[]
        {
            (new Vector2(980, 300), PracticeSession.Exercise.Wires),
            (new Vector2(1330, 300), PracticeSession.Exercise.Frequency),
            (new Vector2(980, 550), PracticeSession.Exercise.Fuses),
            (new Vector2(1330, 550), PracticeSession.Exercise.HullRepair)
        };
        foreach (var (position, exercise) in rooms)
        {
            gateway.Player.GlobalPosition = position;
            gateway._Process(0);
            Check(panel.IsOpen && panel.Message == GatewayMission.Alert(exercise), "Entering room opens its system alert");
            panel.Advance();
            gateway._Process(0);
            Check(!panel.IsOpen, "Room alert does not reopen after Continue");
        }
        Check(panel.EndingFrame == 0, "Final images do not appear before Gateway repairs are complete");
        gateway.Player.SetPhysicsProcess(true);
        foreach (var exercise in Enum.GetValues<PracticeSession.Exercise>())
        {
            FinishExercise(exercise, Gateway.ScenePath, true);
            foreach (string message in GatewayMission.ArrivalMessages()) panel.Enqueue(message);
            Check(panel.Message == GatewayMission.Restored(exercise), "Final sequence waits for restoration message");
            panel.Advance();
        }
        Check(panel.Message == GatewayMission.Complete && panel.EndingFrame == 0,
            "Mission success message precedes final images");
        panel.Advance();
        var image = panel.GetNode<TextureRect>("FinalScenes/Image");
        Check(panel.EndingFrame == 1 && image.Texture.ResourcePath.EndsWith("/prerover1.png"),
            "First Continue shows prerover1");
        Check(panel.GetNode<Control>("FinalScenes").Visible &&
            !panel.GetNode<Control>("Panel").Visible, "First image is fullscreen without the message panel");
        Check(image.AnchorRight == 1 && image.AnchorBottom == 1 &&
            image.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered,
            "Final images fill the viewport without stretching their aspect ratio");
        Check(!gateway.Player.IsPhysicsProcessing() && stations.InteractionLocked,
            "Cinematic keeps movement and station interactions locked");
        panel.Advance();
        Check(panel.EndingFrame == 2 && image.Texture.ResourcePath.EndsWith("/prerover2.png"),
            "Second Continue shows prerover2");
        Check(panel.GetNode<Control>("Panel").Visible && panel.Message == GatewayMission.ConnectionRecovered,
            "Second image has the exact recovered-connection transmission");
        panel.Advance();
        Check(image.IsVisibleInTree() && !panel.GetNode<Control>("Panel").Visible,
            "Final Continue leaves second image visible");
        Check(nextScene == "res://scenes/rover/rover.tscn",
            "Gateway completion routes to the rover scene");
        Check(!gateway.Player.IsPhysicsProcessing() && stations.InteractionLocked && panel.IsOpen,
            "Gameplay stays locked behind the final image");
        panel.Advance();
        Check(panel.EndingFrame == 2 && !GatewayMission.ArrivalMessages().Any(),
            "Ending does not replay within the same session");
        gateway.QueueFree();
    }
}

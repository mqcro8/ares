namespace ares.scripts.training;
using Godot;
using ares.scripts.player;

/// <summary>Room restoration and interaction; all room geometry lives in the scene.</summary>
public partial class TrainingLab : Node2D
{
    [Export] public Player Player { get; set; }
    [Export] public Camera2D Camera { get; set; }
    [Export] public Control WiresZone { get; set; }
    [Export] public Control FusesZone { get; set; }
    [Export] public Control GymZone { get; set; }
    [Export] public Godot.Collections.Array<Marker2D> ArrivalMarkers { get; set; } = new();

    public override void _Ready()
    {
        var arrival = PracticeSession.Arrive(PracticeSession.LabScene);
        if (arrival == null) PracticeSession.Clear();
        if (arrival != null)
        {
            foreach (var marker in ArrivalMarkers)
                if (marker.Name == arrival.Spawn.Replace('.', '_')) Player.GlobalPosition = marker.GlobalPosition;
            if (arrival.Spawn.EndsWith(".return")) Player.GlobalPosition = arrival.ReturnPosition;
            PracticeSession.RestoreFacing(PracticeSession.LabScene, Player);
        }
        FitCamera(); GetViewport().SizeChanged += FitCamera;
        var stations = new TrainingStations { Name = "PracticeStations", Player = Player }; AddChild(stations);
        stations.Targets.Add(new("Wiring workbench", WiresZone.GetGlobalRect(), PracticeSession.SceneFor(PracticeSession.Exercise.Wires), "lab.wires.return", PracticeSession.Exercise.Wires));
        stations.Targets.Add(new("Fuse cabinet", FusesZone.GetGlobalRect(), PracticeSession.SceneFor(PracticeSession.Exercise.Fuses), "lab.fuses.return", PracticeSession.Exercise.Fuses));
        stations.Targets.Add(new("Return to gym", GymZone.GetGlobalRect(), PracticeSession.TrainingScene, "gym.from_lab"));
    }

    private void FitCamera()
    {
        var size = GetViewportRect().Size;
        Camera.Zoom = Vector2.One * Mathf.Min(size.X / 640, size.Y / 480);
    }
    public override void _ExitTree() => GetViewport().SizeChanged -= FitCamera;
}

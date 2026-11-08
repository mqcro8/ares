namespace ares.scripts.training;
using Godot;
using ares.scripts.player;
using ares.scripts.mission;

/// <summary>Room restoration and interaction; all room geometry lives in the scene.</summary>
public partial class TrainingLab : Node2D
{
	[Export] public Player Player { get; set; }
	[Export] public Camera2D Camera { get; set; }
	[Export] public Control WiresZone { get; set; }
	[Export] public Control FusesZone { get; set; }
	[Export] public Control GymZone { get; set; }
	[Export] public Control SimulatorZone { get; set; }
	[Export] public Control LaunchPadZone { get; set; }
	private TrainingStations _stations;

	public override void _Ready()
	{
		if (Player == null || Camera == null || WiresZone == null || FusesZone == null ||
			GymZone == null || SimulatorZone == null || LaunchPadZone == null)
		{
			GD.PushError("TrainingLab: player, camera or station zones are not assigned.");
			return;
		}
		var arrival = PracticeSession.Arrive(PracticeSession.LabScene);
		if (arrival == null) PracticeSession.Clear();
		if (arrival != null)
		{
			string spawn = arrival.Spawn ?? "";
			if (spawn.EndsWith(".return"))
				Player.GlobalPosition = arrival.ReturnPosition;
			else
			{
				string markerPath = spawn switch
				{
					"lab.from_gym" => "lab_from_gym",
					"lab.wires.return" => "WiringWorkbench/lab_wires_return",
					"lab.fuses.return" => "FuseCabinet/lab_fuses_return",
					_ => null
				};
				if (markerPath != null && GetNodeOrNull<Marker2D>(markerPath) is Marker2D marker)
					Player.GlobalPosition = marker.GlobalPosition;
			}
			PracticeSession.RestoreFacing(PracticeSession.LabScene, Player);
		}
		FitCamera(); GetViewport().SizeChanged += FitCamera;
		_stations = new TrainingStations { Name = "PracticeStations", Player = Player }; AddChild(_stations);
		_stations.Targets.Add(new("Wiring workbench", WiresZone, PracticeSession.SceneFor(PracticeSession.Exercise.Wires), "lab.wires.return", PracticeSession.Exercise.Wires));
		_stations.Targets.Add(new("Fuse cabinet", FusesZone, PracticeSession.SceneFor(PracticeSession.Exercise.Fuses), "lab.fuses.return", PracticeSession.Exercise.Fuses));
		_stations.Targets.Add(new("Return to gym", GymZone, PracticeSession.TrainingScene, "gym.from_lab"));
		AddLaunchTargets();
		if (TrainingCompletionDialog.Open(this, Player, () => _stations.InteractionLocked = false))
			_stations.InteractionLocked = true;
	}

	private void AddLaunchTargets()
	{
		var mission = MissionState.Instance;
		_stations.Targets.Add(new("Launch simulator", SimulatorZone,
			"res://scenes/RocketLaunch/Launching.tscn", "launch.practice", null,
			PracticeSession.LaunchMode.Practice));
		string locked = !mission.LaunchPadUnlocked
			? "LAUNCH PAD: LOCKED — complete course, wiring, fuses and simulator."
			: mission.LaunchRecord != null ? "LAUNCH COMPLETE — review the mission." : null;
		_stations.Targets.Add(new("Launch pad", LaunchPadZone,
			"res://scenes/RocketLaunch/Launching.tscn", "launch.real", null,
			PracticeSession.LaunchMode.Real, locked));
	}

	private void FitCamera()
	{
		var size = GetViewportRect().Size;
		Camera.Zoom = Vector2.One * Mathf.Min(size.X / 640, size.Y / 480);
		// Gym: 1.5 sprite scale * 1.5 camera zoom = 2.25 screen pixels per art pixel.
		// Derive every resize from native art coordinates; never scale the physics body.
		float visualScale = 2.25f / Camera.Zoom.X;
		var visual = Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		visual.Scale = Vector2.One * visualScale;
		visual.Position = new Vector2(0, -11) * visualScale;
		Player.GetNode<Polygon2D>("GroundShadow").Scale = Vector2.One * (visualScale / 2);
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= FitCamera;
}

namespace ares.scripts.training;
using Godot;
using System;
using ares.scripts.player;
using ares.scripts.mission;
using ares.scripts.systems.items;
using ares.scripts.ui.mission_monitor;

/// <summary>Room restoration and interaction; all room geometry lives in the scene.</summary>
public partial class TrainingLab : Node2D
{
	public const float RoomWidth = 768f;
	public const float RoomHeight = 432f;
	public static readonly Vector2 DefaultSpawn = new(384, 352);
	[Export] public Player Player { get; set; }
	[Export] public Camera2D Camera { get; set; }
	[Export] public Control WiresZone { get; set; }
	[Export] public Control FusesZone { get; set; }
	[Export] public Control FrequencyZone { get; set; }
	[Export] public Control HullRepairZone { get; set; }
	[Export] public Control GymZone { get; set; }
	[Export] public Control SimulatorZone { get; set; }
	[Export] public Control LaunchPadZone { get; set; }
	[Export] public EquipmentLocker Locker { get; set; }
	[Export] public MissionMonitorView Monitor { get; set; }
	public TrainingControlsGuide ControlsGuide { get; private set; }
	private TrainingStations _stations;
	private Label _cargoStatus;
	private bool _completionNoticeOpen;

	public override void _Ready()
	{
		if (Player == null || Camera == null || WiresZone == null || FusesZone == null ||
			FrequencyZone == null || HullRepairZone == null ||
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
					_ => null
				};
				if (markerPath != null && GetNodeOrNull<Marker2D>(markerPath) is Marker2D marker)
					Player.GlobalPosition = marker.GlobalPosition;
			}
			PracticeSession.RestoreFacing(PracticeSession.LabScene, Player);
		}
		FitCamera(); GetViewport().SizeChanged += FitCamera;
		_stations = new TrainingStations { Name = "PracticeStations", Player = Player }; AddChild(_stations);
		_stations.Targets.Add(new("Wiring workbench", WiresZone, PracticeSession.SceneFor(PracticeSession.Exercise.Wires), "lab.wires.return", PracticeSession.Exercise.Wires, RequiredCapability: ItemCapability.Repair));
		_stations.Targets.Add(new("Fuse cabinet", FusesZone, PracticeSession.SceneFor(PracticeSession.Exercise.Fuses), "lab.fuses.return", PracticeSession.Exercise.Fuses, RequiredCapability: ItemCapability.Repair));
		_stations.Targets.Add(new("Frequency calibrator", FrequencyZone, PracticeSession.SceneFor(PracticeSession.Exercise.Frequency), "lab.frequency.return", PracticeSession.Exercise.Frequency, RequiredCapability: ItemCapability.Repair));
		_stations.Targets.Add(new("Hull repair bench", HullRepairZone, PracticeSession.SceneFor(PracticeSession.Exercise.HullRepair), "lab.hull.return", PracticeSession.Exercise.HullRepair, RequiredCapability: ItemCapability.Repair));
		if (Locker != null)
			_stations.Targets.Add(new("Supply locker", Locker.FrontZone, null, null,
				Interact: () => Locker.Open(Player), Prompt: () => Locker.InteractionPrompt(Player)));
		_stations.Targets.Add(new("Return to gym", GymZone, PracticeSession.TrainingScene, "gym.from_lab"));
		AddLaunchTargets();
		ControlsGuide = new TrainingControlsGuide
		{
			Name = "ContextControlsGuide",
			Player = Player,
			Stations = _stations
		};
		AddChild(ControlsGuide);
		CreateCargoStatus();
		if (TrainingCompletionDialog.Open(this, Player, () =>
		{
			_stations.InteractionLocked = false;
			_completionNoticeOpen = false;
		}))
		{
			_completionNoticeOpen = true;
			_stations.InteractionLocked = true;
		}
	}

	public override void _Process(double delta)
	{
		if (Monitor != null)
		{
			var mission = MissionState.Instance;
			var saved = PracticeSession.Saved;
			Monitor.Render(MissionMonitorPresenter.CaptureTraining(mission,
				inLab: true,
				stamina: saved?.Movement?.Stamina,
				circuitProgress: mission.TrainingCourseComplete ? 3 : saved?.Course?.Cleared ?? 0,
				circuitComplete: mission.TrainingCourseComplete || saved?.Course?.Finished == true,
				hasRepairKit: Player.Inventory?.HasCapability(ItemCapability.Repair) == true,
				prompt: _stations?.MonitorPrompt,
				feedback: _stations?.MonitorFeedback,
				visible: !_completionNoticeOpen));
		}
		if (_cargoStatus != null)
			_cargoStatus.Text = $"CARGO {MissionState.Instance.CargoUsed}/{MissionState.Instance.CargoCapacity}";
	}

	private void CreateCargoStatus()
	{
		var layer = Player.GetNodeOrNull<CanvasLayer>("HotbarLayer");
		if (layer == null) return;
		_cargoStatus = new Label
		{
			Name = "CargoStatus",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_cargoStatus.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
		_cargoStatus.OffsetLeft = -150;
		_cargoStatus.OffsetTop = -46;
		_cargoStatus.OffsetRight = -12;
		_cargoStatus.OffsetBottom = -28;
		_cargoStatus.AddThemeColorOverride("font_color", new Color(0.46f, 0.89f, 0.96f));
		_cargoStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
		_cargoStatus.AddThemeConstantOverride("outline_size", 4);
		_cargoStatus.AddThemeFontSizeOverride("font_size", 10);
		layer.AddChild(_cargoStatus);
	}

	private void AddLaunchTargets()
	{
		var mission = MissionState.Instance;
		_stations.Targets.Add(new("Launch simulator", SimulatorZone,
			"res://scenes/RocketLaunch/Launching.tscn", "launch.practice", null,
			PracticeSession.LaunchMode.Practice));
		_stations.Targets.Add(new("Launch pad", LaunchPadZone,
			"res://scenes/RocketLaunch/Launching.tscn", "launch.real", null,
			PracticeSession.LaunchMode.Real, LaunchPadLockMessage));
	}

	private static string LaunchPadLockMessage()
	{
		var mission = MissionState.Instance;
		if (!mission.LaunchPadUnlocked)
			return "LAUNCH PAD: LOCKED — complete course, wiring, fuses and simulator.";
		return mission.LaunchRecord != null ? "LAUNCH COMPLETE — review the mission." : null;
	}

	private void FitCamera()
	{
		// Match the gym's fixed camera and sprite transform exactly at every resolution.
		Camera.Zoom = Vector2.One * 1.5f;
		Camera.GlobalPosition = new Vector2(RoomWidth / 2, RoomHeight / 2);
		var visual = Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		visual.Scale = Vector2.One * 1.5f;
		visual.Position = new Vector2(1.5f, -16.5f);
		Player.GetNode<Polygon2D>("GroundShadow").Scale = Vector2.One * 0.75f;
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= FitCamera;
}

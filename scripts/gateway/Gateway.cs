namespace ares.scripts.gateway;

using System.Linq;
using Godot;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.items;
using ares.scripts.training;
using ares.scripts.ui.mission_monitor;
using Ares.Systems.Audio;
using Ares.Systems.Multiplayer;

/// <summary>
/// Gateway hub and four-room repair annex. Each annex room owns one of the
/// training exercises, while the interaction and inventory rules stay shared
/// with the Training Lab.
/// </summary>
public partial class Gateway : Node2D, ares.scripts.systems.IDevSceneSkipTarget
{
	public const string ScenePath = "res://scenes/gateway/Gateway.tscn";
	[Export] public Player Player { get; set; }
	[Export] public Camera2D Camera { get; set; }
	[Export] public Control WiresZone { get; set; }
	[Export] public Control FusesZone { get; set; }
	[Export] public Control FrequencyZone { get; set; }
	[Export] public Control HullRepairZone { get; set; }
	[Export] public MissionMonitorView Monitor { get; set; }

	private TrainingStations _stations;
	private Label _status;
	private TeamSession _team;
	public GatewayMissionControl MissionControl { get; private set; }
	public bool CanSkipWithDevCode => IsInsideTree() && MissionControl != null &&
		MissionState.Instance?.Ending == MissionEnding.None;

	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.TensionMusic, 1.5);
		MissionState.Instance.Stage = MissionStage.Gateway;
		_team = TeamSession.Instance;
		var arrival = PracticeSession.Arrive(ScenePath);
		if (arrival != null)
		{
			Player.GlobalPosition = arrival.ReturnPosition;
			PracticeSession.RestoreFacing(ScenePath, Player);
		}
		else
			PracticeSession.Clear();

		_stations = new TrainingStations { Name = "GatewayStations", Player = Player };
		AddChild(_stations);
		AddStation("Electrical Bay", WiresZone, PracticeSession.Exercise.Wires, "gateway.wires.return");
		AddStation("Power Distribution Room", FusesZone, PracticeSession.Exercise.Fuses, "gateway.fuses.return");
		AddStation("Communications Bay", FrequencyZone, PracticeSession.Exercise.Frequency, "gateway.frequency.return");
		AddStation("Hull Access", HullRepairZone, PracticeSession.Exercise.HullRepair, "gateway.hull.return");

		if (IsTeamMission)
		{
			_team.TeamStateReceived += OnTeamStateReceived;
			ApplyTeamTasks();
			ReportAssignedTaskIfAlreadyComplete();
		}

		MissionControl = GD.Load<PackedScene>("res://scenes/gateway/MissionControlBox.tscn")
			.Instantiate<GatewayMissionControl>();
		AddChild(MissionControl);
		MissionControl.Bind(Player, _stations);
		foreach (string message in GatewayMission.ArrivalMessages()) MissionControl.Enqueue(message);
		FitCamera();
		GetViewport().SizeChanged += FitCamera;
	}

	private void AddStation(string name, Control zone, PracticeSession.Exercise exercise, string marker)
		=> _stations.Targets.Add(new(name, zone, PracticeSession.SceneFor(exercise), marker,
			exercise, Locked: () => IsTeamMission && AssignedTaskIndex != (int)exercise
				? $"CREW ASSIGNMENT — PLAYER {(int)exercise + 1} OWNS THIS REPAIR."
				: null, RequiredCapability: ItemCapability.Repair));

	private bool IsTeamMission => _team?.Active == true && _team.MissionStarted;
	private int AssignedTaskIndex => _team?.LocalSlot ?? -1;

	private void ReportAssignedTaskIfAlreadyComplete()
	{
		if (AssignedTaskIndex is < 0 or > 3) return;
		var exercise = (PracticeSession.Exercise)AssignedTaskIndex;
		if (PracticeSession.CompletedIn(ScenePath, exercise))
			_team.MarkGatewayTaskComplete(AssignedTaskIndex);
	}

	private void OnTeamStateReceived(string json)
	{
		if (!IsInsideTree()) return;
		ApplyTeamTasks();
		if (MissionControl != null)
			foreach (string message in GatewayMission.ArrivalMessages()) MissionControl.Enqueue(message);
	}

	private void ApplyTeamTasks()
	{
		for (int slot = 0; slot < 4; slot++)
			if (_team.IsGatewayTaskComplete(slot))
				PracticeSession.CompleteForDeveloper(ScenePath, (PracticeSession.Exercise)slot);
	}

	public override void _Process(double delta)
	{
		if (MissionControl != null && !MissionControl.IsOpen && EquipmentPanel.Active == null)
		{
			Vector2 position = Player.GlobalPosition;
			PracticeSession.Exercise? room = position switch
			{
				{ X: > 816 and < 1152, Y: > 16 and < 384 } => PracticeSession.Exercise.Wires,
				{ X: > 1152 and < 1488, Y: > 16 and < 384 } => PracticeSession.Exercise.Frequency,
				{ X: > 816 and < 1152, Y: > 480 and < 832 } => PracticeSession.Exercise.Fuses,
				{ X: > 1152 and < 1488, Y: > 480 and < 832 } => PracticeSession.Exercise.HullRepair,
				_ => null
			};
			if (room.HasValue) MissionControl.Enqueue(GatewayMission.EnterRoom(room.Value));
		}
		if (Monitor != null)
		{
			var mission = MissionState.Instance;
			Monitor.Render(MissionMonitorPresenter.CaptureTraining(mission, inLab: true,
				stamina: mission.Stamina, circuitProgress: 0, circuitComplete: false,
				hasRepairKit: Player.Inventory?.HasCapability(ItemCapability.Repair) == true,
				prompt: _stations?.MonitorPrompt, feedback: _stations?.MonitorFeedback,
				visible: MissionControl?.IsOpen != true));
		}
		if (Camera != null && Player != null)
			Camera.GlobalPosition = Player.GlobalPosition;
		if (_status != null)
			_status.Text = $"GATEWAY ANNEX  //  {_stations?.CurrentTargetName ?? "READY"}";
	}

	private void FitCamera()
	{
		if (Camera == null || Player == null) return;
		Camera.Zoom = Vector2.One * 1.5f;
		Camera.GlobalPosition = Player.GlobalPosition;
	}

	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= FitCamera;
		if (_team != null) _team.TeamStateReceived -= OnTeamStateReceived;
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		foreach (var exercise in System.Enum.GetValues<PracticeSession.Exercise>())
			PracticeSession.CompleteForDeveloper(ScenePath, exercise);
		GatewayMission.ArrivalMessages().ToArray();
		MissionControl.SkipToRover();
		return true;
	}
}

using Godot;
using System;
using System.Threading.Tasks;
using ares.scripts.mission;
using ares.scripts.ui;

public partial class OxygenSystemTest : Node
{
	private int _assertions;

	private void Check(bool condition, string message)
	{
		_assertions++;
		if (!condition) throw new InvalidOperationException(message);
	}

	private async Task Frames(int count = 3)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	public override async void _Ready()
	{
		try
		{
			TestSupplyModel();
			TestCanisterCollection();
			await TestPickupInteraction();
			await TestMarsBaseWiring();
			await TestHudAndMissionFailure();
			GD.Print($"OXYGEN PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GetTree().Paused = false;
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void TestSupplyModel()
	{
		var supply = new OxygenSupply(secondsPerTank: 100);
		Check(supply.TrySetTankCount(4, out string error), "Four tanks accepted: " + error);
		Check(supply.CapacitySeconds == 400 && supply.RemainingSeconds == 400,
			"Tank count determines total oxygen capacity");
		supply.Advance(30);
		Check(supply.RemainingSeconds == 400 && !supply.IsActive,
			"Oxygen does not drain before Mars surface activity");
		supply.BeginMarsSurface();
		supply.Advance(339);
		Check(!supply.IsCritical && supply.RemainingSeconds == 61,
			"Critical warning stays off above 15 percent");
		supply.Advance(1);
		Check(supply.IsCritical && Math.Abs(supply.Fraction - 0.15) < 0.00001,
			"Critical state begins at 15 percent");
		supply.StopMarsSurface();
		supply.Advance(20);
		Check(supply.RemainingSeconds == 60 && !supply.IsCritical,
			"Oxygen pauses outside a Mars-surface scene");
		supply.BeginMarsSurface();
		Check(supply.IsActive && supply.RemainingSeconds == 60,
			"Returning to the surface resumes the same oxygen reserve");
		int depletionSignals = 0;
		supply.Depleted += () => depletionSignals++;
		supply.Advance(60);
		Check(supply.IsDepleted && !supply.IsActive && supply.RemainingSeconds == 0 && depletionSignals == 1,
			"Zero oxygen emits one mission-failure signal");
		Check(supply.EmergencyReturnRequired,
			"Running out of oxygen forces an emergency return");
		supply.Advance(10);
		Check(depletionSignals == 1, "Depleted oxygen cannot emit repeated failure signals");

		var empty = new OxygenSupply(secondsPerTank: 100);
		empty.TrySetTankCount(0, out _);
		int emptySignals = 0;
		empty.Depleted += () => emptySignals++;
		empty.BeginMarsSurface();
		Check(empty.IsDepleted && emptySignals == 1, "Starting on Mars without tanks fails immediately");

		var reserve = new OxygenSupply(secondsPerTank: 100);
		reserve.TrySetTankCount(4, out _);
		reserve.BeginMarsSurface();
		Check(!reserve.IsReturnReserveLow, "A full reserve is not a return warning");
		reserve.Advance(320);
		Check(reserve.RemainingSeconds == 80 && reserve.IsReturnReserveLow,
			"The return warning appears once the reserve can no longer cover the walk back");
		reserve.Advance(10);
		Check(reserve.RemainingSeconds == 70 && reserve.IsReturnReserveLow && !reserve.IsCritical,
			"The return warning stays on while the reserve is still above the critical level");
		reserve.Advance(20);
		Check(reserve.RemainingSeconds == 50 && reserve.IsCritical && reserve.IsReturnReserveLow,
			"The critical warning takes over below fifteen percent");
	}

	private void TestCanisterCollection()
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.CargoCapacity = 6;
		Check(mission.TrySetOxygenTankCount(2, out string error), "Mission accepts two tanks: " + error);
		mission.Oxygen.BeginMarsSurface();
		mission.Oxygen.Advance(100);

		var pickup = GD.Load<PackedScene>("res://scenes/mars/OxygenCanister.tscn")
			.Instantiate<OxygenPickup>();
		AddChild(pickup);
		Check(pickup.Seconds == 60 && !pickup.RefillToFull,
			"The canister scene carries a fixed amount of oxygen");
		string feedback = pickup.Collect();
		Check(feedback.Contains("+60") && pickup.Collected,
			"Collecting a canister fills the suit reserve: " + feedback);
		Check(Math.Abs(mission.Oxygen.RemainingSeconds - 320) < .001,
			"The canister adds its seconds to the reserve");
		Check(pickup.Collect() == "ALREADY COLLECTED.",
			"A collected canister cannot be taken twice");
		var station = GD.Load<PackedScene>("res://scenes/mars/OxygenStation.tscn")
			.Instantiate<OxygenPickup>();
		AddChild(station);
		Check(station.RefillToFull, "The station scene refills the reserve completely");
		string refillFeedback = station.Collect();
		Check(refillFeedback == "OXYGEN REFILLED." && mission.Oxygen.IsFull,
			"The lander station refills the reserve: " + refillFeedback);
		Check(station.Collect() == "OXYGEN FULL.", "A filled suit cannot be refilled again");

		var full = GD.Load<PackedScene>("res://scenes/mars/OxygenCanister.tscn")
			.Instantiate<OxygenPickup>();
		AddChild(full);
		full.Seconds = 600;
		string fullFeedback = full.Collect();
		Check(fullFeedback.Contains("OXYGEN FULL") && !full.Collected,
			"A full reserve leaves the canister untouched: " + fullFeedback);
		Check(mission.Oxygen.IsFull, "The reserve stays capped at the tank capacity");

		pickup.QueueFree();
		full.QueueFree();
		station.QueueFree();
		mission.ResetEquipment();
	}

	private async Task TestPickupInteraction()
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.CargoCapacity = 6;
		Check(mission.TrySetOxygenTankCount(2, out string error), "Mission accepts two tanks: " + error);
		mission.Oxygen.BeginMarsSurface();
		mission.Oxygen.Advance(60);

		var player = new ares.scripts.player.Player { Name = "Player" };
		AddChild(player);
		var pickup = GD.Load<PackedScene>("res://scenes/mars/OxygenCanister.tscn")
			.Instantiate<OxygenPickup>();
		pickup.Position = new Vector2(64, 0);
		AddChild(pickup);
		await Frames(4);
		Check(!pickup.PlayerNearby,
			"A distant canister does not offer its pickup prompt");

		player.GlobalPosition = new Vector2(64, 0);
		for (int i = 0; i < 8; i++) await Frames();
		Check(pickup.PlayerNearby,
			"The canister notices the astronaut standing next to it");
		Check(pickup.GetNode<Label>("Prompt").Visible &&
			pickup.GetNode<Label>("Prompt").Text.Contains("E"),
			"The canister shows an E prompt when the astronaut is close");

		Input.ParseInputEvent(new InputEventKey
		{
			Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true
		});
		Input.FlushBufferedEvents();
		await Frames(2);
		Check(pickup.Collected && mission.Oxygen.RemainingSeconds > 300,
			"Pressing E collects the canister into the suit reserve");
		Check(mission.OxygenMessage.Contains("+60"),
			"The HUD receives the pickup message: " + mission.OxygenMessage);

		player.QueueFree();
		pickup.QueueFree();
		await Frames();
		mission.ResetEquipment();
	}

	private async Task TestHudAndMissionFailure()
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.CargoCapacity = 6;
		Check(mission.TrySetOxygenTankCount(1, out string error), "Mission accepts one tank: " + error);
		var hud = GD.Load<PackedScene>("res://scenes/ui/OxygenHud.tscn").Instantiate<OxygenHud>();
		AddChild(hud);
		await Frames();
		Check(mission.Stage == MissionStage.Mars && mission.Oxygen.IsActive,
			"Oxygen HUD starts drain only for the Mars-surface scene");
		mission.Oxygen.Advance(mission.Oxygen.CapacitySeconds * 0.851);
		Check(hud.IsCritical, "HUD activates its warning at or below 15 percent");
		await Frames();
		Check(hud.CriticalFlashAlpha > 0 && hud.IsCriticalWarningVisible,
			"Critical state flashes the whole screen and shows a text warning");
		mission.Oxygen.Advance(mission.Oxygen.RemainingSeconds + 1);
		Check(hud.IsEmergencyVisible && mission.Oxygen.IsDepleted,
			"Running out of oxygen shows the emergency return screen");
		Check(!GetTree().Paused,
			"Running out of oxygen no longer freezes the game so the astronaut can walk back");
		hud.QueueFree();
		await Frames();
		mission.ResetEquipment();
	}

	private async Task TestMarsBaseWiring()
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.CargoCapacity = 6;
		Check(mission.TrySetOxygenTankCount(2, out string error), "Mars loadout accepts oxygen tanks: " + error);
		var mars = GD.Load<PackedScene>("res://scenes/mars/MarsBase.tscn").Instantiate<Node2D>();
		AddChild(mars);
		await Frames(6);
		Check(mission.Stage == MissionStage.Mars && mission.Oxygen.IsActive,
			"The outdoor Mars scene activates oxygen drain");
		Check(mars.GetNodeOrNull<CanvasLayer>("OxygenHud") != null &&
			mars.GetNodeOrNull<OxygenPickup>("SurfaceOxygenCanister") != null &&
			mars.GetNodeOrNull<OxygenPickup>("LanderOxygenStation") != null,
			"Mars Base contains the oxygen HUD, a canister and the lander refill station");
		var station = mars.GetNode<OxygenPickup>("LanderOxygenStation");
		Check(station.PlayerNearby,
			"The lander refill station is reachable from the Mars spawn");
		mars.QueueFree();
		await Frames();
		Check(!mission.Oxygen.IsActive,
			"Leaving the outdoor Mars scene pauses oxygen drain indoors");
		mission.ResetEquipment();
	}
}

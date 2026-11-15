using Godot;
using System;
using System.Threading.Tasks;
using ares.scripts.dialogue;
using ares.scripts.mars;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.items;
using Ares.Systems.Audio;
using Ares.Systems.Controllers;

public partial class MarsRepairRetryTest : Node
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

	private async Task Wait(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	public override async void _Ready()
	{
		try
		{
			TestProgressState();
			TestDialogueResources();
			TestMarsAudioWiring();
			await TestNoToolRetryForEachStation();
			await TestToolCompletesInOneRun();
			await TestCancelKeepsPendingRetry();
			GD.Print($"MARS REPAIR RETRY PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void TestProgressState()
	{
		var mission = MissionState.Instance;
		mission.ResetPuzzleProgress();

		Check(!mission.MarsRepairNeedsRetry(MarsRepairTask.Generator) &&
			mission.MarsRepairAttempts(MarsRepairTask.Generator) == 0,
			"Generator starts without a pending retry");

		mission.RecordMarsRepairAttempt(MarsRepairTask.Generator);
		Check(mission.MarsRepairNeedsRetry(MarsRepairTask.Generator) &&
			!mission.GeneratorRepaired && mission.MarsRepairAttempts(MarsRepairTask.Generator) == 1,
			"First tool-less success records a retry without completing the task");

		mission.MarkWireBoxRepaired();
		mission.MarkSignalTableRepaired();
		for (int i = 0; i < MissionState.DoorAccessCount; i++)
			mission.SetDoorAccess(i, true);
		Check(!mission.AllPuzzlesComplete && mission.CurrentObjective == MissionObjective.RestorePower,
			"A pending retry keeps the return gate and objective locked");

		mission.MarkGeneratorRepaired();
		Check(mission.AllPuzzlesComplete && !mission.MarsRepairNeedsRetry(MarsRepairTask.Generator),
			"Only the completed second run opens the return gate");

		mission.ResetPuzzleProgress();
		Check(!mission.MarsRepairNeedsRetry(MarsRepairTask.Generator) &&
			!mission.GeneratorRepaired && !mission.WireBoxRepaired && !mission.SignalTableRepaired,
			"New mission clears repair progress and pending retries");
	}

	private void TestDialogueResources()
	{
		CheckDialogue("res://data/dialogue/mars_retry_generator.tres", "spare fuses", "faulty");
		CheckDialogue("res://data/dialogue/mars_retry_wires.tres", "wire pliers", "loose");
		CheckDialogue("res://data/dialogue/mars_retry_frequency.tres", "frequency analyzer", "wrong frequency");
	}

	private void CheckDialogue(string path, string tool, string reason)
	{
		var sequence = GD.Load<DialogueSequence>(path);
		Check(sequence != null && sequence.Lines.Count == 1, $"Dialogue exists with one line: {path}");
		if (sequence == null || sequence.Lines.Count == 0) return;
		DialogueLine line = sequence.Lines[0];
		string text = line.Text.ToLowerInvariant();
		Check(line.Speaker == "YOU" && text.Contains(tool) && text.Contains(reason) &&
			text.Contains("again") && text.Contains("should have brought"),
			$"Retry line names the missing tool and the reason for the redo: {path}");
	}

	private void TestMarsAudioWiring()
	{
		var mars = GD.Load<PackedScene>("res://scenes/mars/MarsBase.tscn").Instantiate<Node2D>();
		var wind = mars.GetNodeOrNull<SfxEmitter2D>("Player/AmbientWind");
		Check(wind != null && wind.SoundId == SoundIds.RoverWindSfx && wind.PlayOnReady,
			"MarsBase has a looping wind emitter attached to the player");
		Check(mars.GetNodeOrNull<Player>("Player")?.FootstepSoundId == SoundIds.FootstepsPlanet,
			"Mars surface uses planet footsteps");
		mars.Free();

		foreach (string path in new[]
		{
			"res://scenes/mars/FirstBuilding.tscn",
			"res://scenes/mars/SecondBuilding.tscn",
			"res://scenes/mars/ThirdBuilding.tscn",
		})
		{
			var building = GD.Load<PackedScene>(path).Instantiate<Node2D>();
			Check(building.GetNodeOrNull<Player>("Player")?.FootstepSoundId == SoundIds.FootstepsMetal,
				$"Mars interior uses metal footsteps: {path}");
			building.Free();
		}

		Check(AudioManager.TryGetSfx(SoundIds.RoverWindSfx, out SoundDef windDef) &&
			windDef.Loop && windDef.Stream != null,
			"The ambient wind sound is a configured looping SFX");
		foreach (string id in new[]
		{
			SoundIds.Enter, SoundIds.FootstepsPlanet, SoundIds.FootstepsMetal,
			SoundIds.OpenDoor, SoundIds.RoverPowerUp, SoundIds.SystemMalfunction,
		})
			Check(AudioManager.TryGetSfx(id, out _), $"Mars sound id resolves: {id}");
	}

	private async Task TestNoToolRetryForEachStation()
	{
		var mission = MissionState.Instance;

		mission.ResetEquipment();
		mission.GetOrCreatePlayerInventory(7, out _);
		mission.ResetPuzzleProgress();
		await CheckNoToolRetry<Generator>("res://scenes/mars/Generator.tscn",
			MarsRepairTask.Generator, ToolEffects.SpareFuses,
			(station, player) => station.BeginRepair(player),
			() => mission.GeneratorRepaired);

		mission.ResetEquipment();
		mission.GetOrCreatePlayerInventory(7, out _);
		mission.ResetPuzzleProgress();
		await CheckNoToolRetry<ElectricalBox>("res://scenes/mars/ElectricalBox.tscn",
			MarsRepairTask.Wires, ToolEffects.WirePliers,
			(station, player) => station.BeginRepair(player),
			() => mission.WireBoxRepaired);

		mission.ResetEquipment();
		mission.GetOrCreatePlayerInventory(7, out _);
		mission.ResetPuzzleProgress();
		await CheckNoToolRetry<SignalTable>("res://scenes/mars/SignalTable.tscn",
			MarsRepairTask.Frequency, ToolEffects.FrequencyAnalyzer,
			(station, player) => station.BeginRepair(player),
			() => mission.SignalTableRepaired);
	}

	private async Task CheckNoToolRetry<T>(string scenePath, MarsRepairTask task, string toolId,
		Action<T, Node2D> begin, Func<bool> completed) where T : Node2D
	{
		var mission = MissionState.Instance;
		var owner = new Node2D { Name = $"Owner_{task}" };
		AddChild(owner);
		var player = new Node2D { Name = "Player" };
		owner.AddChild(player);
		var station = GD.Load<PackedScene>(scenePath).Instantiate<T>();
		owner.AddChild(station);
		await Frames();

		begin(station, player);
		var overlay = station.GetNodeOrNull<MinigameOverlay>("MinigameOverlay");
		Check(overlay != null, $"{task} opens the minigame overlay");
		if (overlay == null)
		{
			owner.QueueFree();
			return;
		}

		overlay.RetryMessageSeconds = 0.05f;
		Control firstBoard = overlay.CurrentGame;
		Solve(firstBoard);

		Check(overlay.IsRetryDialogueVisible,
			$"{task} shows the regret dialogue immediately after a tool-less first success");
		Check(mission.MarsRepairAttempts(task) == 1 && !completed() && !mission.HasEquipped(toolId),
			$"{task} records the first attempt without marking the task complete");

		await Wait(0.2);
		Control secondBoard = overlay.CurrentGame;
		Check(secondBoard != null && !ReferenceEquals(firstBoard, secondBoard),
			$"{task} auto-starts a fresh minigame board after the dialogue");
		Check(!overlay.IsRetryDialogueVisible && mission.MarsRepairAttempts(task) == 1 && !completed(),
			$"{task} keeps the retry pending until the second run");

		Check(overlay.HandleControllerCommand(Cancel()),
			"Phone cancel closes the retry overlay");
		await Frames();
		Check(!IsInstanceValid(overlay) && mission.MarsRepairAttempts(task) == 1 && !completed(),
			$"{task} preserves the pending retry after cancel");

		owner.QueueFree();
		await Frames();
	}

	private async Task TestToolCompletesInOneRun()
	{
		var mission = MissionState.Instance;

		await CheckOneRun<Generator>("res://scenes/mars/Generator.tscn", MarsRepairTask.Generator,
			ToolEffects.SpareFuses, (station, player) => station.BeginRepair(player),
			() => mission.GeneratorRepaired);

		await CheckOneRun<ElectricalBox>("res://scenes/mars/ElectricalBox.tscn", MarsRepairTask.Wires,
			ToolEffects.WirePliers, (station, player) => station.BeginRepair(player),
			() => mission.WireBoxRepaired);

		await CheckOneRun<SignalTable>("res://scenes/mars/SignalTable.tscn", MarsRepairTask.Frequency,
			ToolEffects.FrequencyAnalyzer, (station, player) => station.BeginRepair(player),
			() => mission.SignalTableRepaired);
	}

	private async Task CheckOneRun<T>(string scenePath, MarsRepairTask task, string toolId,
		Action<T, Node2D> begin, Func<bool> completed) where T : Node2D
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.ResetPuzzleProgress();
		var inventory = mission.GetOrCreatePlayerInventory(7, out _);
		inventory.AddItem(GD.Load<ItemData>($"res://data/equipment/items/{toolId}.tres"));

		var owner = new Node2D { Name = $"Owner_{task}_Tool" };
		AddChild(owner);
		var player = new Node2D { Name = "Player" };
		owner.AddChild(player);
		var station = GD.Load<PackedScene>(scenePath).Instantiate<T>();
		owner.AddChild(station);
		await Frames();

		begin(station, player);
		var overlay = station.GetNodeOrNull<MinigameOverlay>("MinigameOverlay");
		Check(overlay != null, $"{task} opens with the matching tool");
		if (overlay == null)
		{
			owner.QueueFree();
			return;
		}

		Solve(overlay.CurrentGame);
		Check(overlay.HandleControllerCommand(Cancel()) && IsInstanceValid(overlay),
			$"{task} ignores a cancel while the solved board is finishing");
		await Wait(1.1);
		Check(overlay.IsRetryDialogueVisible == false && completed() &&
			mission.MarsRepairAttempts(task) == 0,
			$"{task} completes in one successful run when its tool is carried");
		Check(!IsInstanceValid(overlay), $"{task} closes after the tool-assisted success");

		owner.QueueFree();
		await Frames();
	}

	private async Task TestCancelKeepsPendingRetry()
	{
		var mission = MissionState.Instance;
		mission.ResetEquipment();
		mission.GetOrCreatePlayerInventory(7, out _);
		mission.ResetPuzzleProgress();

		var owner = new Node2D { Name = "Owner_CancelRetry" };
		AddChild(owner);
		var player = new Node2D { Name = "Player" };
		owner.AddChild(player);
		var generator = GD.Load<PackedScene>("res://scenes/mars/Generator.tscn").Instantiate<Generator>();
		owner.AddChild(generator);
		await Frames();

		generator.BeginRepair(player);
		var overlay = generator.GetNodeOrNull<MinigameOverlay>("MinigameOverlay");
		Check(overlay != null, "Generator opens for the persistence check");
		if (overlay == null)
		{
			owner.QueueFree();
			return;
		}
		overlay.RetryMessageSeconds = 0.05f;
		Solve(overlay.CurrentGame);
		Check(overlay.HandleControllerCommand(Cancel()), "Cancel is accepted during the first retry");
		await Frames();
		Check(generator.PromptText == "Press E to retry generator repair",
			"The station prompt asks for the mandatory second attempt");

		owner.QueueFree();
		await Frames();

		var secondOwner = new Node2D { Name = "Owner_RetryResume" };
		AddChild(secondOwner);
		var secondPlayer = new Node2D { Name = "Player" };
		secondOwner.AddChild(secondPlayer);
		var resumed = GD.Load<PackedScene>("res://scenes/mars/Generator.tscn").Instantiate<Generator>();
		secondOwner.AddChild(resumed);
		await Frames();

		resumed.BeginRepair(secondPlayer);
		var resumedOverlay = resumed.GetNodeOrNull<MinigameOverlay>("MinigameOverlay");
		Check(resumedOverlay != null && mission.MarsRepairNeedsRetry(MarsRepairTask.Generator),
			"A re-entered station still knows the retry is pending");
		if (resumedOverlay != null)
		{
			Solve(resumedOverlay.CurrentGame);
			Check(!resumedOverlay.IsRetryDialogueVisible,
				"One more successful run finishes a previously botched repair");
			await Wait(1.1);
			Check(mission.GeneratorRepaired, "The pending retry completes on the next success");
		}

		secondOwner.QueueFree();
		await Frames();
	}

	private static ControllerCommand Cancel() =>
		new(ControllerCommands.MinigameCancel, 0, false, 0f, false);

	private static void Solve(Control board)
	{
		switch (board)
		{
			case WireMinigame wire:
				wire.EmitSignal(WireMinigame.SignalName.Completed);
				break;
			case FuseBoxMinigame fuse:
				fuse.EmitSignal(FuseBoxMinigame.SignalName.Completed);
				break;
			case FrequencyBoxMinigame frequency:
				frequency.EmitSignal(FrequencyBoxMinigame.SignalName.Completed);
				break;
			default:
				throw new InvalidOperationException($"Unsupported Mars board: {board?.GetType().Name ?? "null"}");
		}
	}
}

namespace ares.scripts.mission;

using System;
using Godot;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

// Single source of truth for the run. Registered as an autoload.
// Every scene reads and writes here; nothing is duplicated locally.
public partial class MissionState : Node
{
	public const int OxygenTankCargoCost = ToolEffects.OxygenTankCargoCost;
	public static MissionState Instance { get; private set; }

	public const int DoorAccessCount = 4;

	public MissionStage Stage { get; set; } = MissionStage.MainMenu;
	public double MissionTime { get; set; }
	public Inventory PlayerInventory { get; private set; }
	public Inventory LockerInventory { get; private set; }
	public int CargoCapacity { get; set; } = ToolEffects.CargoCapacity;
	public const float MaxStamina = 100f;
	public const float StaminaRestDelay = 1f;
	public const float StaminaRecoveryPerSecond = 25f;
	public float Stamina { get; private set; } = MaxStamina;
	public float StaminaRestSeconds { get; private set; }
	public bool LoadoutConfirmed { get; set; }
	public OxygenSupply Oxygen { get; } = new();
	public int OxygenTankCount => Oxygen.TankCount;
	public int OxygenCargoUsed => OxygenTankCount * OxygenTankCargoCost;
	public int CargoUsed => (PlayerInventory?.CargoUsed ?? 0) + OxygenCargoUsed;

	// Puzzle progress. Lives here so it survives building/scene changes.
	public bool GeneratorRepaired { get; private set; }
	public bool WireBoxRepaired { get; private set; }
	public bool SignalTableRepaired { get; private set; }
	public bool AllPuzzlesComplete => GeneratorRepaired && WireBoxRepaired && SignalTableRepaired && AllDoorAccessOn;
	public bool SecondBuildingBriefingShown { get; private set; }
	public MissionObjective? LastAnnouncedObjective { get; private set; }

	public MissionObjective CurrentObjective
	{
		get
		{
			if (!GeneratorRepaired || !WireBoxRepaired)
				return MissionObjective.RestorePower;
			if (!AllDoorAccessOn)
				return MissionObjective.OpenGreenhouseDoors;
			if (!SignalTableRepaired)
				return MissionObjective.RestoreCommunications;
			return MissionObjective.ReturnToStart;
		}
	}

	private readonly bool[] _doorAccess = new bool[DoorAccessCount];
	public int DoorAccessOnCount
	{
		get
		{
			int count = 0;
			foreach (bool on in _doorAccess)
				if (on) count++;
			return count;
		}
	}
	public bool AllDoorAccessOn => DoorAccessOnCount == DoorAccessCount;

	[Signal] public delegate void GeneratorRepairedChangedEventHandler(bool repaired);
	[Signal] public delegate void WireBoxRepairedChangedEventHandler(bool repaired);
	[Signal] public delegate void SignalTableRepairedChangedEventHandler(bool repaired);
	[Signal] public delegate void DoorAccessChangedEventHandler(int index, bool on);
	[Signal] public delegate void AllDoorAccessCompletedEventHandler();

	public bool IsDoorAccessOn(int index)
		=> index >= 0 && index < DoorAccessCount && _doorAccess[index];

	public void SetDoorAccess(int index, bool on)
	{
		if (index < 0 || index >= DoorAccessCount || _doorAccess[index] == on)
			return;

		_doorAccess[index] = on;
		EmitSignal(SignalName.DoorAccessChanged, index, on);
		if (on && AllDoorAccessOn)
			EmitSignal(SignalName.AllDoorAccessCompleted);
	}

	public void MarkGeneratorRepaired()
	{
		if (GeneratorRepaired)
			return;

		GeneratorRepaired = true;
		EmitSignal(SignalName.GeneratorRepairedChanged, true);
	}

	public void MarkWireBoxRepaired()
	{
		if (WireBoxRepaired)
			return;

		WireBoxRepaired = true;
		EmitSignal(SignalName.WireBoxRepairedChanged, true);
	}

	public void MarkSignalTableRepaired()
	{
		if (SignalTableRepaired)
			return;

		SignalTableRepaired = true;
		EmitSignal(SignalName.SignalTableRepairedChanged, true);
	}

	public void MarkSecondBuildingBriefingShown()
	{
		SecondBuildingBriefingShown = true;
	}

	public void MarkObjectiveAnnounced(MissionObjective objective)
	{
		LastAnnouncedObjective = objective;
	}

	public void ResetPuzzleProgress()
	{
		GeneratorRepaired = false;
		WireBoxRepaired = false;
		SignalTableRepaired = false;
		SecondBuildingBriefingShown = false;
		LastAnnouncedObjective = null;
		for (int i = 0; i < _doorAccess.Length; i++)
			_doorAccess[i] = false;
	}

	// Resource axes from the design doc. Exact axes and starting values are an M0 decision.
	public int Budget { get; set; }
	public int Mass { get; set; }
	public int Energy { get; set; }
	// A scene reload must not record the same launch twice.
	public long LaunchSequence { get; private set; } = 1;
	public LaunchResult LaunchRecord { get; private set; }
	public bool TrainingCourseComplete { get; set; }
	public bool TrainingWiresComplete { get; set; }
	public bool TrainingFusesComplete { get; set; }
	public bool TrainingFrequencyComplete { get; set; }
	public bool TrainingHullRepairComplete { get; set; }
	public bool TrainingLaunchPracticeComplete { get; set; }
	public bool LaunchPadUnlocked => TrainingCourseComplete && TrainingWiresComplete &&
		TrainingFusesComplete && TrainingLaunchPracticeComplete;
	public bool TrainingCompletionPending { get; private set; }
	public bool TrainingCompletionShown { get; private set; }
	public void EvaluateTrainingCompletion()
	{
		if (LaunchPadUnlocked && !TrainingCompletionShown) TrainingCompletionPending = true;
	}
	public bool TakeTrainingCompletionNotification()
	{
		if (!TrainingCompletionPending || TrainingCompletionShown) return false;
		TrainingCompletionPending = false;
		TrainingCompletionShown = true;
		return true;
	}
	public Inventory GetOrCreatePlayerInventory(int size, out bool created)
	{
		created = PlayerInventory == null;
		if (created) PlayerInventory = new Inventory { Size = size };
		return PlayerInventory;
	}
	public bool HasEquipped(string itemId)
	{
		if (string.IsNullOrEmpty(itemId) || PlayerInventory == null) return false;
		for (int i = 0; i < PlayerInventory.SlotCount; i++)
			if (PlayerInventory.GetStack(i)?.Item.Id == itemId) return true;
		return false;
	}
	public Inventory GetOrCreateLocker(int size, ItemData starterItem, ItemData[] extraItems = null)
	{
		if (LockerInventory == null)
		{
			LockerInventory = new Inventory { Size = size };
			if (starterItem != null) LockerInventory.AddItem(starterItem);
			if (extraItems != null)
				foreach (ItemData item in extraItems)
					if (item != null) LockerInventory.AddItem(item);
		}
		return LockerInventory;
	}
	public void ResetEquipment()
	{
		PlayerInventory = null;
		LockerInventory = null;
		CargoCapacity = ToolEffects.CargoCapacity;
		LoadoutConfirmed = false;
		Oxygen.Reset();
	}
	public bool TrySetOxygenTankCount(int count, out string error)
	{
		error = "";
		if (LoadoutConfirmed || Oxygen.HasStartedSurface)
		{
			error = "OXYGEN LOADOUT LOCKED — choose tanks before the real launch.";
			return false;
		}
		if (count < 0 || count > OxygenSupply.MaxTanks)
		{
			error = $"CHOOSE 0–{OxygenSupply.MaxTanks} OXYGEN TANKS.";
			return false;
		}
		int inventoryCargo = PlayerInventory?.CargoUsed ?? 0;
		int requestedCargo = count * OxygenTankCargoCost;
		if (inventoryCargo + requestedCargo > CargoCapacity)
		{
			error = $"CARGO LIMIT — {inventoryCargo + requestedCargo}/{CargoCapacity}.";
			return false;
		}
		return Oxygen.TrySetTankCount(count, out error);
	}
	public void BeginMarsSurface()
	{
		Stage = MissionStage.Mars;
		Oxygen.BeginMarsSurface();
	}
	public void EndMarsSurface() => Oxygen.StopMarsSurface();
	/// <summary>Stores a found canister into the suit reserve without raising its capacity.</summary>
	public bool TryCollectOxygenCanister(double seconds, out string error, out double addedSeconds) =>
		Oxygen.TryCollectCanister(seconds, out error, out addedSeconds);
	/// <summary>Refills the reserve at the lander or another safe station.</summary>
	public bool TryRefillOxygen(out string error) => Oxygen.TryRefill(out error);
	/// <summary>Short message shown by the oxygen HUD after a pickup attempt.</summary>
	public string OxygenMessage { get; private set; } = "";
	public event Action OxygenMessageChanged;
	public void SetOxygenMessage(string message)
	{
		OxygenMessage = message ?? "";
		OxygenMessageChanged?.Invoke();
	}
	public void BeginFromMainMenu()
	{
		BeginLaunchSequence();
		ResetEquipment();
		Stage = MissionStage.Intro;
	}
	public void CompleteIntro() => Stage = MissionStage.Training;
	public void BeginLaunchSequence()
	{
		LaunchSequence++;
		LaunchRecord = null; // Deliberately preserve existing resource values.
		PendingLaunchFaults = 0;
		Outcome.BeginRun(LaunchSequence);
		RoverStageCompleted = false;
		ResetPuzzleProgress();
		RoverImpacts = RoverCollisions = 0;
		RoverDamageLimit = ToolEffects.RoverBaseDamageLimit;
		Scoring.Reset();
		TrainingCourseComplete = TrainingWiresComplete = TrainingFusesComplete = false;
		TrainingFrequencyComplete = TrainingHullRepairComplete = TrainingLaunchPracticeComplete = false;
		TrainingCompletionPending = TrainingCompletionShown = false;
		ResetStamina();
	}
	public bool TrySpendStamina(float amount)
	{
		if (amount < 0 || Stamina < amount) return false;
		Stamina -= amount;
		StaminaRestSeconds = 0;
		return true;
	}
	public void RestoreStamina(float stamina, float restSeconds = 0)
	{
		Stamina = Mathf.Clamp(stamina, 0, MaxStamina);
		StaminaRestSeconds = Mathf.Max(0, restSeconds);
	}
	public void ResetStamina() => RestoreStamina(MaxStamina);

	/// <summary>Debug-only: starts Mars testing with a full oxygen reserve and stamina.</summary>
	public void DebugFillStats()
	{
		Oxygen.DebugFill(OxygenSupply.MaxTanks);
		ResetStamina();
	}

	public void AdvanceStamina(double delta, bool resting)
	{
		if (!resting)
		{
			StaminaRestSeconds = 0;
			return;
		}
		float previousRest = StaminaRestSeconds;
		StaminaRestSeconds += (float)delta;
		Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRecoveryPerSecond *
			(Mathf.Max(0, StaminaRestSeconds - StaminaRestDelay) - Mathf.Max(0, previousRest - StaminaRestDelay)));
	}
	public bool RecordLaunch(long sequence, LaunchResult result)
	{
		if (sequence != LaunchSequence || LaunchRecord != null || result == null || !result.Completed)
			return false;
		LaunchRecord = result;
		LoadoutConfirmed = true;
		PendingLaunchFaults = result.IgnitionFaults;
		return true;
	}
	/// <summary>Ignition faults owed by the last launch. They become repair work after landing.</summary>
	public int PendingLaunchFaults { get; private set; }
	/// <summary>Marks every training exercise as done. Used by the developer shortcut.</summary>
	public void CompleteAllTraining()
	{
		TrainingCourseComplete = true;
		TrainingWiresComplete = true;
		TrainingFusesComplete = true;
		TrainingFrequencyComplete = true;
		TrainingHullRepairComplete = true;
		TrainingLaunchPracticeComplete = true;
		TrainingCompletionShown = false;
		EvaluateTrainingCompletion();
	}
	/// <summary>Clears the training progress again. Used by the developer shortcut.</summary>
	public void ResetTrainingProgress()
	{
		TrainingCourseComplete = false;
		TrainingWiresComplete = false;
		TrainingFusesComplete = false;
		TrainingFrequencyComplete = false;
		TrainingHullRepairComplete = false;
		TrainingLaunchPracticeComplete = false;
		TrainingCompletionPending = false;
		TrainingCompletionShown = false;
	}
	public int Communications { get; set; }
	public int SpareParts { get; set; }
	public int RoverImpacts { get; set; }
	public int RoverCollisions { get; set; }
	public int RoverDamageLimit { get; set; } = ToolEffects.RoverBaseDamageLimit;
	public MissionScoring Scoring { get; } = new();
	/// <summary>Rover is an intermediate mission stage, not a whole-run ending.</summary>
	public bool RoverStageCompleted { get; private set; }
	public bool CompleteRoverStage(long runSequence)
	{
		if (runSequence != LaunchSequence || RoverStageCompleted || IsRunOver) return false;
		RoverStageCompleted = true;
		return true;
	}

	/// <summary>Authoritative lives and terminal result of the run.</summary>
	public MissionOutcome Outcome { get; } = new();
	public MissionEnding Ending => Outcome.Ending;
	public bool IsRunOver => Outcome.IsOver;

	/// <summary>Records completion of the whole campaign, not an intermediate rover stage.</summary>
	public bool TryCompleteMission()
	{
		Scoring.RecordRun(this);
		return Outcome.TryComplete(LaunchSequence);
	}
	public bool TryFailMission() => Outcome.TryFail(LaunchSequence);

	public override void _EnterTree()
	{
		Instance = this;
		if (Outcome.RunId == 0) Outcome.BeginRun(LaunchSequence);
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Process(double delta)
	{
		var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
		if (player == null)
		{
			AdvanceStamina(delta, true);
			return;
		}
		bool grounded = player.TrainingMovement == null ||
			!player.TrainingMovement.IsConfigured || player.TrainingMovement.Grounded;
		bool resting = grounded &&
			Input.GetVector("move_left", "move_right", "move_up", "move_down") == Vector2.Zero &&
			player.Velocity.LengthSquared() < 0.01f;
		AdvanceStamina(delta, resting);
	}
}

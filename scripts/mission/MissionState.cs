namespace ares.scripts.mission;

using System;
using Godot;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

// Single source of truth for the run. Registered as an autoload.
// Every scene reads and writes here; nothing is duplicated locally.
public partial class MissionState : Node
{
	public const int OxygenTankCargoCost = ToolEffects.OxygenTankCargoCost;
	public static MissionState Instance { get; private set; }

	public MissionStage Stage { get; set; } = MissionStage.MainMenu;
	public double MissionTime { get; set; }
	public Inventory PlayerInventory { get; private set; }
	public Inventory LockerInventory { get; private set; }
	public int CargoCapacity { get; set; } = ToolEffects.CargoCapacity;
	public bool LoadoutConfirmed { get; set; }
	public OxygenSupply Oxygen { get; } = new();
	public int OxygenTankCount => Oxygen.TankCount;
	public int OxygenCargoUsed => OxygenTankCount * OxygenTankCargoCost;
	public int CargoUsed => (PlayerInventory?.CargoUsed ?? 0) + OxygenCargoUsed;

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
		RoverImpacts = RoverCollisions = 0;
		TrainingCourseComplete = TrainingWiresComplete = TrainingFusesComplete = false;
		TrainingFrequencyComplete = TrainingHullRepairComplete = TrainingLaunchPracticeComplete = false;
		TrainingCompletionPending = TrainingCompletionShown = false;
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
	public override void _EnterTree() => Instance = this;
}

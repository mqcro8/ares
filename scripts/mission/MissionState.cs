namespace ares.scripts.mission;

using Godot;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

// Single source of truth for the run. Registered as an autoload.
// Every scene reads and writes here; nothing is duplicated locally.
public partial class MissionState : Node
{
	public static MissionState Instance { get; private set; }

	public MissionStage Stage { get; set; } = MissionStage.MainMenu;
	public double MissionTime { get; set; }
	public Inventory PlayerInventory { get; private set; }
	public Inventory LockerInventory { get; private set; }
	public int CargoCapacity { get; set; } = 6;
	public bool LoadoutConfirmed { get; set; }
	public int CargoUsed => PlayerInventory?.CargoUsed ?? 0;

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
	public Inventory GetOrCreateLocker(int size, ItemData starterItem)
	{
		if (LockerInventory == null)
		{
			LockerInventory = new Inventory { Size = size };
			if (starterItem != null) LockerInventory.AddItem(starterItem);
		}
		return LockerInventory;
	}
	public void ResetEquipment()
	{
		PlayerInventory = null;
		LockerInventory = null;
		CargoCapacity = 6;
		LoadoutConfirmed = false;
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
		TrainingCourseComplete = TrainingWiresComplete = TrainingFusesComplete = false;
		TrainingFrequencyComplete = TrainingHullRepairComplete = TrainingLaunchPracticeComplete = false;
		TrainingCompletionPending = TrainingCompletionShown = false;
	}
	public bool RecordLaunch(long sequence, LaunchResult result)
	{
		if (sequence != LaunchSequence || LaunchRecord != null || result == null || !result.Completed)
			return false;
		LaunchRecord = result;
		return true;
	}
	public int Communications { get; set; }
	public int SpareParts { get; set; }
	public int RoverImpacts { get; set; }
	public int RoverCollisions { get; set; }
	public override void _EnterTree() => Instance = this;
}

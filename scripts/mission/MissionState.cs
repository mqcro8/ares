namespace ares.scripts.mission;

using Godot;

// Single source of truth for the run. Registered as an autoload.
// Every scene reads and writes here; nothing is duplicated locally.
public partial class MissionState : Node
{
	public static MissionState Instance { get; private set; }

	public MissionStage Stage { get; set; } = MissionStage.MainMenu;
	public double MissionTime { get; set; }

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
	public void BeginFromMainMenu()
	{
		BeginLaunchSequence();
		Stage = MissionStage.Intro;
	}
	public void CompleteIntro() => Stage = MissionStage.Training;
	public void BeginLaunchSequence()
	{
		LaunchSequence++;
		LaunchRecord = null; // Deliberately preserve existing resource values.
		TrainingCourseComplete = TrainingWiresComplete = TrainingFusesComplete = TrainingLaunchPracticeComplete = false;
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

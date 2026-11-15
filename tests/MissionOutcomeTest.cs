using System;

public static class MissionOutcomeTest
{
	public static void Run(Action<bool, string> check)
	{
		var outcome = new ares.scripts.mission.MissionOutcome();
		outcome.BeginRun(10);
		check(outcome.Ending == ares.scripts.mission.MissionEnding.None,
			"A new campaign run has no terminal outcome");
		check(outcome.TryFail(10) && outcome.Ending == ares.scripts.mission.MissionEnding.Bad,
			"A whole-game failure latches the bad ending");
		check(!outcome.TryFail(10) && !outcome.TryComplete(10),
			"The terminal result cannot be overwritten or recorded twice");
		check(!outcome.TryFail(9), "Stale run callbacks are rejected");
		outcome.BeginRun(11);
		check(outcome.Ending == ares.scripts.mission.MissionEnding.None,
			"A new campaign run clears the old result");
		check(outcome.TryComplete(11) && outcome.Ending == ares.scripts.mission.MissionEnding.Good,
			"Mission completion latches the good ending");

		var mission = new ares.scripts.mission.MissionState();
		mission.TrainingCourseComplete = mission.TrainingWiresComplete =
			mission.TrainingFusesComplete = mission.TrainingFrequencyComplete =
			mission.TrainingHullRepairComplete = mission.TrainingLaunchPracticeComplete = true;
		mission.RecordLaunch(mission.LaunchSequence,
			new LaunchResult(10, 10, 25, 25, 0));
		mission.MarkGeneratorRepaired();
		mission.MarkWireBoxRepaired();
		mission.MarkSignalTableRepaired();
		for (int i = 0; i < ares.scripts.mission.MissionState.DoorAccessCount; i++)
			mission.SetDoorAccess(i, true);
		mission.CompleteRoverStage(mission.LaunchSequence);
		mission.Oxygen.TrySetTankCount(4, out _);
		mission.Oxygen.BeginMarsSurface();
		mission.Oxygen.Advance(mission.Oxygen.CapacitySeconds * 0.5);
		mission.Scoring.RecordRun(mission);
		check(Math.Abs(mission.Scoring.Overall - 100) < 1e-9 && mission.Scoring.Passed,
			"Clean phase performance and 50% oxygen earns a passing 100% score");
		mission.Oxygen.Advance(mission.Oxygen.CapacitySeconds * 0.26);
		mission.Scoring.RecordRun(mission);
		check(!mission.Scoring.HasReturnReserve && !mission.Scoring.Passed,
			"Ending below 25% oxygen fails the return-reserve requirement");
		check(mission.Scoring.AllMissionsWon, "A passing score requires successful launch, rover and Mars objectives");
		mission.BeginLaunchSequence();
		mission.ResetEquipment();
		mission.Oxygen.TrySetTankCount(4, out _);
		mission.RecordLaunch(mission.LaunchSequence, new LaunchResult(10, 10, 25, 25, 0));
		mission.MarkGeneratorRepaired();
		mission.MarkWireBoxRepaired();
		mission.MarkSignalTableRepaired();
		for (int i = 0; i < ares.scripts.mission.MissionState.DoorAccessCount; i++) mission.SetDoorAccess(i, true);
		mission.FailRoverStage(mission.LaunchSequence);
		mission.Oxygen.BeginMarsSurface();
		mission.Oxygen.Advance(mission.Oxygen.CapacitySeconds * 0.5);
		mission.Scoring.RecordRun(mission);
		check(mission.Scoring.Rover == 0 && !mission.Scoring.AllMissionsWon && !mission.Scoring.Passed,
			"Rover defeat applies its score penalty and prevents an all-missions pass");
		mission.BeginLaunchSequence();
		mission.TrainingCourseComplete = mission.TrainingWiresComplete =
			mission.TrainingFusesComplete = mission.TrainingFrequencyComplete =
			mission.TrainingHullRepairComplete = mission.TrainingLaunchPracticeComplete = true;
		check(mission.FailLaunch(mission.LaunchSequence) && mission.Ending == ares.scripts.mission.MissionEnding.Bad &&
			mission.LaunchFailed && mission.ShowScoreImmediately,
			"Failed launch records a terminal score-screen result");
		check(!mission.FailLaunch(mission.LaunchSequence) && mission.Scoring.Launch == 0 &&
			!mission.Scoring.AllMissionsWon,
			"Failed launch score cannot be applied twice or treated as a win");
	}
}

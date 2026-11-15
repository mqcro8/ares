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
	}
}

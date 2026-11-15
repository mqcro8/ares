namespace ares.scripts.mission;

using System;

/// <summary>How the mission ended. None means the run is still in progress.</summary>
public enum MissionEnding
{
	None,
	Good,
	Bad,
}

/// <summary>Authoritative result of the run. It is latched once so it can only be recorded once.</summary>
public sealed class MissionOutcome
{
	public MissionEnding Ending { get; private set; } = MissionEnding.None;
	public long RunId { get; private set; }
	public bool IsOver => Ending != MissionEnding.None;

	/// <summary>Starts a fresh campaign run and clears the previous whole-mission result.</summary>
	public void BeginRun(long runId)
	{
		RunId = runId;
		Ending = MissionEnding.None;
	}

	/// <summary>Records the terminal whole-game result. Intermediate-stage wins/failures do not call this.</summary>
	public bool TryRecord(long runId, MissionEnding ending)
	{
		if (runId != RunId || IsOver || ending == MissionEnding.None) return false;
		Ending = ending;
		return true;
	}

	public bool TryComplete(long runId) => TryRecord(runId, MissionEnding.Good);
	public bool TryFail(long runId) => TryRecord(runId, MissionEnding.Bad);
}

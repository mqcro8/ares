namespace ares.scripts.rover;

/// <summary>Implemented only by the active rover mission scene for the ORBIT developer code.</summary>
public interface IRoverMissionSkipTarget
{
	bool CanSkipRoverMission { get; }
	bool SkipRoverMission();
}

namespace ares.scripts.mission;

using System;

/// <summary>Weighted whole-run score, with category values expressed as percentages.</summary>
public sealed class MissionScoring
{
	public const double PassingGrade = 80;
	public const double ReturnReserveFraction = 0.25;

	public double Training { get; set; }
	public double Launch { get; set; }
	public double Repairs { get; set; }
	public double Rover { get; set; }
	public double Resources { get; private set; }
	public double Overall => Training * 0.10 + Launch * 0.20 + Repairs * 0.25 +
		Rover * 0.20 + Resources * 0.25;
	public bool HasReturnReserve { get; private set; }
	public double RecordedOxygenFraction { get; private set; }
	public double FinalOxygenSeconds { get; private set; }
	public bool Passed => Overall >= PassingGrade && HasReturnReserve;

	public void Reset()
	{
		Training = Launch = Repairs = Rover = Resources = 0;
		HasReturnReserve = false;
		RecordedOxygenFraction = 0;
		FinalOxygenSeconds = 0;
	}

	public void RecordRun(MissionState mission)
	{
		Training = Average(mission.TrainingCourseComplete, mission.TrainingWiresComplete,
			mission.TrainingFusesComplete, mission.TrainingFrequencyComplete,
			mission.TrainingHullRepairComplete, mission.TrainingLaunchPracticeComplete);
		Launch = mission.LaunchRecord == null ? 0 : 70 + 30 * mission.LaunchRecord.IgnitionRatio;
		Repairs = Average(mission.GeneratorRepaired, mission.WireBoxRepaired,
			mission.SignalTableRepaired, mission.AllDoorAccessOn);
		Rover = mission.RoverStageCompleted
			? 70 + 30 * Math.Clamp(1.0 - mission.RoverImpacts / (double)Math.Max(1, mission.RoverDamageLimit), 0, 1)
			: 0;

		FinalOxygenSeconds = mission.Oxygen.RemainingSeconds;
		RecordedOxygenFraction = mission.Oxygen.CapacitySeconds > 0
			? Math.Clamp(FinalOxygenSeconds / mission.Oxygen.CapacitySeconds, 0, 1) : 0;
		HasReturnReserve = mission.Oxygen.CapacitySeconds > 0 &&
			FinalOxygenSeconds >= mission.Oxygen.ReturnReserveSeconds;
		Resources = HasReturnReserve
			? 80 + 20 * Math.Clamp((RecordedOxygenFraction - ReturnReserveFraction) / 0.25, 0, 1)
			: 100 * RecordedOxygenFraction / ReturnReserveFraction;
	}

	private static double Average(params bool[] completed)
	{
		int count = 0;
		foreach (bool value in completed)
			if (value) count++;
		return completed.Length == 0 ? 0 : count * 100.0 / completed.Length;
	}
}

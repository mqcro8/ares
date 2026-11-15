namespace ares.scripts.systems.items;

using System;

/// <summary>Single source for tool identifiers, game effects and player-facing trade-offs.</summary>
public static class ToolEffects
{
	public const string MaintenanceKit = "maintenance_kit";
	public const string OxygenTank = "oxygen_tank";
	public const string SpareFuses = "spare_fuses";
	public const string FrequencyAnalyzer = "frequency_analyzer";
	public const string PrecisionWelder = "precision_welder";
	public const string WirePliers = "wire_pliers";
	public const string RoverBumper = "rover_bumper";
	public const string RoverServos = "rover_servos";
	public const string OxygenRegulator = "oxygen_regulator";

	public const int CargoCapacity = 7;
	public const int MaximumToolCargoCost = 2;
	public const int OxygenTankCargoCost = 1;
	public const double OxygenBaseSecondsPerTank = 120.0;
	public const int OxygenRegulatorMinimumTanks = 2;
	public const double OxygenRegulatorTankFactor = 1.3;

	public const int FuseBasePresses = 4;
	public const int FuseExtraPresses = 1;
	public const float FrequencyBaseThreshold = 0.95f;
	public const float FrequencyThresholdDelta = -0.07f;
	public const float FrequencyBaseHoldSeconds = 0.35f;
	public const float HullBaseWeldTolerance = 14f;
	public const float HullPrecisionWeldTolerance = 20f;
	public const float HullBasePlaceTolerance = 14f;
	public const float HullBaseHeatOffSeam = 0.8f;
	public const float HullWelderHeatFactor = 0.7f;
	public const float HullPrecisionHeatOffSeam = HullBaseHeatOffSeam * HullWelderHeatFactor;
	public const int WireConnectionCount = 4;
	public const int WirePreconnectedCount = 1;
	public const string FuseFailure = "Fuse box";
	public const string FrequencyFailure = "Frequency calibration";
	public const string HullFailure = "Hull repair";
	public const string WireFailure = "Wiring";
	public const string RoverFailure = "Rover";
	public const string OxygenFailure = "Oxygen";
	public static readonly string[] FailureCategories =
	{
		FuseFailure, FrequencyFailure, HullFailure, WireFailure, RoverFailure, OxygenFailure
	};

	public const int RoverBaseDamageLimit = 5;
	public const int RoverBumperExtraDamageLimit = 2;
	public const float RoverBumperSpeedFactor = 0.85f;
	public const float RoverBaseLaneChangeSpeed = 600f;
	public const float RoverServoControlTimeFactor = 0.5f;
	public const int RoverBaseDamagePerCollision = 1;
	public const int RoverServoDamagePerCollision = 2;
	public const float RoverBaseControlLagPerCollision = 0.2f;
	public const float RoverServoControlLagFactor = 0.5f;

	public static float FrequencyAnalyzerThreshold => FrequencyBaseThreshold + FrequencyThresholdDelta;
	public static int FusePressesFor(bool equipped) => FuseBasePresses + (equipped ? FuseExtraPresses : 0);
	public static int WireConnectionsFor(bool equipped) => WireConnectionCount - (equipped ? WirePreconnectedCount : 0);
	/// <summary>Relative visual meter only; exact gameplay values appear beside the bar.</summary>
	public static float BenefitFraction(string id) => id switch
	{
		SpareFuses => (float)FuseExtraPresses / FuseBasePresses,
		FrequencyAnalyzer => Math.Abs(FrequencyThresholdDelta) / FrequencyBaseThreshold,
		PrecisionWelder => Math.Max((HullPrecisionWeldTolerance - HullBaseWeldTolerance) / HullBaseWeldTolerance,
			1 - HullWelderHeatFactor),
		WirePliers => (float)WirePreconnectedCount / WireConnectionCount,
		RoverBumper => (float)RoverBumperExtraDamageLimit / RoverBaseDamageLimit,
		RoverServos => 1 / RoverServoControlTimeFactor - 1,
		OxygenRegulator => (float)(OxygenRegulatorTankFactor - 1),
		_ => 0f,
	};

	public static string BenefitText(string id) => id switch
	{
		SpareFuses => $"+{Number(FuseExtraPresses)} extra press: {Number(FuseBasePresses)}→{Number(FusePressesFor(true))} total presses",
		FrequencyAnalyzer => $"Threshold {Number(FrequencyBaseThreshold)}→{Number(FrequencyAnalyzerThreshold)}; hold {Number(FrequencyBaseHoldSeconds)} s",
		PrecisionWelder => $"Weld tolerance {Number(HullBaseWeldTolerance)}→{Number(HullPrecisionWeldTolerance)} px; off-seam heat {Number(HullBaseHeatOffSeam)}→{Number(HullPrecisionHeatOffSeam)}/s",
		WirePliers => $"{Number(WirePreconnectedCount)} of {Number(WireConnectionCount)} cables preconnected",
		RoverBumper => $"Impact limit {Number(RoverBaseDamageLimit)}→{Number(RoverBaseDamageLimit + RoverBumperExtraDamageLimit)}",
		RoverServos => $"Lane speed {Number(RoverBaseLaneChangeSpeed)}→{Number(RoverBaseLaneChangeSpeed / RoverServoControlTimeFactor)}; damage {Number(RoverBaseDamagePerCollision)}→{Number(RoverServoDamagePerCollision)}",
		OxygenRegulator => $"{Number(OxygenBaseSecondsPerTank)}→{Number(OxygenBaseSecondsPerTank * OxygenRegulatorTankFactor)} s per tank",
		_ => "",
	};

	public static string ShortName(string id) => id switch
	{
		SpareFuses => "SPARE FUSES",
		FrequencyAnalyzer => "ANALYZER",
		PrecisionWelder => "PRECISION WELDER",
		WirePliers => "WIRE PLIERS",
		RoverBumper => "ROVER BUMPER",
		RoverServos => "ROVER SERVOS",
		OxygenRegulator => "OXYGEN REGULATOR",
		_ => id
	};

	public static string DrawbackText(string id) => id switch
	{
		SpareFuses => "Fuse box only",
		FrequencyAnalyzer => "Frequency calibration only",
		PrecisionWelder => "Hull repair only",
		WirePliers => "No timer; wiring only",
		RoverBumper => $"Lane speed {Number(RoverBaseLaneChangeSpeed)} → {Number(RoverBaseLaneChangeSpeed * RoverBumperSpeedFactor)} px/s",
		RoverServos => "Each collision deals double damage",
		OxygenRegulator => $"Requires {Number(OxygenRegulatorMinimumTanks)} or more tanks",
		_ => "",
	};

	public static string Describe(string id) => id switch
	{
		MaintenanceKit => "Unlocks all four repair practices. Drawback: uses cargo space.",
		OxygenTank => $"Reserve: {Number(OxygenBaseSecondsPerTank)} s per tank. Drawback: uses cargo space.",
		SpareFuses or FrequencyAnalyzer or WirePliers =>
			$"{BenefitText(id)}; Mars repair: one successful run instead of two. Drawback: {DrawbackText(id)}.",
		PrecisionWelder or RoverBumper or RoverServos or OxygenRegulator =>
			$"{BenefitText(id)}. Drawback: {DrawbackText(id)}.",
		_ => "No tool effect registered."
	};

	public static string CoveredFailure(string id) => id switch
	{
		SpareFuses => FuseFailure,
		FrequencyAnalyzer => FrequencyFailure,
		PrecisionWelder => HullFailure,
		WirePliers => WireFailure,
		RoverBumper or RoverServos => RoverFailure,
		OxygenRegulator => OxygenFailure,
		_ => ""
	};

	private static string Number(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

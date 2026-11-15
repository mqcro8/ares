namespace ares.scripts.systems.items;

using System;
using System.Globalization;

/// <summary>Single source for tool identifiers, game effects and player-facing trade-offs.</summary>
public static class ToolEffects
{
	private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

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
	public const double OxygenBaseSecondsPerTank = 180.0;
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
	public const string FuseFailure = "Caja de fusibles";
	public const string FrequencyFailure = "Frecuencias";
	public const string HullFailure = "Casco";
	public const string WireFailure = "Cables";
	public const string RoverFailure = "Rover";
	public const string OxygenFailure = "Oxígeno";
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
		SpareFuses => $"+{Number(FuseExtraPresses)} margen: {Number(FuseBasePresses)}→{Number(FusePressesFor(true))} pulsaciones",
		FrequencyAnalyzer => $"Umbral {Number(FrequencyBaseThreshold)}→{Number(FrequencyAnalyzerThreshold)}; sostén {Number(FrequencyBaseHoldSeconds)} s",
		PrecisionWelder => $"Soldadura {Number(HullBaseWeldTolerance)}→{Number(HullPrecisionWeldTolerance)} px; calor {Number(HullBaseHeatOffSeam)}→{Number(HullPrecisionHeatOffSeam)}/s",
		WirePliers => $"{Number(WirePreconnectedCount)} de {Number(WireConnectionCount)} cables conectado",
		RoverBumper => $"Impactos {Number(RoverBaseDamageLimit)}→{Number(RoverBaseDamageLimit + RoverBumperExtraDamageLimit)}",
		RoverServos => $"Carril {Number(RoverBaseLaneChangeSpeed)}→{Number(RoverBaseLaneChangeSpeed / RoverServoControlTimeFactor)}; daño {Number(RoverBaseDamagePerCollision)}→{Number(RoverServoDamagePerCollision)}",
		OxygenRegulator => $"{Number(OxygenBaseSecondsPerTank)}→{Number(OxygenBaseSecondsPerTank * OxygenRegulatorTankFactor)} s/tanque",
		_ => "",
	};

	public static string ShortName(string id) => id switch
	{
		SpareFuses => "FUSIBLES",
		FrequencyAnalyzer => "ANALIZADOR",
		PrecisionWelder => "SOLDADOR",
		WirePliers => "ALICATES",
		RoverBumper => "PARACHOQUES",
		RoverServos => "SERVOS",
		OxygenRegulator => "REGULADOR",
		_ => id
	};

	public static string DrawbackText(string id) => id switch
	{
		SpareFuses => "Solo fusibles",
		FrequencyAnalyzer => "Solo frecuencias",
		PrecisionWelder => "Solo casco",
		WirePliers => "Sin temporizador; solo cables",
		RoverBumper => $"Carril {Number(RoverBaseLaneChangeSpeed)} → {Number(RoverBaseLaneChangeSpeed * RoverBumperSpeedFactor)} px/s",
		RoverServos => "Cada choque resta doble",
		OxygenRegulator => $"Solo funciona con {Number(OxygenRegulatorMinimumTanks)}+ tanques",
		_ => "",
	};

	public static string Describe(string id) => id switch
	{
		MaintenanceKit => "Acceso a las cuatro prácticas de reparación. Desventaja: ocupa cargo.",
		OxygenTank => $"Reserva: {Number(OxygenBaseSecondsPerTank)} s por tanque. Desventaja: ocupa cargo.",
		SpareFuses or FrequencyAnalyzer or PrecisionWelder or WirePliers or RoverBumper or RoverServos or OxygenRegulator =>
			$"{BenefitText(id)}. Desventaja: {DrawbackText(id)}.",
		_ => "Sin efecto de herramienta registrado."
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

	private static string Number(double value) => value.ToString("0.##", Spanish);
}

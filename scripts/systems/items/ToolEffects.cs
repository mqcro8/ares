namespace ares.scripts.systems.items;

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
	public const int OxygenTankCargoCost = 1;
	public const double OxygenBaseSecondsPerTank = 180.0;
	public const int OxygenRegulatorMinimumTanks = 2;
	public const double OxygenRegulatorTankFactor = 1.3;

	public const int FuseBasePresses = 4;
	public const int FuseExtraPresses = 1;
	public const float FrequencyBaseThreshold = 0.95f;
	public const float FrequencyThresholdDelta = -0.07f;
	public const float FrequencyBaseHoldSeconds = 0.35f;
	public const float FrequencyAnalyzerHoldSeconds = 0.35f;
	public const float HullBaseWeldTolerance = 14f;
	public const float HullPrecisionWeldTolerance = 20f;
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

	public static string Describe(string id) => id switch
	{
		MaintenanceKit => "Acceso a las cuatro prácticas de reparación. Desventaja: ocupa carga.",
		OxygenTank => $"Reserva: {Number(OxygenBaseSecondsPerTank)} s por tanque. Desventaja: ocupa carga.",
		SpareFuses => $"Fusibles: {Number(FuseBasePresses)} pulsaciones base + {Number(FuseExtraPresses)} de margen ({Number(FusePressesFor(true))} disponibles). Solo caja de fusibles.",
		FrequencyAnalyzer => $"Frecuencia: umbral {Number(FrequencyBaseThreshold)} → {Number(FrequencyAnalyzerThreshold)}; sostén {Number(FrequencyBaseHoldSeconds)} s (sin cambio). Solo frecuencias.",
		PrecisionWelder => $"Casco: soldadura {Number(HullBaseWeldTolerance)} → {Number(HullPrecisionWeldTolerance)} px; calor fuera de costura {Number(HullBaseHeatOffSeam)} → {Number(HullPrecisionHeatOffSeam)}/s. Solo casco.",
		WirePliers => $"Cables: {Number(WirePreconnectedCount)} de {Number(WireConnectionCount)} conexiones lista al iniciar; quedan {Number(WireConnectionsFor(true))}. No hay temporizador. Solo cables.",
		RoverBumper => $"Rover: daño máximo {Number(RoverBaseDamageLimit)} → {Number(RoverBaseDamageLimit + RoverBumperExtraDamageLimit)}; carril {Number(RoverBaseLaneChangeSpeed)} → {Number(RoverBaseLaneChangeSpeed * RoverBumperSpeedFactor)} px/s. Más lento.",
		RoverServos => $"Rover: carril {Number(RoverBaseLaneChangeSpeed)} → {Number(RoverBaseLaneChangeSpeed / RoverServoControlTimeFactor)} px/s; daño por choque {Number(RoverBaseDamagePerCollision)} → {Number(RoverServoDamagePerCollision)}. Cada choque resta más vidas.",
		OxygenRegulator => $"Con {Number(OxygenRegulatorMinimumTanks)}+ tanques: {Number(OxygenBaseSecondsPerTank)} → {Number(OxygenBaseSecondsPerTank * OxygenRegulatorTankFactor)} s por tanque (+{Number((OxygenRegulatorTankFactor - 1) * 100)}%). Sin efecto con menos tanques.",
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

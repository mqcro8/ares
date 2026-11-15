namespace ares.scripts.gateway;

using System.Collections.Generic;
using System.Linq;
using ares.scripts.training;

/// <summary>Gateway-only message history survives minigame scene swaps, not a new session.</summary>
public static class GatewayMission
{
    private static long _session = -1;
    private static readonly HashSet<PracticeSession.Exercise> Alerts = new();
    private static readonly HashSet<PracticeSession.Exercise> Restorations = new();
    private static bool _started, _finished;

    public const string Start = "[MISSION CONTROL — INCOMING TRANSMISSION]\nWARNING: Multiple spacecraft systems have reported critical failures.\nElectrical circuits, power distribution, hull integrity, and communications require immediate attention.\nMISSION OBJECTIVE: REPAIR ALL CRITICAL SYSTEMS.";
    public const string Complete = "MISSION ACCOMPLISHED.\nExcellent work, crew member. The spacecraft is operational and ready to continue its mission.";
    public const string ConnectionRecovered = "CONNECTION RECOVERED: OPPORTUNITY-ROVER\nWe received a connection request, Our last lost Rover has recovered the connection to the spaceship, take the control and drive it to the mars settlemnent.";

    public static string Alert(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "GATEWAY DIAGNOSTICS: ELECTRICAL CIRCUIT FAULT\nSeveral local connections are unresponsive. Reconnect the service circuit remotely from Ares.",
        PracticeSession.Exercise.HullRepair => "GATEWAY DIAGNOSTICS: PRESSURE-CONTROL FAULT\nThe service-compartment pressure controls are unresponsive. Restore remote control; inspect structural seals after landing.",
        PracticeSession.Exercise.Fuses => "GATEWAY DIAGNOSTICS: DISTRIBUTION FAULT\nProtective fuses have tripped. Restore the correct local power configuration from Ares.",
        _ => "GATEWAY DIAGNOSTICS: LOCAL LINK MISALIGNED\nThe Earth uplink remains offline. Recalibrate Gateway's local communications relay."
    };

    public static string Restored(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "LOCAL SERVICE RESTORED\nRemote wiring connections respond. Full surface inspection is still required.",
        PracticeSession.Exercise.HullRepair => "PRESSURE CONTROLS RESTORED\nService-compartment pressure is stable. Gateway's structural seals still require on-site inspection.",
        PracticeSession.Exercise.Fuses => "LOCAL DISTRIBUTION RESTORED\nGateway's service circuits are stable. Main surface power remains offline.",
        _ => "LOCAL RELAY SYNCHRONIZED\nGateway telemetry is available. The Earth uplink remains offline."
    };

    private static void EnsureSession()
    {
        if (_session == PracticeSession.SessionId) return;
        _session = PracticeSession.SessionId;
        Alerts.Clear();
        Restorations.Clear();
        _started = _finished = false;
    }

    public static IEnumerable<string> ArrivalMessages()
    {
        EnsureSession();
        if (!_started) { _started = true; yield return Start; }
        foreach (var exercise in System.Enum.GetValues<PracticeSession.Exercise>())
            if (PracticeSession.CompletedIn(Gateway.ScenePath, exercise) && Restorations.Add(exercise))
                yield return Restored(exercise);
        if (!_finished && System.Enum.GetValues<PracticeSession.Exercise>()
            .All(exercise => PracticeSession.CompletedIn(Gateway.ScenePath, exercise)))
        {
            _finished = true;
            yield return Complete;
        }
    }

    public static string EnterRoom(PracticeSession.Exercise exercise)
    {
        EnsureSession();
        return !PracticeSession.CompletedIn(Gateway.ScenePath, exercise) && Alerts.Add(exercise)
            ? Alert(exercise) : null;
    }
}

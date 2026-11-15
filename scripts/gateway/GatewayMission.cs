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
    public const string Complete = "MISSION STATUS: SUCCESS\nAll critical spacecraft systems restored.\nExcellent work, crew member. The spacecraft is operational and ready to continue its mission.";
    public const string ConnectionRecovered = "CONNECTION RECOVERED: OPPORTUNITY-ROVER\nWe received a connection request, Our last lost Rover has recovered the connection to the spaceship, take the control and drive it to the mars settlemnent.";

    public static string Alert(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "SYSTEM ALERT: ELECTRICAL FAILURE\nElectrical circuit malfunction detected.\nPower connections have been disrupted. Access the Electrical Bay and reconnect the damaged wiring.",
        PracticeSession.Exercise.HullRepair => "CRITICAL ALERT: HULL BREACH\nStructural damage detected. Cabin pressure is at risk.\nProceed to Hull Access and seal the damaged sections before depressurization occurs.",
        PracticeSession.Exercise.Fuses => "SYSTEM ALERT: POWER FAILURE\nPower distribution malfunction detected.\nMultiple protective fuses have failed. Inspect the fuse box and restore the correct electrical configuration.",
        _ => "SYSTEM ALERT: SIGNAL LOST\nCommunication link with Mission Control interrupted.\nSignal frequency mismatch detected. Recalibrate the communications system to reestablish contact."
    };

    public static string Restored(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "SYSTEM RESTORED\nElectrical connections restored. Circuit integrity confirmed.",
        PracticeSession.Exercise.HullRepair => "SYSTEM RESTORED\nHull breach sealed. Pressure stabilized. Structural integrity restored.",
        PracticeSession.Exercise.Fuses => "SYSTEM RESTORED\nPower distribution restored. All circuits operating within safe parameters.",
        _ => "SIGNAL RESTORED\nFrequency synchronized. Communication link with Mission Control reestablished."
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

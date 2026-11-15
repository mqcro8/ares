using Godot;
using System;
using System.Linq;
using ares.scripts.ui.mission_monitor;

public partial class MissionMonitorTest : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"MONITOR MODEL PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static MonitorObjective Objective(MonitorSnapshot snapshot, string id) =>
        snapshot.Objectives.First(item => item.Id == id);

    private void Run()
    {
        var gym = MissionMonitorPresenter.BuildTraining(false, 63.4f, 1, false,
            false, false, false, false, false, false);
        Check(gym.Title == "TRAINING / GYM" && gym.Stamina == 63.4f, "Gym context shows the actual stamina source");
        Check(Objective(gym, "circuit").Text == "CIRCUIT  1/3" &&
            Objective(gym, "circuit").State == MonitorObjectiveState.Active,
            "Partial circuit is active in the gym");
        Check(gym.Objectives.All(item => item.Id is not "energy" and not "integrity" and not "health"),
            "Unsupported ship and health statistics are not invented");

        var directLab = MissionMonitorPresenter.BuildTraining(true, 100, 0, false,
            false, false, false, false, false, false);
        Check(directLab.Stamina == 100, "Direct lab entry shows the mission stamina stat");
        Check(Objective(directLab, "kit").State == MonitorObjectiveState.Active &&
            Objective(directLab, "wires").State == MonitorObjectiveState.Pending,
            "Lab directs the player to the missing kit before repair");

        var kitReady = MissionMonitorPresenter.BuildTraining(true, 80, 2, false,
            true, false, false, false, false, false, "E — Wiring workbench", "Practice cancelled.");
        Check(Objective(kitReady, "kit").State == MonitorObjectiveState.Complete &&
            Objective(kitReady, "wires").State == MonitorObjectiveState.Active &&
            Objective(kitReady, "fuses").State == MonitorObjectiveState.Active,
            "Carrying the kit activates the repair practices without imposing a new order");
        Check(kitReady.Prompt == "E — Wiring workbench" && kitReady.Feedback == "Practice cancelled.",
            "Station prompts and practice feedback reach the monitor model");

        var simulator = MissionMonitorPresenter.BuildTraining(true, 74.5f, 3, true,
            false, true, true, false, false, false,
            frequencyComplete: true, hullRepairComplete: true);
        Check(simulator.Stamina == 74.5f && Objective(simulator, "circuit").State == MonitorObjectiveState.Complete,
            "Lab keeps course progress and stamina visible together");
        Check(!simulator.Objectives.Any(item => item.Id == "kit") &&
            Objective(simulator, "simulator").State == MonitorObjectiveState.Active &&
            Objective(simulator, "launch").State == MonitorObjectiveState.Pending,
            "Kit objective disappears after repairs and simulator becomes the recommendation");

        var launched = MissionMonitorPresenter.BuildTraining(true, 40, 3, true,
            false, true, true, true, true, true, visible: false,
            frequencyComplete: true, hullRepairComplete: true);
        Check(launched.Stamina == 40 && Objective(launched, "launch").State == MonitorObjectiveState.Complete && !launched.Visible,
            "Completed launch, stamina and modal visibility are reflected in the snapshot");
    }
}

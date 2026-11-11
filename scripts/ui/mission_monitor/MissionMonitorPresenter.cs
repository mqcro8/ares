namespace ares.scripts.ui.mission_monitor;

using System;
using System.Collections.Generic;

public enum MonitorObjectiveState
{
    Pending,
    Active,
    Complete,
}

public sealed record MonitorObjective(string Id, string Text, MonitorObjectiveState State);

public sealed record MonitorSnapshot(
    string Title,
    float? Stamina,
    IReadOnlyList<MonitorObjective> Objectives,
    string Prompt,
    string Feedback,
    bool Visible);

/// <summary>Builds the training monitor view-model from actual mission/training state.</summary>
public static class MissionMonitorPresenter
{
    public static MonitorSnapshot BuildTraining(bool inLab, float? stamina, int circuitProgress,
        bool circuitComplete, bool hasRepairKit, bool wiresComplete, bool fusesComplete,
        bool simulatorComplete, bool launchUnlocked, bool launchComplete,
        string prompt = "", string feedback = "", bool visible = true,
        bool frequencyComplete = false, bool hullRepairComplete = false)
    {
        int circuit = Math.Clamp(circuitProgress, 0, 3);
        bool courseDone = circuitComplete || circuit == 3;
        bool coreRepairsDone = wiresComplete && fusesComplete;
        bool repairsDone = coreRepairsDone && frequencyComplete && hullRepairComplete;
        var objectives = new List<MonitorObjective>(8)
        {
            new("circuit", $"CIRCUIT  {circuit}/3", courseDone
                ? MonitorObjectiveState.Complete
                : inLab ? MonitorObjectiveState.Pending : MonitorObjectiveState.Active),
        };

        if (hasRepairKit || !repairsDone)
        {
            objectives.Add(new("kit", "MAINTENANCE KIT", hasRepairKit
                ? MonitorObjectiveState.Complete
                : inLab ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        }

        objectives.Add(new("wires", "WIRING PRACTICE", wiresComplete
            ? MonitorObjectiveState.Complete
            : inLab && hasRepairKit ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        objectives.Add(new("fuses", "FUSE PRACTICE", fusesComplete
            ? MonitorObjectiveState.Complete
            : inLab && hasRepairKit ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        objectives.Add(new("frequency", "FREQUENCY (OPTIONAL)", frequencyComplete
            ? MonitorObjectiveState.Complete
            : inLab && hasRepairKit ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        objectives.Add(new("hull", "HULL REPAIR (OPTIONAL)", hullRepairComplete
            ? MonitorObjectiveState.Complete
            : inLab && hasRepairKit ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        objectives.Add(new("simulator", "LAUNCH SIMULATOR", simulatorComplete
            ? MonitorObjectiveState.Complete
            : inLab && coreRepairsDone ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));
        objectives.Add(new("launch", "LAUNCH PAD", launchComplete
            ? MonitorObjectiveState.Complete
            : inLab && launchUnlocked ? MonitorObjectiveState.Active : MonitorObjectiveState.Pending));

        return new MonitorSnapshot(
            inLab ? "TRAINING / LAB" : "TRAINING / GYM",
            stamina,
            objectives,
            prompt ?? "",
            feedback ?? "",
            visible);
    }
}

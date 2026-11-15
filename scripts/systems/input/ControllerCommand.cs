using System;
using System.Collections.Generic;
using Godot;

namespace Ares.Systems.Controllers;

/// <summary>One semantic command from the phone, e.g. "press fuse 3".</summary>
public readonly struct ControllerCommand
{
    public ControllerCommand(string name, int index, bool hasIndex, float value, bool hasValue)
    {
        Name = name;
        Index = index;
        HasIndex = hasIndex;
        Value = value;
        HasValue = hasValue;
    }

    public string Name { get; }
    public int Index { get; }
    public bool HasIndex { get; }
    public float Value { get; }
    public bool HasValue { get; }

    public override string ToString() =>
        HasValue
            ? $"Command {Name} {Index} {Value:0.##}"
            : HasIndex
                ? $"Command {Name} {Index}"
                : $"Command {Name}";
}

/// <summary>A scene that can consume phone commands while it is active.</summary>
public interface IControllerCommandSink
{
    bool HandleControllerCommand(ControllerCommand command);
}

/// <summary>A scene that can publish the control surface the phone should show.</summary>
public interface IControllerContextSource
{
    void PublishControllerContext();
}

public static class ControllerCommands
{
    public const string WirePick = "wire_pick";
    public const string WireConnect = "wire_connect";
    public const string FusePress = "fuse_press";
    public const string FrequencySelect = "freq_select";
    public const string FrequencySet = "freq_set";
    public const string HullSelect = "hull_select";
    public const string HullRestart = "hull_restart";
    public const string MinigameStart = "minigame_start";
    public const string MinigameCancel = "minigame_cancel";
    public const string LaunchStart = "launch_start";
    public const string LaunchTap = "launch_tap";
    public const string LaunchRetry = "launch_retry";
    public const string EndingSkip = "ending_skip";
    public const string EndingContinue = "ending_continue";
    public const string EndingPause = "ending_pause";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        WirePick,
        WireConnect,
        FusePress,
        FrequencySelect,
        FrequencySet,
        HullSelect,
        HullRestart,
        MinigameStart,
        MinigameCancel,
        LaunchStart,
        LaunchTap,
        LaunchRetry,
        EndingSkip,
        EndingContinue,
        EndingPause,
    };

    public static bool IsSupported(string name) =>
        !string.IsNullOrEmpty(name) && Known.Contains(name);

    /// <summary>Validates the command shape before it reaches a scene.</summary>
    public static bool Validate(ControllerCommand command, out string error)
    {
        error = "";
        if (!IsSupported(command.Name))
        {
            error = $"Unknown command '{command.Name}'.";
            return false;
        }

        switch (command.Name)
        {
            case WirePick:
            case WireConnect:
            case FusePress:
            case FrequencySelect:
                if (!command.HasIndex || command.Index is < 0 or > 7)
                {
                    error = $"{command.Name} needs an index between 0 and 7.";
                    return false;
                }
                if (command.HasValue)
                {
                    error = $"{command.Name} does not take a value.";
                    return false;
                }
                return true;

            case FrequencySet:
                if (!command.HasIndex || command.Index is < 0 or > 2)
                {
                    error = "freq_set needs an index between 0 and 2.";
                    return false;
                }
                if (!command.HasValue || !float.IsFinite(command.Value) ||
                    command.Value < 0f || command.Value > 1f)
                {
                    error = "freq_set needs a value between 0 and 1.";
                    return false;
                }
                return true;

            case HullSelect:
                if (!command.HasIndex || command.Index is < 0 or > 3)
                {
                    error = "hull_select needs a plate index between 0 and 3.";
                    return false;
                }
                if (command.HasValue)
                {
                    error = "hull_select does not take a value.";
                    return false;
                }
                return true;

            default:
                if (command.HasIndex || command.HasValue)
                {
                    error = $"{command.Name} does not take arguments.";
                    return false;
                }
                return true;
        }
    }
}

/// <summary>
/// Dispatches commands to the currently active minigame/overlay. Several
/// subscribers can coexist; the first one that handles a command wins.
/// </summary>
public static class ControllerCommandRouter
{
    private static readonly List<(object Owner, IControllerCommandSink Sink)> Sinks = new();

    public static void Register(object owner, IControllerCommandSink sink)
    {
        if (owner == null || sink == null) return;
        for (int i = 0; i < Sinks.Count; i++)
        {
            if (ReferenceEquals(Sinks[i].Owner, owner))
            {
                Sinks[i] = (owner, sink);
                return;
            }
        }
        Sinks.Add((owner, sink));
    }

    public static void Unregister(object owner)
    {
        if (owner == null) return;
        Sinks.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
    }

    public static bool Dispatch(ControllerCommand command)
    {
        // Snapshot so a handler can safely unregister itself.
        var sinks = Sinks.ToArray();
        foreach (var entry in sinks)
        {
            if (entry.Sink.HandleControllerCommand(command)) return true;
        }
        return false;
    }

    public static void Clear() => Sinks.Clear();

    public static int Count => Sinks.Count;
}

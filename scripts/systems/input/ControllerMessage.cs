using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ares.Systems.Controllers;

public enum ControllerMessageKind
{
    Invalid,
    Input,
    Command,
    ControllerConnected,
    ControllerDisconnected,
    GameConnected,
    GameDisconnected,
    RoomReady,
}

/// <summary>One parsed relay message. The relay forwards phone payloads verbatim.</summary>
public readonly struct ControllerMessage
{
    public static readonly ControllerMessage Invalid = new(ControllerMessageKind.Invalid, "", 0f);

    private ControllerMessage(ControllerMessageKind kind, string action, float strength)
        : this(kind, action, strength, default)
    {
    }

    private ControllerMessage(ControllerMessageKind kind, string action, float strength, ControllerCommand command)
    {
        Kind = kind;
        Action = action;
        Strength = strength;
        Command = command;
    }

    public ControllerMessageKind Kind { get; }
    public string Action { get; }
    public float Strength { get; }
    public ControllerCommand Command { get; }
    public bool IsInput => Kind == ControllerMessageKind.Input;
    public bool IsCommand => Kind == ControllerMessageKind.Command;
    public bool Pressed => Strength > 0f;

    public static ControllerMessage Input(string action, float strength) =>
        new(ControllerMessageKind.Input, action, Mathf.Clamp(strength, 0f, 1f));

    public static ControllerMessage ForCommand(ControllerCommand command) =>
        new(ControllerMessageKind.Command, command.Name, 0f, command);

    public static ControllerMessage Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Invalid;

        var json = new Json();
        if (json.Parse(text) != Error.Ok) return Invalid;
        if (json.Data.VariantType != Variant.Type.Dictionary) return Invalid;

        var data = json.Data.AsGodotDictionary();
        string type = data.TryGetValue("type", out Variant typeValue) ? typeValue.AsString() : "";

        switch (type)
        {
            case "controller_connected": return new(ControllerMessageKind.ControllerConnected, "", 0f);
            case "controller_disconnected": return new(ControllerMessageKind.ControllerDisconnected, "", 0f);
            case "game_connected": return new(ControllerMessageKind.GameConnected, "", 0f);
            case "game_disconnected": return new(ControllerMessageKind.GameDisconnected, "", 0f);
            case "room_ready": return new(ControllerMessageKind.RoomReady, "", 0f);
            case "command": return ParseCommand(data);
            case "":
            case "input":
                return ParseInput(data);
            default:
                return Invalid;
        }
    }

    private static ControllerMessage ParseInput(Godot.Collections.Dictionary data)
    {
        if (!data.TryGetValue("action", out Variant actionValue) || actionValue.VariantType != Variant.Type.String)
            return Invalid;

        string action = actionValue.AsString();
        if (!ControllerActions.IsSupported(action)) return Invalid;

        float strength = 1f;
        if (data.TryGetValue("strength", out Variant strengthValue))
        {
            if (strengthValue.VariantType is not (Variant.Type.Int or Variant.Type.Float)) return Invalid;
            strength = (float)strengthValue.AsDouble();
        }

        if (!float.IsFinite(strength) || strength < 0f || strength > 1f) return Invalid;

        return Input(action, strength);
    }

    private static ControllerMessage ParseCommand(Godot.Collections.Dictionary data)
    {
        if (!data.TryGetValue("name", out Variant nameValue) || nameValue.VariantType != Variant.Type.String)
            return Invalid;

        string name = nameValue.AsString();
        int index = 0;
        bool hasIndex = data.TryGetValue("index", out Variant indexValue);
        if (hasIndex)
        {
            if (indexValue.VariantType is not (Variant.Type.Int or Variant.Type.Float)) return Invalid;
            double rawIndex = indexValue.AsDouble();
            if (!double.IsFinite(rawIndex) || rawIndex != System.Math.Floor(rawIndex)) return Invalid;
            index = (int)rawIndex;
        }

        float value = 0f;
        bool hasValue = data.TryGetValue("value", out Variant valueValue);
        if (hasValue)
        {
            if (valueValue.VariantType is not (Variant.Type.Int or Variant.Type.Float)) return Invalid;
            value = (float)valueValue.AsDouble();
        }

        var command = new ControllerCommand(name, index, hasIndex, value, hasValue);
        if (!ControllerCommands.Validate(command, out _)) return Invalid;
        return ForCommand(command);
    }

    public override string ToString() => Kind switch
    {
        ControllerMessageKind.Input => string.Create(CultureInfo.InvariantCulture, $"Input {Action} {Strength:0.##}"),
        ControllerMessageKind.Command => Command.ToString(),
        _ => Kind.ToString(),
    };
}

public static class ControllerActions
{
    public const string Interact = "interact";
    public const string MinigameAction = "minigame_action";
    public const string MinigameFine = "minigame_fine";
    public const string RotateLeft = "minigame_rotate_left";
    public const string RotateRight = "minigame_rotate_right";
    public const string LaunchHold = "launch_hold";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "move_left",
        "move_right",
        "move_up",
        "move_down",
        "training_jump",
        "hotbar_1",
        "hotbar_2",
        "hotbar_3",
        "hotbar_4",
        "hotbar_5",
        MinigameAction,
        MinigameFine,
        RotateLeft,
        RotateRight,
        LaunchHold,
    };

    /// <summary>interact is a raw E key; everything else must exist in the input map.</summary>
    public static bool IsSupported(string action) =>
        !string.IsNullOrEmpty(action) &&
        (action == Interact || Known.Contains(action) || InputMap.HasAction(action));
}

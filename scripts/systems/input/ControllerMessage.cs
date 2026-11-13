using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ares.Systems.Controllers;

public enum ControllerMessageKind
{
    Invalid,
    Input,
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
    {
        Kind = kind;
        Action = action;
        Strength = strength;
    }

    public ControllerMessageKind Kind { get; }
    public string Action { get; }
    public float Strength { get; }
    public bool IsInput => Kind == ControllerMessageKind.Input;
    public bool Pressed => Strength > 0f;

    public static ControllerMessage Input(string action, float strength) =>
        new(ControllerMessageKind.Input, action, Mathf.Clamp(strength, 0f, 1f));

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

    public override string ToString() => Kind == ControllerMessageKind.Input
        ? string.Create(CultureInfo.InvariantCulture, $"Input {Action} {Strength:0.##}")
        : Kind.ToString();
}

public static class ControllerActions
{
    public const string Interact = "interact";

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
    };

    /// <summary>interact is a raw E key; everything else must exist in the input map.</summary>
    public static bool IsSupported(string action) =>
        !string.IsNullOrEmpty(action) &&
        (action == Interact || Known.Contains(action) || InputMap.HasAction(action));
}

using Godot;
using System.Collections.Generic;

namespace Ares.Systems.Controllers;

/// <summary>
/// Translates controller messages into ordinary Godot input events, so existing
/// gameplay code (Rover, Hotbar, TrainingStations) keeps working unchanged.
/// </summary>
public sealed class ControllerInputBridge
{
    private readonly Dictionary<string, float> _strengths = new();

    public int ActiveActionCount => _strengths.Count;

    public bool Apply(ControllerMessage message) =>
        message.IsInput && Apply(message.Action, message.Strength);

    public bool Apply(string action, float strength)
    {
        if (!ControllerActions.IsSupported(action)) return false;

        strength = Mathf.Clamp(strength, 0f, 1f);
        if (_strengths.TryGetValue(action, out float previous) && Mathf.IsEqualApprox(previous, strength))
            return false;

        if (strength <= 0f) _strengths.Remove(action);
        else _strengths[action] = strength;

        if (action == ControllerActions.Interact)
            InjectInteract(strength > 0f);
        else
            InjectAction(action, strength);

        return true;
    }

    /// <summary>Releases every action the phone was holding, e.g. when it disconnects.</summary>
    public void ReleaseAll()
    {
        if (_strengths.Count == 0) return;

        var actions = new string[_strengths.Count];
        _strengths.Keys.CopyTo(actions, 0);
        foreach (string action in actions) Apply(action, 0f);
    }

    private static void InjectAction(string action, float strength)
    {
        Input.ParseInputEvent(new InputEventAction
        {
            Action = action,
            Pressed = strength > 0f,
            Strength = strength,
        });
    }

    private static void InjectInteract(bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = pressed,
            Echo = false,
        });
    }
}

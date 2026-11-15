using Godot;

namespace Ares.Systems.Controllers;

/// <summary>The control surface the phone should show.</summary>
public enum ControllerContextMode
{
    None,
    Wires,
    Fuses,
    Frequency,
    Hull,
    Launch,
    Ending,
}

/// <summary>
/// Token-scoped current context published by the active minigame. A scene calls
/// <see cref="Begin"/>, <see cref="Update"/> and <see cref="End"/>; calls from a
/// stale owner are ignored so an exiting scene cannot clear a newer one.
/// </summary>
public static class ControllerContext
{
    private static readonly string[] ModeIds =
    {
        "none", "wires", "fuses", "frequency", "hull", "launch", "ending",
    };

    private static object _owner;
    private static ControllerContextMode _mode = ControllerContextMode.None;
    private static Godot.Collections.Dictionary _state = new();
    private static bool _active;
    private static ulong _revision;

    /// <summary>Changes every time the published context changes.</summary>
    public static ulong Revision => _revision;

    public static bool IsActive => _active;

    public static ControllerContextMode Mode => _mode;

    public static string ModeId(ControllerContextMode mode) =>
        mode is >= ControllerContextMode.None and <= ControllerContextMode.Ending
            ? ModeIds[(int)mode]
            : ModeIds[(int)ControllerContextMode.None];

    /// <summary>Starts publishing a context for <paramref name="owner"/>.</summary>
    public static void Begin(object owner, ControllerContextMode mode)
    {
        if (owner == null || mode == ControllerContextMode.None)
        {
            End(owner);
            return;
        }

        _owner = owner;
        _mode = mode;
        _state = new Godot.Collections.Dictionary();
        _active = true;
        _revision++;
    }

    /// <summary>Replaces the published state. Ignored when the owner is stale.</summary>
    public static void Update(object owner, Godot.Collections.Dictionary state)
    {
        if (!IsOwner(owner)) return;
        _state = state ?? new Godot.Collections.Dictionary();
        _revision++;
    }

    /// <summary>Sets a single state key. Ignored when the owner is stale.</summary>
    public static void Set(object owner, string key, Variant value)
    {
        if (!IsOwner(owner) || string.IsNullOrEmpty(key)) return;
        _state[key] = value;
        _revision++;
    }

    /// <summary>Removes a state key. Ignored when the owner is stale.</summary>
    public static void Remove(object owner, string key)
    {
        if (!IsOwner(owner) || string.IsNullOrEmpty(key)) return;
        if (_state.Remove(key)) _revision++;
    }

    /// <summary>Stops publishing when <paramref name="owner"/> is the active one.</summary>
    public static void End(object owner)
    {
        if (owner != null && _owner != null && !ReferenceEquals(owner, _owner)) return;
        if (!_active && _owner == null) return;

        _owner = null;
        _mode = ControllerContextMode.None;
        _state = new Godot.Collections.Dictionary();
        _active = false;
        _revision++;
    }

    /// <summary>Forces a reset, e.g. between tests or on shutdown.</summary>
    public static void Reset()
    {
        _owner = null;
        _mode = ControllerContextMode.None;
        _state = new Godot.Collections.Dictionary();
        _active = false;
        _revision++;
    }

    /// <summary>Serializes the current context for the relay, including "none".</summary>
    public static string BuildMessage()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["type"] = "context",
            ["mode"] = ModeId(_active ? _mode : ControllerContextMode.None),
            ["active"] = _active,
            ["state"] = _active ? _state : new Godot.Collections.Dictionary(),
        };
        return Json.Stringify(payload);
    }

    private static bool IsOwner(object owner) =>
        owner != null && _active && ReferenceEquals(owner, _owner);
}

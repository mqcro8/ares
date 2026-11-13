namespace ares.scripts.training;
using Godot;
using ares.scripts.player;
using System;
using System.Collections.Generic;
using System.Linq;
using ares.scripts.systems.items;

/// <summary>Room-local front zones and nearest usable, release-gated interaction.</summary>
public partial class TrainingStations : Node
{
    public sealed record Target(string Name, Control Zone, string Scene, string Marker,
        PracticeSession.Exercise? Exercise = null, PracticeSession.LaunchMode? Launch = null, string Locked = null,
        ItemCapability? RequiredCapability = null, Func<string> Interact = null, Func<string> Prompt = null)
    {
        public Vector2 Center => Zone.GetGlobalTransform() * (Zone.Size / 2);
        public bool Contains(Vector2 globalPosition)
        {
            Vector2 localPosition = Zone.GetGlobalTransform().AffineInverse() * globalPosition;
            return new Rect2(Vector2.Zero, Zone.Size).HasPoint(localPosition);
        }
    }
    public readonly List<Target> Targets = new();
    public Player Player;
    public TrainingIntro Intro;
    public bool InteractionLocked { get; set; }
    private bool _released, _changing, _travelRequested;
    private string _error = "", _errorTarget = "";
    private Target _near;
    public static string Instructions(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "Drag each cable to the terminal with the matching color. Keyboard: 1–4, Up/Down, Enter.",
        PracticeSession.Exercise.Fuses => "Turn all six switches ON within four presses. DOUBLE toggles the linked pair too.",
        PracticeSession.Exercise.Frequency => "Select amplitude, frequency or phase with 1/2/3. Hold Left/Right to match the target wave; Shift adjusts slowly.",
        PracticeSession.Exercise.HullRepair => "Place each plate over its matching hole, then weld along each seam without overheating.",
        _ => "Complete the repair task."
    };
    public string MonitorPrompt { get; private set; } = "";
    public string ContextPrompt { get; private set; } = "";
    public Vector2 ContextPosition { get; private set; }
    public bool HasContextTarget => _near != null && Usable;
    public string MonitorFeedback => _error != "" ? _error : PracticeSession.Feedback;
    public string CurrentTargetName => _near?.Name ?? "";
    private bool Usable => !_changing && !InteractionLocked && !(Intro?.BriefingActive ?? false) &&
        (Player.TrainingMovement == null || (Player.TrainingMovement.Grounded && Player.TrainingMovement.Height == 0));
    public override void _Process(double delta)
    {
        if (!Input.IsPhysicalKeyPressed(Key.E)) _released = true;
        _near = Usable ? Targets.Where(t => t.Contains(Player.GlobalPosition)).OrderBy(t => t.Center.DistanceSquaredTo(Player.GlobalPosition)).FirstOrDefault() : null;
        if (_error != "" && _near?.Name != _errorTarget) { _error = ""; _errorTarget = ""; }
        string requirement = Requirement(_near);
        string prompt = "";
        if (_near != null)
        {
            if (_near.Locked != null) prompt = _near.Locked;
            else if (requirement != "") prompt = requirement;
            else if (_near.Interact != null) prompt = _near.Prompt?.Invoke() ?? $"E — {_near.Name}";
            else prompt = $"E — {_near.Name}";
        }
        ContextPosition = _near?.Center ?? Vector2.Zero;
        ContextPrompt = _error != "" ? _error : prompt;
        MonitorPrompt = _error == "" ? prompt : "";
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false } key || (key.PhysicalKeycode != Key.E && key.Keycode != Key.E) || !_released || _near == null || !Usable || !_near.Contains(Player.GlobalPosition)) return;
        _error = ""; _errorTarget = "";
        _released = false; _changing = true; GetViewport().SetInputAsHandled();
        if (_near.Locked != null) { _changing = false; _error = _near.Locked; _errorTarget = _near.Name; return; }
        string requirement = Requirement(_near);
        if (requirement != "") { _changing = false; _error = requirement; _errorTarget = _near.Name; return; }
        if (_near.Interact != null) { _error = _near.Interact(); _errorTarget = _near.Name; _changing = false; return; }
        var root = GetTree().CurrentScene;
        PracticeSession.ClearFeedback();
        PracticeSession.Capture(root.SceneFilePath, Player);
        if (Intro != null) PracticeSession.Saved = new(Intro.Course.Capture(), Player.TrainingMovement.Capture(), Intro.CompletionShown);
        if (_near.Exercise.HasValue) PracticeSession.Begin(_near.Exercise.Value, Player.GlobalPosition);
        else if (_near.Launch.HasValue)
            PracticeSession.RequestLaunch(_near.Launch.Value, Player.GlobalPosition);
        else
            PracticeSession.Prepare(root.SceneFilePath, _near.Scene, _near.Marker, Player.GlobalPosition);
        Player.Velocity = Vector2.Zero;
        Player.SetPhysicsProcess(false);
        var playerAnimation = Player.GetNodeOrNull<PlayerAnimation>("AnimatedSprite2D");
        playerAnimation?.SetProcess(false);
        string target = _near.Scene;
        Callable.From(() =>
        {
            if (!IsInsideTree()) return;
            _travelRequested = true;
            var error = GetTree().ChangeSceneToFile(target);
            if (error == Error.Ok) return;
            _travelRequested = false;
            PracticeSession.Abort(); _changing = false; Player.SetPhysicsProcess(true);
            playerAnimation?.SetProcess(true);
            _error = "Could not open the room. Release E and try again.";
            _errorTarget = _near?.Name ?? "";
            GD.PushError($"Training travel failed: {error}");
        }).CallDeferred();
    }
    private string Requirement(Target target)
    {
        if (target?.RequiredCapability is not ItemCapability capability) return "";
        if (Player.SelectedStack?.Item.HasCapability(capability) == true) return "";
        return Player.Inventory?.HasCapability(capability) == true
            ? "SELECT MAINTENANCE KIT (1-5)"
            : "MAINTENANCE KIT REQUIRED — USE THE SUPPLY LOCKER";
    }
    public override void _ExitTree()
    {
        if (!_travelRequested) PracticeSession.Clear();
    }
}

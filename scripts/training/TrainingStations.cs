namespace ares.scripts.training;
using Godot;
using ares.scripts.player;
using System.Collections.Generic;
using System.Linq;

/// <summary>Room-local front zones and nearest usable, release-gated interaction.</summary>
public partial class TrainingStations : Node
{
    public sealed record Target(string Name, Rect2 Front, string Scene, string Marker, PracticeSession.Exercise? Exercise = null);
    public readonly List<Target> Targets = new();
    public Player Player;
    public TrainingIntro Intro;
    private Label _status, _prompt;
    private bool _released, _changing, _travelRequested;
    private string _error = "";
    private Target _near;
    private bool Usable => !_changing && !(Intro?.BriefingActive ?? false) && (Player.TrainingMovement == null || (Player.TrainingMovement.Grounded && Player.TrainingMovement.Height == 0));
    public static string Instructions(PracticeSession.Exercise exercise) => exercise == PracticeSession.Exercise.Wires
        ? "Connect all four matching colors.\nDrag from left to right with the mouse.\nKeyboard: 1–4 selects a source from top to bottom; Up/Down selects a target; Enter connects.\nMistakes have no penalty."
        : "Turn all six switches ON within four presses.\nClick or press 1–6 (top row, then bottom row).\nThe DOUBLE switch also toggles its listed partner.\nWhen presses run out, the same puzzle resets.";
    public override void _Ready()
    {
        var canvas = new CanvasLayer { Layer = 12 }; AddChild(canvas);
        var box = new VBoxContainer { Position = new(12, 42), MouseFilter = Control.MouseFilterEnum.Ignore }; canvas.AddChild(box);
        _status = new Label(); _prompt = new Label(); box.AddChild(_status); box.AddChild(_prompt);
        foreach (var label in new[] { _status, _prompt }) { label.AddThemeColorOverride("font_outline_color", Colors.Black); label.AddThemeConstantOverride("outline_size", 6); }
    }
    public override void _Process(double delta)
    {
        if (!Input.IsPhysicalKeyPressed(Key.E)) _released = true;
        _near = Usable ? Targets.Where(t => t.Front.HasPoint(Player.GlobalPosition)).OrderBy(t => t.Front.GetCenter().DistanceSquaredTo(Player.GlobalPosition)).FirstOrDefault() : null;
        _status.Visible = _prompt.Visible = !(Intro?.BriefingActive ?? false);
        string Mark(bool done) => done ? "[done]" : "[ ]";
        _status.Text = $"{Mark(PracticeSession.Wires)} Wires   {Mark(PracticeSession.Fuses)} Fuses";
        int cleared = Intro?.Course.ClearedCount ?? PracticeSession.Saved?.Course.Cleared ?? 0;
        _status.Text = $"Circuit {cleared}/3   " + _status.Text;
        if (Intro == null) _status.Text += "\n" + PracticeSession.Feedback;
        if (cleared == 3 && PracticeSession.Wires && PracticeSession.Fuses) _status.Text += "\nTraining complete. Keep practicing at your own pace.";
        _prompt.Text = _near != null ? $"E — {_near.Name}" : Intro != null && Player.Position.Y > 480
            ? "WASD: move  |  Space: jump  |  Cross three obstacles in order. Stand still to rest." : "";
        if (_error != "") _prompt.Text = _error;
        if (Intro != null) Intro.Course.Hud.Visible = !Intro.BriefingActive;
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false } key || (key.PhysicalKeycode != Key.E && key.Keycode != Key.E) || !_released || _near == null || !Usable || !_near.Front.HasPoint(Player.GlobalPosition)) return;
        _error = "";
        _released = false; _changing = true; GetViewport().SetInputAsHandled();
        var root = GetTree().CurrentScene;
        PracticeSession.Capture(root.SceneFilePath, Player);
        if (Intro != null) PracticeSession.Saved = new(Intro.Course.Capture(), Player.TrainingMovement.Capture(), Intro.CompletionShown);
        if (_near.Exercise.HasValue) PracticeSession.Begin(_near.Exercise.Value, Player.GlobalPosition);
        else PracticeSession.Prepare(root.SceneFilePath, _near.Scene, _near.Marker, Player.GlobalPosition);
        Player.Velocity = Vector2.Zero; Player.SetPhysicsProcess(false);
        string target = _near.Scene;
        Callable.From(() =>
        {
            if (!IsInsideTree()) return;
            _travelRequested = true;
            var error = GetTree().ChangeSceneToFile(target);
            if (error == Error.Ok) return;
            _travelRequested = false;
            PracticeSession.Abort(); _changing = false; Player.SetPhysicsProcess(true);
            _error = "Could not open the room. Release E and try again.";
            GD.PushError($"Training travel failed: {error}");
        }).CallDeferred();
    }
    public override void _ExitTree()
    {
        if (!_travelRequested) PracticeSession.Clear();
    }
}

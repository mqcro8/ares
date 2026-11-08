namespace ares.scripts.training;
using Godot;

/// <summary>Instructions inside the real minigame, with standalone completion signals intact.</summary>
public partial class PracticeReturn : CanvasLayer
{
    private Control _game;
    private long _ticket, _session;
    private bool _finished, _starting, _active, _returnRequested;
    private bool _returnFailed;
    // Per-instance loader seam permits deterministic IO-failure regression tests.
    internal System.Func<string, Error> SceneLoader { get; set; }
    private PanelContainer _instructions;
    private Control _overlay;
    private TextureRect _tutorial;
    private Button _start, _back;
    public static void Attach(Control game) => game.AddChild(new PracticeReturn { Name = "PracticeReturn", Layer = 20 });
    public override void _Ready()
    {
        _game = (Control)GetParent();
        _session = PracticeSession.SessionId; _ticket = PracticeSession.EnterExercise(_game);
        _game.SetProcessInput(false);
        if (_game is WireMinigame wire) wire.Completed += Success;
        if (_game is FuseBoxMinigame fuse) fuse.Completed += Success;
        _overlay = new Control(); AddChild(_overlay); _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0.015f, 0.028f, 0.05f, 0.965f) }; _overlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _instructions = new PanelContainer(); _overlay.AddChild(_instructions);
        _instructions.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _instructions.OffsetLeft = -340; _instructions.OffsetRight = 340; _instructions.OffsetTop = -150; _instructions.OffsetBottom = 150;
        _instructions.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("152535"), BorderColor = new Color("75e4ff"), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2 });
        var margin = new MarginContainer(); _instructions.AddChild(margin);
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); margin.AddChild(box);
        box.AddChild(new Label { Text = _game is WireMinigame ? "WIRING PRACTICE" : "FUSE PRACTICE" });
        string tutorialPath = _game is WireMinigame
            ? "res://art/training/tutorials/wires/cable_connection_tutorial.png"
            : "res://art/training/tutorials/fuses/fuse_switch_tutorial.png";
        _tutorial = new TextureRect
        {
            Texture = GD.Load<Texture2D>(tutorialPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        box.AddChild(_tutorial);
        string hint = _game is WireMinigame
            ? "Drag matching colors. Keyboard: 1–4, Up/Down, Enter."
            : "Turn all six ON in four presses. DOUBLE toggles both linked switches.";
        box.AddChild(new Label { Text = hint, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _start = new Button { Text = "Start" }; box.AddChild(_start);
        _start.Pressed += () => { _starting = true; _start.ReleaseFocus(); };
        _start.GrabFocus();
        _back = new Button { Text = _ticket != 0 ? "Cancel and return to lab (Esc)" : "Cancel / restart (Esc)", Position = new(12, 8) };
        AddChild(_back); _back.Hide(); _back.Pressed += Cancel;
        var cancel = new Button { Text = _back.Text }; box.AddChild(cancel); cancel.Pressed += Cancel;
        GetViewport().SizeChanged += FitLayout;
        FitLayout();
    }
    private void FitLayout()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        float halfWidth = Mathf.Min(340, (size.X - 32) / 2);
        _instructions.OffsetLeft = -halfWidth;
        _instructions.OffsetRight = halfWidth;
        float tutorialWidth = Mathf.Max(160, Mathf.Min(480, size.X - 80));
        _tutorial.CustomMinimumSize = new Vector2(tutorialWidth, tutorialWidth * .4f);
        float panelHeight = Mathf.Min(size.Y - 24, tutorialWidth * .4f + 224);
        _instructions.OffsetTop = -panelHeight / 2;
        _instructions.OffsetBottom = panelHeight / 2;
        if (_game is WireMinigame)
        {
            var left = _game.GetNode<Control>("LeftTerminals");
            var right = _game.GetNode<Control>("RightTerminals");
            left.OffsetLeft = size.X * 0.15f;
            left.OffsetRight = left.OffsetLeft + 125;
            right.OffsetRight = size.X * 0.85f;
            right.OffsetLeft = right.OffsetRight - 125;
            Callable.From(() =>
            {
                if (IsInsideTree()) ((WireMinigame)_game).RefreshWireLayout();
            }).CallDeferred();
        }
        else if (_game is FuseBoxMinigame)
        {
            var board = _game.GetNode<Control>("Box");
            float scale = Mathf.Min(1, Mathf.Min((size.Y - 140) / 500, (size.X - 32) / 360));
            board.Scale = Vector2.One * scale;
            board.Position = new Vector2((size.X - 360 * scale) / 2, 120 + (size.Y - 140 - 500 * scale) / 2);
        }
    }
    public override void _Process(double delta)
    {
        if (!_starting || Input.IsPhysicalKeyPressed(Key.E) || Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left)) return;
        for (int i = 0; i < 6; i++) if (Input.IsKeyPressed((Key)((int)Key.Key1 + i))) return;
        _starting = false; _active = true; _overlay.Hide(); _back.Show(); _game.SetProcessInput(true);
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { GetViewport().SetInputAsHandled(); Cancel(); }
    }
    private void Success()
    {
        if (!_active || _finished) return;
        if (_ticket == 0) { _game.SetProcessInput(false); _back.Text = "Complete — restart (Esc)"; return; }
        Finish(true);
    }
    private void Cancel()
    {
        if (_returnFailed) { _returnFailed = false; Callable.From(Return).CallDeferred(); return; }
        if (_ticket == 0) { GetTree().ReloadCurrentScene(); return; }
        Finish(false);
    }
    private void Finish(bool success)
    {
        if (_finished || !PracticeSession.Finish(_session, _ticket, success)) return;
        _finished = true; _starting = false; _game.SetProcessInput(false);
        if (_game is FuseBoxMinigame fuse) fuse.StopPending();
        Callable.From(Return).CallDeferred();
    }
    private void Return()
    {
        if (!IsInsideTree()) return;
        _returnRequested = true;
        var error = SceneLoader != null ? SceneLoader(PracticeSession.LabScene) : GetTree().ChangeSceneToFile(PracticeSession.LabScene);
        if (error == Error.Ok) return;
        _returnRequested = false;
        _returnFailed = true;
        _overlay.Hide();
        _back.Show(); _back.Text = "Return failed — retry return to lab (Esc)";
        _back.GrabFocus();
        GD.PushError($"Practice return failed: {error}");
    }
    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= FitLayout;
        if (_game is WireMinigame wire) wire.Completed -= Success;
        if (_game is FuseBoxMinigame fuse) fuse.Completed -= Success;
        if (_ticket != 0 && !_returnRequested) PracticeSession.Clear();
    }
}

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
    public bool IsActive => _active && !_finished;
    public static bool CanPlay(Control game) => game.IsInsideTree() && !game.GetTree().Paused &&
        (game.GetNodeOrNull<PracticeReturn>("PracticeReturn")?.IsActive ?? true);
    private void SetBoardActive(bool active)
    {
        _active = active;
        _game.SetProcess(active);
        _game.SetPhysicsProcess(active);
        _game.SetProcessInput(active);
        if (!active)
        {
            if (_game is WireMinigame wire) wire.CancelInteraction();
            if (_game is FuseBoxMinigame fuse) fuse.StopPending();
        }
    }
    public static void Attach(Control game) => game.AddChild(new PracticeReturn { Name = "PracticeReturn", Layer = 20 });
    public override void _Ready()
    {
        _game = (Control)GetParent();
        _session = PracticeSession.SessionId; _ticket = PracticeSession.EnterExercise(_game);
        SetBoardActive(false);
        if (_game is WireMinigame wire) wire.Completed += Success;
        if (_game is FuseBoxMinigame fuse) fuse.Completed += Success;
        if (_game is FrequencyBoxMinigame frequency) frequency.Completed += Success;
        if (_game is HullRepairMinigame hull) hull.Completed += Success;
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
        PracticeSession.Exercise exercise = ExerciseForGame();
        box.AddChild(new Label { Text = TitleFor(exercise) });
        string tutorialPath = exercise switch
        {
            PracticeSession.Exercise.Wires => "res://art/training/tutorials/wires/cable_connection_tutorial.png",
            PracticeSession.Exercise.Fuses => "res://art/training/tutorials/fuses/fuse_switch_tutorial.png",
            PracticeSession.Exercise.Frequency => "res://art/training/tutorials/frequency/frequency_tutorial.png",
            PracticeSession.Exercise.HullRepair => "res://art/training/tutorials/hull_repair/hull_repair_tutorial.png",
            _ => null
        };
        if (tutorialPath != null)
        {
            _tutorial = new TextureRect
            {
                Name = "Tutorial",
                Texture = GD.Load<Texture2D>(tutorialPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            box.AddChild(_tutorial);
        }
        box.AddChild(new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = TrainingStations.Instructions(exercise) });
        _start = new Button { Text = "Start" }; box.AddChild(_start);
        _start.Pressed += () => { _starting = true; _start.ReleaseFocus(); };
        _start.GrabFocus();
        _back = new Button { Text = _ticket != 0 ? "Cancel and return to lab" : "Cancel / restart", Position = new(12, 8) };
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
        float tutorialWidth = _tutorial == null ? 0 : Mathf.Max(160, Mathf.Min(480, size.X - 80));
        if (_tutorial != null)
            _tutorial.CustomMinimumSize = new Vector2(tutorialWidth, tutorialWidth * 0.4f);
        float panelHeight = Mathf.Min(size.Y - 24, 180 + tutorialWidth * 0.4f);
        _instructions.OffsetTop = -panelHeight / 2;
        _instructions.OffsetBottom = panelHeight / 2;
        if (_game is WireMinigame)
        {
            var left = _game.GetNode<Control>("LeftTerminals");
            var right = _game.GetNode<Control>("RightTerminals");
            float half = Mathf.Max(80f, Mathf.Min(300f, size.X * 0.5f - 24f));
            left.OffsetLeft = -half + 20;
            left.OffsetRight = -half + 80;
            right.OffsetLeft = half - 80;
            right.OffsetRight = half - 20;
            var leftPlate = _game.GetNodeOrNull<Control>("LeftPlate");
            if (leftPlate != null) { leftPlate.OffsetLeft = -half; leftPlate.OffsetRight = -half + 100; }
            var rightPlate = _game.GetNodeOrNull<Control>("RightPlate");
            if (rightPlate != null) { rightPlate.OffsetLeft = half - 100; rightPlate.OffsetRight = half; }
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
        else if (_game is FrequencyBoxMinigame)
        {
            FitCentered(_game.GetNode<Control>("Box"), new Vector2(320, 520), size, 32);
        }
        else if (_game is HullRepairMinigame)
        {
            FitCentered(_game.GetNode<Control>("Frame"), new Vector2(720, 460), size, 32);
        }
    }

    private static void FitCentered(Control control, Vector2 nativeSize, Vector2 viewport, float padding)
    {
        float scale = Mathf.Min(1f,
            Mathf.Min((viewport.X - padding) / nativeSize.X, (viewport.Y - padding) / nativeSize.Y));
        control.PivotOffset = nativeSize / 2;
        control.Scale = Vector2.One * Mathf.Max(0.35f, scale);
    }

    private PracticeSession.Exercise ExerciseForGame() => _game switch
    {
        WireMinigame => PracticeSession.Exercise.Wires,
        FuseBoxMinigame => PracticeSession.Exercise.Fuses,
        FrequencyBoxMinigame => PracticeSession.Exercise.Frequency,
        HullRepairMinigame => PracticeSession.Exercise.HullRepair,
        _ => PracticeSession.Selected
    };

    private static string TitleFor(PracticeSession.Exercise exercise) => exercise switch
    {
        PracticeSession.Exercise.Wires => "WIRING PRACTICE",
        PracticeSession.Exercise.Fuses => "FUSE PRACTICE",
        PracticeSession.Exercise.Frequency => "FREQUENCY CALIBRATION",
        PracticeSession.Exercise.HullRepair => "HULL REPAIR",
        _ => "TRAINING PRACTICE"
    };
    public override void _Process(double delta)
    {
        if (!_starting || Input.IsPhysicalKeyPressed(Key.E) || Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left)) return;
        for (int i = 0; i < 6; i++) if (Input.IsKeyPressed((Key)((int)Key.Key1 + i))) return;
        foreach (Key key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.W, Key.A, Key.S, Key.D, Key.R })
            if (Input.IsKeyPressed(key)) return;
        _starting = false; SetBoardActive(true); _overlay.Hide(); _back.Show();
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && !_active)
        {
            GetViewport().SetInputAsHandled();
            Cancel();
        }
    }
    private void Success()
    {
        if (!_active || _finished) return;
        if (_ticket == 0) { SetBoardActive(false); _back.Text = "Complete — restart"; return; }
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
        _finished = true; _starting = false; SetBoardActive(false);
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
        _back.Show(); _back.Text = "Return failed — retry return to lab";
        _back.GrabFocus();
        GD.PushError($"Practice return failed: {error}");
    }
    public override void _ExitTree()
    {
        SetBoardActive(false);
        GetViewport().SizeChanged -= FitLayout;
        if (_game is WireMinigame wire) wire.Completed -= Success;
        if (_game is FuseBoxMinigame fuse) fuse.Completed -= Success;
        if (_game is FrequencyBoxMinigame frequency) frequency.Completed -= Success;
        if (_game is HullRepairMinigame hull) hull.Completed -= Success;
        if (_ticket != 0 && !_returnRequested) PracticeSession.Clear();
    }
}

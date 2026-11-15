using Godot;

/// <summary>Persistent fade layer for scene changes. Uses real seconds during slow motion.</summary>
public partial class SceneTransition : CanvasLayer
{
    public static SceneTransition Instance { get; private set; }
    public const float FadeSeconds = 0.6f;
    private ColorRect _cover;
    private bool _busy;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;
        _cover = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _cover.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_cover);
    }

    public async void GoTo(string scenePath)
    {
        if (_busy || string.IsNullOrEmpty(scenePath)) return;
        _busy = true;
        Engine.TimeScale = 1f;
        await Fade(1f);
        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            GD.PushError($"SceneTransition: unable to load {scenePath}: {error}");
            await Fade(0f);
            _busy = false;
            return;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Fade(0f);
        _busy = false;
    }

    private async System.Threading.Tasks.Task Fade(float alpha)
    {
        var tween = CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(_cover, "color:a", alpha, FadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}

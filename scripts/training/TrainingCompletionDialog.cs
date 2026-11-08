namespace ares.scripts.training;

using Godot;
using System;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.player;

/// <summary>One mission-wide completion notice, shown after all four training requirements.</summary>
public partial class TrainingCompletionDialog : CanvasLayer
{
    public Player Player { get; set; }
    public Action Closed { get; set; }

    private bool _previousPhysicsProcessing;
    private bool _ownsPhysicsLock;
    private Button _continue;
    private PanelContainer _panel;

    public static bool Open(Node owner, Player player, Action closed = null)
    {
        if (!MissionState.Instance.TakeTrainingCompletionNotification()) return false;
        owner.AddChild(new TrainingCompletionDialog { Name = "TrainingCompletionDialog", Player = player, Closed = closed });
        return true;
    }

    public override void _Ready()
    {
        Layer = 24;
        _previousPhysicsProcessing = Player.IsPhysicsProcessing();
        _ownsPhysicsLock = true;
        Player.Velocity = Vector2.Zero;
        Player.SetPhysicsProcess(false);

        var shade = new ColorRect { Color = new Color(0.01f, 0.025f, 0.045f, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);

        _panel = new PanelContainer();
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddChild(_panel);
        float halfWidth = Mathf.Min(270, (GetViewport().GetVisibleRect().Size.X - 32) / 2);
        _panel.OffsetLeft = -halfWidth;
        _panel.OffsetRight = halfWidth;
        _panel.OffsetTop = -110;
        _panel.OffsetBottom = 110;
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("152535"), BorderColor = new Color("75e4ff"),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
        });

        var margin = new MarginContainer(); _panel.AddChild(margin);
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 18);
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 12); margin.AddChild(column);
        var title = new Label { Text = "MISSION CONTROL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeColorOverride("font_color", new Color("75e4ff"));
        title.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(title);
        var message = new Label
        {
            Text = CompletionMessage(),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 86)
        };
        column.AddChild(message);
        _continue = new Button { Text = "Continue", CustomMinimumSize = new Vector2(0, 34) };
        _continue.Pressed += Close;
        column.AddChild(_continue);
        _continue.GrabFocus();
    }

    private static string CompletionMessage()
    {
        var sequence = GD.Load<DialogueSequence>("res://data/dialogue/training_completion.tres");
        return sequence != null && sequence.Lines.Count > 0
            ? sequence.Lines[0].Text
            : "All training is complete.";
    }

    private void Close()
    {
        if (_ownsPhysicsLock && GodotObject.IsInstanceValid(Player))
        {
            Player.TrainingMovement?.SuppressDialogueJump();
            Player.SetPhysicsProcess(_previousPhysicsProcessing);
            _ownsPhysicsLock = false;
        }
        Closed?.Invoke();
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_continue)) _continue.Pressed -= Close;
        if (_ownsPhysicsLock && GodotObject.IsInstanceValid(Player))
            Player.SetPhysicsProcess(_previousPhysicsProcessing);
        _ownsPhysicsLock = false;
    }
}

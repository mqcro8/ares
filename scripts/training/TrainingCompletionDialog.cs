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

    private readonly TrainingPlayerLock _physicsLock = new();
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
        _physicsLock.Acquire(Player);

        var shade = new ColorRect { Color = new Color(0.01f, 0.025f, 0.045f, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);

		_panel = new PanelContainer();
		AddChild(_panel);
		DialoguePanelStyle.ApplyPanel(_panel);
		FitPanel();
		GetViewport().SizeChanged += FitPanel;

		var margin = new MarginContainer(); _panel.AddChild(margin);
		DialoguePanelStyle.ApplyMargins(margin);
		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		DialoguePanelStyle.ApplyColumn(column); margin.AddChild(column);
		var title = new Label { Text = "MISSION CONTROL", HorizontalAlignment = HorizontalAlignment.Center };
		DialoguePanelStyle.ApplySpeaker(title);
		column.AddChild(title);
        var message = new Label
        {
            Text = CompletionLine().Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 86)
        };
		DialoguePanelStyle.ApplyBody(message);
		column.AddChild(message);
		_continue = new Button { Text = "Continue", CustomMinimumSize = new Vector2(0, 40) };
		DialoguePanelStyle.ApplyAction(_continue);
        _continue.Pressed += Close;
        column.AddChild(_continue);
        _continue.GrabFocus();
    }

	public static DialogueLine CompletionLine(DialogueSequence sequence = null)
    {
        sequence ??= GD.Load<DialogueSequence>("res://data/dialogue/training_completion.tres");
        return sequence != null && sequence.Lines.Count > 0
            ? sequence.Lines[0]
            : new DialogueLine { Speaker = "MISSION CONTROL", Text = "All training is complete." };
	}

	private void FitPanel() => DialoguePanelStyle.FitBottom(_panel, GetViewport().GetVisibleRect().Size);

    private void Close()
    {
        _physicsLock.Release();
        Closed?.Invoke();
        QueueFree();
    }

	public override void _ExitTree()
	{
		if (GetViewport() != null) GetViewport().SizeChanged -= FitPanel;
        if (GodotObject.IsInstanceValid(_continue)) _continue.Pressed -= Close;
        _physicsLock.Release();
    }
}

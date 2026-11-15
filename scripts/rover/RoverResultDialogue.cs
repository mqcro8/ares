namespace ares.scripts.rover;

using Godot;
using ares.scripts.dialogue;

/// <summary>Shared, pausable presentation for rover success and failure screens.</summary>
public partial class RoverResultDialogue : Node
{
	public Label Title { get; set; }
	public Label Text { get; set; }
	public PanelContainer Panel { get; set; }
	public Control Actions { get; set; }
	public string[] Lines { get; set; }
	private CanvasLayer _dialogueLayer;
	private Label _speaker;
	private double _remaining = 2;
	private int _line;
	private bool _showingDialogue;
	private bool _waitingForTitle;

	public override void _Ready()
	{
		if (Panel == null) return;
		_speaker = Panel.GetNodeOrNull<Label>("Margin/VBox/Speaker");
		var body = Text;
		var margin = Panel.GetNodeOrNull<MarginContainer>("Margin");
		var column = Panel.GetNodeOrNull<VBoxContainer>("Margin/VBox");
		var continueButton = Panel.GetNodeOrNull<Button>("Margin/VBox/Continue");

		_dialogueLayer = new CanvasLayer { Name = "RoverDialogueLayer", Layer = 30 };
		GetParent()?.AddChild(_dialogueLayer);
		Panel.Reparent(_dialogueLayer, false);
		Title?.Reparent(_dialogueLayer, false);
		Actions?.Reparent(_dialogueLayer, false);

		DialoguePanelStyle.ApplyPanel(Panel);
		DialoguePanelStyle.ApplyMargins(margin);
		DialoguePanelStyle.ApplyColumn(column);
		DialoguePanelStyle.ApplySpeaker(_speaker);
		DialoguePanelStyle.ApplyBody(body);
		DialoguePanelStyle.ApplyAction(continueButton);
		FitDialoguePanel();
		GetViewport().SizeChanged += FitDialoguePanel;
	}

	public override void _Process(double delta)
	{
		if (_showingDialogue) return;
		_remaining -= delta;
		if (_remaining > 0) return;
		if (_waitingForTitle)
		{
			Title.Show();
			Actions?.Show();
			SetProcess(false);
		}
		else
		{
			Panel.Show();
			Panel.GetNode<Button>("Margin/VBox/Continue").GrabFocus();
			_showingDialogue = true;
		}
	}

	public void Advance()
	{
		if (!_showingDialogue) return;
		if (_line < Lines.Length)
		{
			Text.Text = Lines[_line++];
			return;
		}
		Panel.Hide();
		_showingDialogue = false;
		_waitingForTitle = true;
		_remaining = 1.5;
	}

	private void FitDialoguePanel() =>
		DialoguePanelStyle.FitBottom(Panel, GetViewport().GetVisibleRect().Size);

	public override void _ExitTree()
	{
		if (GetViewport() != null) GetViewport().SizeChanged -= FitDialoguePanel;
	}
}

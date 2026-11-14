namespace ares.scripts.rover;

using Godot;

/// <summary>Shared, pausable presentation for rover success and failure screens.</summary>
public partial class RoverResultDialogue : Node
{
	public Label Title { get; set; }
	public Label Text { get; set; }
	public PanelContainer Panel { get; set; }
	public Control Actions { get; set; }
	public string[] Lines { get; set; }
	private double _remaining = 2;
	private int _line;
	private bool _showingDialogue;
	private bool _waitingForTitle;

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
}

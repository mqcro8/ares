using Godot;
using System;
using System.Threading.Tasks;

public partial class MissionCompleted : Node2D
{
	private Label _title;
	private Label _text;
	private PanelContainer _panel;
	private int _line = 0;

	public override void _Ready()
	{
		_title = GetNodeOrNull<Label>("Title");
		_text = GetNodeOrNull<Label>("Panel/Margin/VBox/Text");
		_panel = GetNodeOrNull<PanelContainer>("Panel");
		if(_text == null || _title == null || _panel == null)
		{
			GD.PrintErr("stuff didn't load");
		}

		_ = ShowPanel();
	}

	private void _on_continue_pressed()
	{	
		GD.Print($"pressed {_line}");
		Showline();
		_line++;
	}

	private void Showline()
	{
		switch (_line)
		{
			case 0:
				_text.Text = "Rover recovered";
			break;

			case 1:
				_text.Text = "Well done!";
			break;

			case 2:
				_panel.Visible = false;
				_ = ShowTitle();
			break;
		}
	}

	private async Task ShowPanel()
	{
		await ToSignal(
				GetTree().CreateTimer(2),
				SceneTreeTimer.SignalName.Timeout
			);
		_panel.Visible = true;
	}
	private async Task ShowTitle()
	{
		await ToSignal(
				GetTree().CreateTimer(1.5),
				SceneTreeTimer.SignalName.Timeout
			);
		_title.Visible = true;
	}
}

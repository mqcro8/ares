using Godot;
using System;
using System.Threading.Tasks;

public partial class GameOver : Node2D
{
	private Label _title;
	private VBoxContainer _vboxcontainer;
	private Label _text;
	private PanelContainer _panel;
	private int _line = 0;

	public override void _Ready()
	{
		_title = GetNodeOrNull<Label>("Title");
		_vboxcontainer = GetNodeOrNull<VBoxContainer>("VBoxContainer");
		_text = GetNodeOrNull<Label>("Panel/Margin/VBox/Text");
		_panel = GetNodeOrNull<PanelContainer>("Panel");
		if (_title == null || _vboxcontainer == null)
		{
			GD.PrintErr("container didn't load...?");
		}

		_ = ShowPanel();
	}

	private async Task ShowContainer()
	{
		await ToSignal(
				GetTree().CreateTimer(1.5),
				SceneTreeTimer.SignalName.Timeout
			);
		_title.Visible = true;
		_vboxcontainer.Visible = true;
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
				_text.Text = "Rover recovery unsuccessful";
			break;

			case 1:
				_text.Text = "Mission failed";
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
		await ShowContainer();
		_title.Visible = true;
	}
	private void _on_retry_pressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/rover/rover.tscn");
	}

	private void _on_main_menu_pressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn");
	}
}

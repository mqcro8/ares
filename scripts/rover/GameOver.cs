using Godot;
using ares.scripts.rover;

public partial class GameOver : Node2D
{
	private RoverResultDialogue _dialogue;

	public override void _Ready()
	{
		_dialogue = new RoverResultDialogue
		{
			Title = GetNode<Label>("Title"),
			Text = GetNode<Label>("Panel/Margin/VBox/Text"),
			Panel = GetNode<PanelContainer>("Panel"),
			Actions = GetNode<Control>("VBoxContainer"),
			Lines = new[] { "Rover recovery unsuccessful", "Mission failed" }
		};
		AddChild(_dialogue);
	}

	private void _on_continue_pressed() => _dialogue.Advance();
	private void _on_retry_pressed() => GetTree().ChangeSceneToFile("res://scenes/rover/rover.tscn");
	private void _on_main_menu_pressed() => GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn");
}

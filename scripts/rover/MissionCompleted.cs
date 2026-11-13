using Godot;
using ares.scripts.rover;

public partial class MissionCompleted : Node2D
{
	private RoverResultDialogue _dialogue;

	public override void _Ready()
	{
		_dialogue = new RoverResultDialogue
		{
			Title = GetNode<Label>("Title"),
			Text = GetNode<Label>("Panel/Margin/VBox/Text"),
			Panel = GetNode<PanelContainer>("Panel"),
			Lines = new[] { "Rover recovered", "Well done!" }
		};
		AddChild(_dialogue);
	}

	private void _on_continue_pressed() => _dialogue.Advance();
}

using Godot;
using System;
using ares.scripts.rover;

public partial class MissionCompleted : Node2D, ares.scripts.systems.IDevSceneSkipTarget
{
	[Export] public string NextScenePath { get; set; } = "";
	[Export] public string MainMenuScene { get; set; } = "res://scenes/main_menu/main_menu_take3.tscn";
	private RoverResultDialogue _dialogue;
	private Button _nextStage, _mainMenu;
	internal Func<string, Error> SceneLoader { get; set; }
	public bool CanSkipWithDevCode => IsInsideTree() &&
		ares.scripts.mission.MissionState.Instance?.RoverStageCompleted == true;

	public override void _Ready()
	{
		_nextStage = GetNode<Button>("Actions/Buttons/NextStage");
		_mainMenu = GetNode<Button>("Actions/Buttons/MainMenu");
		_dialogue = new RoverResultDialogue
		{
			Title = GetNode<Label>("Title"),
			Text = GetNode<Label>("Panel/Margin/VBox/Text"),
			Panel = GetNode<PanelContainer>("Panel"),
			Actions = GetNode<Control>("Actions"),
			Lines = new[] { "Rover recovered", "Well done!" }
		};
		AddChild(_dialogue);
	}

	private void _on_continue_pressed() => _dialogue.Advance();

	private void _on_continue_to_mars_pressed()
	{
		var mission = ares.scripts.mission.MissionState.Instance;
		if (mission?.RoverStageCompleted != true) return;
		if (string.IsNullOrWhiteSpace(NextScenePath) || !ResourceLoader.Exists(NextScenePath))
		{
			_dialogue.Text.Text = "Rover recovered. The next Mars exploration scene is not available in this build yet.";
			_nextStage.Text = "MARS AREA NOT AVAILABLE";
			_nextStage.Disabled = true;
			return;
		}
		var error = SceneLoader != null ? SceneLoader(NextScenePath) : GetTree().ChangeSceneToFile(NextScenePath);
		if (error == Error.Ok) return;
		_dialogue.Text.Text = "Rover recovered. Could not open the next Mars area.";
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		_on_continue_to_mars_pressed();
		return true;
	}

	private void _on_main_menu_pressed()
	{
		var error = SceneLoader != null ? SceneLoader(MainMenuScene) : GetTree().ChangeSceneToFile(MainMenuScene);
		if (error != Error.Ok) _dialogue.Text.Text = "Could not return to Mission Control.";
	}
}

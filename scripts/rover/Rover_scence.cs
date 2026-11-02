namespace ares.scripts.rover;
using System.Threading.Tasks;
using Godot;

public partial class Rover_scence : Node2D
{
	private RoverAnimation _roverAnimation;
	private Rover _rover;
	private Label _label;
	public bool _gameStarted = false;
	public override void _Ready()
	{
		_roverAnimation = GetNodeOrNull<RoverAnimation>("CharacterBody2D/AnimatedSprite2D");
		_rover = GetNodeOrNull<Rover>("CharacterBody2D");
		_label = GetNodeOrNull<Label>("Label");

		if (_roverAnimation == null || _rover == null || _label == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		_roverAnimation.PlayAnimation("idle");
		Blink();

		GD.Print("Node is ready!");
	}

	private async Task Blink()
	{
		while (!_gameStarted)
		{
			_label.Show();

			await ToSignal(
				GetTree().CreateTimer(1),
				SceneTreeTimer.SignalName.Timeout
			);

			if (_gameStarted) break;

			_label.Hide();

			await ToSignal(
				GetTree().CreateTimer(0.4),
				SceneTreeTimer.SignalName.Timeout
			);
		}

		_label.Hide();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey inputKey && inputKey.Pressed)
		{
			if (inputKey.Keycode == Key.Enter || inputKey.Keycode == Key.KpEnter)
			{
				_gameStarted = true;
				StartGame();
			}
		}
	}

	private void StartGame()
	{
		GD.Print("Game started!");

		_rover.CanMove = true;
		_roverAnimation.PlayAnimation("walk");
	}

	private void GameOver()
	{
		
	}

	private void Victory()
	{
		
	}
}

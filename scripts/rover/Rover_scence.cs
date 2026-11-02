namespace ares.scripts.rover;

using Godot;

public partial class Rover_scence : Node2D
{
	
	private RoverAnimation _roverAnimation;
	private Rover _rover;
	public override void _Ready()
	{
		GD.Print("Node is ready!");
		_roverAnimation = GetNodeOrNull<RoverAnimation>("CharacterBody2D/AnimatedSprite2D");
		_rover = GetNodeOrNull<Rover>("CharacterBody2D");

		if (_roverAnimation == null || _rover == null )
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		_roverAnimation.PlayAnimation("idle");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey inputKey && inputKey.Pressed)
		{
			if (inputKey.Keycode == Key.Enter || inputKey.Keycode == Key.KpEnter)
			{
				StartGame();
			}
		}
	}

	private void StartGame()
	{
		GD.Print("Game started!");
		_roverAnimation.PlayAnimation("walk");
	}
}

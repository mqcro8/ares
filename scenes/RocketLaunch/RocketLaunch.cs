
using Godot;

public partial class RocketLaunch : Node2D
{
	private const int TargetClicks = 50;
	private const float TimeLimit = 10f;

	private int clicks = 0;
	private float elapsedTime = 0f;
	private bool gameActive = true;

	private Rocket rocket;
	private Background background;

	public override void _Ready()
	{
		rocket = GetNode<Rocket>("Rocket");
		background = GetNode<Background>("Background");
	}

	public override void _Process(double delta)
	{
		if (!gameActive)
			return;

		elapsedTime += (float)delta;

		if (elapsedTime >= TimeLimit)
		{
			FailLaunch();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!gameActive)
			return;

		bool clicked =
			@event is InputEventMouseButton mouse &&
			mouse.ButtonIndex == MouseButton.Left &&
			mouse.Pressed;

		bool pressedSpace =
			@event is InputEventKey key &&
			key.Keycode == Key.Space &&
			key.Pressed &&
			!key.Echo;

		if (clicked || pressedSpace)
		{
			clicks++;
			GD.Print($"Thrust: {clicks}/{TargetClicks}");

			if (clicks >= TargetClicks)
				SuccessfulLaunch();
		}
	}

	private void SuccessfulLaunch()
	{
		gameActive = false;

		GD.Print("ORBITAL INSERTION SUCCESSFUL!");

		rocket.StartLaunch();
		background.BeginAscend();
	}

	private void FailLaunch()
	{
		gameActive = false;

		GD.Print("LAUNCH FAILED!");

		rocket.StopLaunch();
	}
}

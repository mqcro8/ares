
using Godot;

public partial class RocketLaunch : Node2D
{
	private const int TargetClicks = 50;
	private const float TimeLimit = 10f;

	private int clicks = 0;
	private float elapsedTime = 0f;
	private bool gameActive = true;
	private bool launching = false;

	private Rocket rocket;
	private Background background;
	private ProgressBar propulsionBar;

	public override void _Ready()
	{
		rocket = GetNode<Rocket>("Rocket");
		background = GetNode<Background>("Background");

		propulsionBar = GetNode<ProgressBar>(
            "HUD/ProgressBar"
		);

		propulsionBar.MinValue = 0;
		propulsionBar.MaxValue = TargetClicks;
		propulsionBar.Value = 0;

		rocket.StartIgnition();
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

			// Actualizar barra de propulsión
			propulsionBar.Value = clicks;

			GD.Print($"Thrust: {clicks}/{TargetClicks}");

			if (clicks >= TargetClicks)
				SuccessfulLaunch();
		}
	}

	private void SuccessfulLaunch()
	{
		gameActive = false;
		launching = true;

		GD.Print("LAUNCH SUCCESSFUL!");

		rocket.LaunchSuccess();

		rocket.GetNode<AnimatedSprite2D>("Fire")
			.AnimationFinished += OnSuccessAnimationFinished;
	}

	private void OnSuccessAnimationFinished()
	{
		if (!launching)
			return;

		launching = false;

		rocket.StartAscending();
		background.BeginAscend();
	}

	private void FailLaunch()
	{
		gameActive = false;

		GD.Print("LAUNCH FAILED!");

		rocket.LaunchFailed();
	}
}

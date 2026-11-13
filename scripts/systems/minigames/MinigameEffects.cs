using Godot;

/// <summary>Small effects owned by the board: timers disappear with their particles.</summary>
public static class MinigameEffects
{
	public static void Sparks(Node2D effects, Vector2 position, Color color,
		int amount = 24, float lifetime = .5f, float maxVelocity = 200f)
	{
		var particles = new CpuParticles2D
		{
			Position = position,
			OneShot = true,
			Emitting = true,
			Amount = amount,
			Lifetime = lifetime,
			Explosiveness = 1,
			Direction = Vector2.Zero,
			Spread = 180,
			InitialVelocityMin = 80,
			InitialVelocityMax = maxVelocity,
			Gravity = Vector2.Zero,
			ScaleAmountMin = 2,
			ScaleAmountMax = 4,
			Color = color.Lightened(.5f)
		};
		effects.AddChild(particles);
		var cleanup = new Timer { OneShot = true, WaitTime = 1 };
		particles.AddChild(cleanup);
		cleanup.Timeout += particles.QueueFree;
		cleanup.Start();
	}

	public static Timer CompleteAfter(Control board, double seconds, System.Action complete)
	{
		var timer = new Timer { OneShot = true, WaitTime = seconds };
		board.AddChild(timer);
		timer.Timeout += () =>
		{
			if (ares.scripts.training.PracticeReturn.CanPlay(board)) complete();
			timer.QueueFree();
		};
		timer.Start();
		return timer;
	}
}

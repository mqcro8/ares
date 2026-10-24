using Godot;

[GlobalClass]
public partial class Rover : CharacterBody2D
{
	[Export] public float Speed { get; set; } = 200f;

	[Export] public float Acceleration { get; set; } = 1600f;

	[Export] public float Friction { get; set; } = 1400f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		float dt = (float)delta;

		if (direction != Vector2.Zero)
		{
			Velocity = Velocity.MoveToward(direction * Speed, Acceleration * dt);
		}
		else
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * dt);
		}

		MoveAndSlide();
	}
}

using System.Collections.Generic;
using Godot;

// Top-down rover with delayed controls: every command takes LagSeconds to reach the wheels.
// Movement and impact detection only. Counting impacts and changing LagSeconds is the stage's job.
[GlobalClass]
public partial class Rover : CharacterBody2D
{
	[Signal] public delegate void CollidedEventHandler();

	// Only bodies in this group count as impacts (rocks, craters). Map bounds do not.
	private const string HazardGroup = "hazard";

	[Export] public float Speed { get; set; } = 60f;

	[Export] public float Acceleration { get; set; } = 300f;

	[Export] public float Friction { get; set; } = 300f;

	// Seconds between pressing a key and the rover reacting.
	[Export] public float LagSeconds { get; set; } = 0.5f;

	private readonly Queue<(double Time, Vector2 Direction)> _commands = new();
	private Vector2 _executing = Vector2.Zero;
	private double _clock;
	private bool _touchingHazard;

	public override void _PhysicsProcess(double delta)
	{
		_clock += delta;
		_commands.Enqueue((_clock, Input.GetVector("move_left", "move_right", "move_up", "move_down")));

		while (_commands.Count > 0 && _commands.Peek().Time <= _clock - LagSeconds)
			_executing = _commands.Dequeue().Direction;

		float dt = (float)delta;

		Velocity = _executing != Vector2.Zero
			? Velocity.MoveToward(_executing * Speed, Acceleration * dt)
			: Velocity.MoveToward(Vector2.Zero, Friction * dt);

		MoveAndSlide();
		DetectImpact();
	}

	// One impact per contact: staying pressed against a rock does not count again.
	private void DetectImpact()
	{
		bool touching = false;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is Node node && node.IsInGroup(HazardGroup))
				touching = true;
		}

		if (touching && !_touchingHazard)
			EmitSignal("Collided");

		_touchingHazard = touching;
	}
}

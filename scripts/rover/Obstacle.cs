namespace ares.scripts.rover;
using Godot;
using System;

public partial class Obstacle : Area2D
{
	private Sprite2D _sprite;
	private CollisionShape2D _collisionShape;
	private Rover _rover;
	private float _speed;
	private bool _hasHit;

	public event Action Impact;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		BodyEntered += OnBodyEntered;
	}

	public void Configure(Texture2D texture, Rover rover, float speed)
	{
		_sprite.Texture = texture;
		_sprite.Position = Vector2.Zero;
		_collisionShape.Shape = new RectangleShape2D { Size = new Vector2(52, 52) };
		_rover = rover;
		_speed = speed;
		Monitoring = true;
		Monitorable = true;
		Show();
		SetPhysicsProcess(true);
	}

	public void SetSpeed(float speed) => _speed = speed;

	public override void _PhysicsProcess(double delta)
	{
		Position += Vector2.Down * _speed * (float)delta;
		if (GlobalPosition.Y > 420.0f)
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hasHit || body != _rover)
			return;

		_hasHit = true;
		_rover.RegisterObstacleCollision();
		Impact?.Invoke();
		QueueFree();
	}
}

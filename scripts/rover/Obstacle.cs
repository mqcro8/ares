namespace ares.scripts.rover;
using Godot;
using System;

public partial class Obstacle : Area2D
{
	private static readonly float[] LanePositions = { -170.0f, 0.0f, 170.0f };
	private const int DustDevilFrames = 4;

	private Sprite2D _sprite;
	private CollisionShape2D _collisionShape;
	private Rover _rover;
	private float _speed;
	private bool _hasHit;
	private bool _sweepsLanes;
	private int _driftDirection;
	private float _animationTime;

	// Constant sideways speed while sweeping across the lanes.
	[Export] public float DriftSpeed { get; set; } = 170.0f;
	[Export] public float DustDevilFps { get; set; } = 10.0f;

	public event Action Impact;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		BodyEntered += OnBodyEntered;
	}

	public void Configure(Texture2D texture, Rover rover, float speed, bool sweepsLanes = false)
	{
		_sprite.Texture = texture;
		_sprite.Position = Vector2.Zero;
		_collisionShape.Shape = new RectangleShape2D { Size = new Vector2(52, 52) };
		_rover = rover;
		_speed = speed;
		_sweepsLanes = sweepsLanes;

		// Animated obstacles are horizontal frame strips; static ones stay single-frame.
		_sprite.Hframes = sweepsLanes ? DustDevilFrames : 1;
		_sprite.Frame = 0;
		_animationTime = 0f;

		// Start moving inward so edge lanes do not bounce immediately.
		if (Position.X <= LanePositions[0])
			_driftDirection = 1;
		else if (Position.X >= LanePositions[^1])
			_driftDirection = -1;
		else
			_driftDirection = GD.Randf() < 0.5f ? -1 : 1;

		Monitoring = true;
		Monitorable = true;
		Show();
		SetPhysicsProcess(true);
	}

	public void SetSpeed(float speed) => _speed = speed;

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;

		if (_sweepsLanes)
		{
			_animationTime += step;
			int frame = (int)(_animationTime * DustDevilFps) % _sprite.Hframes;
			_sprite.Frame = frame;

			float x = Position.X + _driftDirection * DriftSpeed * step;
			if (x >= LanePositions[^1])
			{
				x = LanePositions[^1];
				_driftDirection = -1;
			}
			else if (x <= LanePositions[0])
			{
				x = LanePositions[0];
				_driftDirection = 1;
			}

			Position = new Vector2(x, Position.Y);
		}

		Position += Vector2.Down * _speed * step;
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

using System.Collections.Generic;
namespace ares.scripts.rover;

using Godot;
using System;

// Top-down rover with delayed controls: every command takes LagSeconds to reach the wheels.
// Movement and impact detection only. Counting impacts and changing LagSeconds is the stage's job.
[GlobalClass]
public partial class Rover : CharacterBody2D
{
	private const int MaxCollisions = 5;
	private const float LagPerCollision = 0.2f;
	private int _collisionCount = 0;
	private float InputLag => _collisionCount * LagPerCollision;
	public event Action<int> CollisionRegistered;
	private bool _gameOver = false;
	public bool IsGameOver => _gameOver;
	private RoverAnimation _roverAnimation;
	public bool CanMove { get; set; } = false;
	public override void _Ready()
	{
		_roverAnimation = GetNodeOrNull<RoverAnimation>("AnimatedSprite2D");
		if (_roverAnimation == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		GD.Print("Node is ready!");
	}

	private enum Lane {Left = 0, Middle = 1, Right = 2}

	private static readonly float[] LanePositions = {-170.0f, 0.0f, 170.0f};

	private Lane _currentLane = Lane.Middle;

	[Export] public float LaneChangeSpeed { get; set; } = 600.0f;
	public override void _PhysicsProcess(double delta)
	{
		if(!CanMove) return;

		HandleInput();

		float targetX = LanePositions[(int)_currentLane];

		Vector2 position = Position;

		position.X = Mathf.MoveToward(position.X, targetX, LaneChangeSpeed * (float)delta);

		Position = position;
	}
	private void HandleInput()
	{
		if (Input.IsActionJustPressed("move_left"))
		{
			if (_currentLane == Lane.Left)
			{
				RegisterCollision();
			}
			else
			{
				_currentLane--;
			}
		}

		if (Input.IsActionJustPressed("move_right"))
		{
			if (_currentLane == Lane.Right)
			{
				RegisterCollision();
			}
			else
			{
				_currentLane++;
			}
		}
	}

	private bool _isColliding = false;
	public void RegisterObstacleCollision()
	{
		if (!_gameOver)
			RegisterCollision();
	}

	private async void RegisterCollision()
	{
		if (_isColliding)
			return;

		_collisionCount++;
		CollisionRegistered?.Invoke(_collisionCount);

		if (_collisionCount >= MaxCollisions)
		{
			LoseRover();
			return;
		}

		_isColliding = true;

		_roverAnimation.Play("collision");

		await ToSignal(_roverAnimation, AnimationPlayer.SignalName.AnimationFinished);

		_roverAnimation.Play("walk");

		_isColliding = false;

		GD.Print($"Collisions: {_collisionCount}");
		GD.Print($"Input lag: {InputLag:F1}s");
	}

	private void LoseRover()
	{
		_gameOver = true;
		_roverAnimation.Play("rover_lost");
		GD.Print("COMMUNICATION LOST. ROVER DESTROYED.");

		// Trigger game over sequence here.
	}
}

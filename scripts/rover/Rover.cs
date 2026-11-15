using System.Collections.Generic;
namespace ares.scripts.rover;

using Ares.Systems.Audio;
using Godot;
using System;
using ares.scripts.mission;
using ares.scripts.systems.items;

// Top-down rover with delayed controls: every command takes LagSeconds to reach the wheels.
// Movement and impact detection only. Counting impacts and changing LagSeconds is the stage's job.
[GlobalClass]
public partial class Rover : CharacterBody2D
{
	private const float BlinkInterval = 0.1f;
	private const float BlinkOpacity = 0.35f;
	private float _blinkTimer;
	private bool _isDimmed;
	private Color _normalModulate;
	private float _controlLag;
	private float _queuedSteerDirection;
	private float _steerQueueTimer;
	public int MaxImpactPoints { get; private set; } = ToolEffects.RoverBaseDamageLimit;
	public int DamagePerCollision { get; private set; } = ToolEffects.RoverBaseDamagePerCollision;
	public float EffectiveLaneChangeSpeed { get; private set; } = ToolEffects.RoverBaseLaneChangeSpeed;
	public int DamagePoints => MissionState.Instance?.RoverImpacts ?? 0;
	public int RemainingLives => Math.Max(0, MaxImpactPoints - DamagePoints);
	public int Collisions => MissionState.Instance?.RoverCollisions ?? 0;
	private float InputLag => _controlLag;
	public float CurrentControlLag => InputLag;
	public bool CanAccelerate { get; private set; }
	public bool HasServos { get; private set; }
	public event Action<int> CollisionRegistered;
	private bool _gameOver = false;
	public bool IsGameOver => _gameOver;
	private RoverAnimation _roverAnimation;
	public bool CanMove { get; set; } = false;
	public bool TutorialEnded { get; set; } = false;
	public override void _Ready()
	{
		_roverAnimation = GetNodeOrNull<RoverAnimation>("AnimatedSprite2D");
		if (_roverAnimation == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}
		_normalModulate = _roverAnimation.Modulate;
		bool hasBumper = MissionState.Instance?.HasEquipped(ToolEffects.RoverBumper) == true;
		bool hasServos = MissionState.Instance?.HasEquipped(ToolEffects.RoverServos) == true;
		HasServos = hasServos;
		CanAccelerate = hasServos;
		MaxImpactPoints = ToolEffects.RoverBaseDamageLimit +
			(hasBumper ? ToolEffects.RoverBumperExtraDamageLimit : 0);
		DamagePerCollision = hasServos ? ToolEffects.RoverServoDamagePerCollision :
			ToolEffects.RoverBaseDamagePerCollision;
		EffectiveLaneChangeSpeed = LaneChangeSpeed *
			(hasBumper ? ToolEffects.RoverBumperSpeedFactor : 1f) /
			(hasServos ? ToolEffects.RoverServoControlTimeFactor : 1f);

	}

	private enum Lane {Left = 0, Middle = 1, Right = 2}

	private static readonly float[] LanePositions = {-170.0f, 0.0f, 170.0f};

	private Lane _currentLane = Lane.Middle;

	public int CurrentLaneIndex => (int)_currentLane;
	public bool IsChangingLane => !Mathf.IsEqualApprox(Position.X, LanePositions[(int)_currentLane]);

	[Export] public float LaneChangeSpeed { get; set; } = ToolEffects.RoverBaseLaneChangeSpeed;
	public override void _PhysicsProcess(double delta)
	{
		if(!CanMove) return;

		if (_steerQueueTimer > 0f)
		{
			_steerQueueTimer -= (float)delta;
			if (_steerQueueTimer <= 0f) ApplySteer(_queuedSteerDirection);
		}
		HandleInput();

		float targetX = LanePositions[(int)_currentLane];

		Vector2 position = Position;

		position.X = Mathf.MoveToward(position.X, targetX, EffectiveLaneChangeSpeed * (float)delta);

		Position = position;
	}

	public override void _Process(double delta)
	{
		if (!_isColliding)
		{
			_blinkTimer = 0f;
			_isDimmed = false;
			_roverAnimation.Modulate = _normalModulate;
			return;
		}

		_blinkTimer += (float)delta;

		if (_blinkTimer >= BlinkInterval)
		{
			_blinkTimer = 0f;
			_isDimmed = !_isDimmed;

			Color modulation = _normalModulate;
			modulation.A *= _isDimmed ? BlinkOpacity : 1f;

			_roverAnimation.Modulate = modulation;
		}
	}
	private void HandleInput()
	{
		if (Input.IsActionJustPressed("move_left"))
		{
			AudioManager.PlaySfx2D(SoundIds.Whoosh, GlobalPosition);
			QueueSteer(-1f);
		}

		if (Input.IsActionJustPressed("move_right"))
		{
			AudioManager.PlaySfx2D(SoundIds.Whoosh, GlobalPosition);
			QueueSteer(1f);
		}
	}

	private void QueueSteer(float direction)
	{
		_queuedSteerDirection = direction;
		_steerQueueTimer = InputLag;
		if (_steerQueueTimer <= 0f) ApplySteer(direction);
	}

	private void ApplySteer(float direction)
	{
		if (direction < 0)
		{
			if (_currentLane == Lane.Left) RegisterCollision();
			else _currentLane--;
		}
		else
		{
			if (_currentLane == Lane.Right) RegisterCollision();
			else _currentLane++;
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

		if (_isColliding || _gameOver)
			return;

		AudioManager.PlaySfx2D(SoundIds.RoverCollision, GlobalPosition);

		
		if (TutorialEnded)
		{
			if (MissionState.Instance != null)
			{
				MissionState.Instance.RoverCollisions++;
				_controlLag = Math.Min(1.2f, _controlLag +
					ToolEffects.RoverBaseControlLagPerCollision *
					(HasServos ? ToolEffects.RoverServoControlLagFactor : 1f));
				MissionState.Instance.RoverImpacts = Math.Min(MaxImpactPoints,
					MissionState.Instance.RoverImpacts + DamagePerCollision);
			}
		}
		CollisionRegistered?.Invoke(DamagePoints);
		

		if (DamagePoints >= MaxImpactPoints)
		{
			LoseRover();
			return;
		}

		_isColliding = true;

		_roverAnimation.Play("collision");

		await ToSignal(_roverAnimation, AnimationPlayer.SignalName.AnimationFinished);

		_isColliding = false;
		
		_roverAnimation.Play("walk");

		GD.Print($"Collisions: {Collisions}");
		GD.Print($"Lane change speed: {EffectiveLaneChangeSpeed:F0}px/s; input lag: {InputLag:F1}s");
	}

	private void LoseRover()
	{
		_gameOver = true;
		CanMove = false;
		_roverAnimation.Play("rover_lost");
		AudioManager.PlaySfx2D(SoundIds.RoverExplode, GlobalPosition);

		GD.Print("COMMUNICATION LOST. ROVER DESTROYED.");

		// Trigger game over sequence here.
	}
}

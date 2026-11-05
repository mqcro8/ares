namespace ares.scripts.rover;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ares.Systems.Audio;
using Godot;

public partial class Rover_scence : Node2D
{
	private RoverAnimation _roverAnimation;
	private Rover _rover;
	private SfxEmitter2D _engineSound;
	private Label _label;
	public bool _gameStarted = false;
	private Hud _hud;
	private Texture2D[] obstacles = new Texture2D[6];
	private Obstacle _obstacleTemplate;
	private float _spawnTimer;
	// At 260 px/s this leaves over a second of clear lane after each obstacle passes.
	private const float ObstacleSpawnInterval = 1.5f;
	private const float DefaultVelocity = 200f;
	private const float MaximumVelocity = 400f;
	private const float MinimumVelocity = DefaultVelocity * 0.5f;
	private const float VelocityChangeRate = 100f;
	private float _levelVelocity = DefaultVelocity;
	private bool _slowingAfterCrash;
	private static readonly float[] ObstacleLanePositions = { -170.0f, 0.0f, 170.0f };
	private readonly RandomNumberGenerator _rng = new();
	private ShaderMaterial _groundMaterial;
	private Control _ground;
	private Camera2D _camera;
	private float _shakeTime;
	private const float ShakeDuration = 0.25f;
	private const float ShakeStrength = 7.0f;
	private bool _gameOver;
	private bool _victory;
	private float _survivalTime;
	private readonly List<Obstacle> _spawnedObstacles = new();

	// 0 disables the timer; useful while testing the scene.
	[Export(PropertyHint.Range, "0,300,1")]
	public float VictoryTime { get; set; } = 30f;
	public override void _Ready()
	{
		_roverAnimation = GetNodeOrNull<RoverAnimation>("CharacterBody2D/AnimatedSprite2D");
		_rover = GetNodeOrNull<Rover>("CharacterBody2D");
		_label = GetNodeOrNull<Label>("Label");
		_hud = GetNodeOrNull<Hud>("HUD");
		_obstacleTemplate = GetNodeOrNull<Obstacle>("Obstacle");
		_ground = GetNode<Control>("ground");
		_groundMaterial = _ground.Material as ShaderMaterial;
		_camera = GetNodeOrNull<Camera2D>("Camera2D");
		_engineSound = GetNodeOrNull<SfxEmitter2D>("CharacterBody2D/EngineSound");

		if (_roverAnimation == null || _rover == null || _label == null || _hud == null || _obstacleTemplate == null || _camera == null || _engineSound == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		AudioManager.PlayMusic(SoundIds.RoverAmbientWind, 1.5);

		_roverAnimation.PlayAnimation("idle");
		_ = Blink();

		GD.Print("Node is ready!");
		_rover.CollisionRegistered += OnCollisionRegistered;

		obstacles[0] = GD.Load<Texture2D>("res://art/rover/obstacle_wreck.png");
		obstacles[1] = GD.Load<Texture2D>("res://art/rover/obstacle_rocks.png");
		obstacles[2] = GD.Load<Texture2D>("res://art/rover/obstacle_debris.png");
		obstacles[3] = GD.Load<Texture2D>("res://art/rover/obstacle_crevasse.png");
		obstacles[4] = GD.Load<Texture2D>("res://art/rover/obstacle_crater.png");
		obstacles[5] = GD.Load<Texture2D>("res://art/rover/obstacle_boulder.png");
		_obstacleTemplate.Hide();
		_obstacleTemplate.Monitoring = false;
		_obstacleTemplate.Monitorable = false;
		_obstacleTemplate.SetPhysicsProcess(false);
		_obstacleTemplate.SetProcess(false);
		_rng.Randomize();
	}

	public override void _Process(double delta)
	{
		UpdateShake((float)delta);

		if (!_gameStarted || _gameOver || _victory)
		{
			return;
		}
		if (_rover.IsGameOver)
		{
			_levelVelocity = DefaultVelocity;
			GameOver();
			return;
		}

		UpdateLevelVelocity((float)delta);

		_survivalTime += (float)delta * (_levelVelocity / DefaultVelocity);
		if (VictoryTime > 0f && _survivalTime >= VictoryTime)
		{
			Victory();
			return;
		}

		_spawnTimer += (float)delta;
		float spawnInterval = ObstacleSpawnInterval * (DefaultVelocity / _levelVelocity);
		if (_spawnTimer >= spawnInterval)
		{
			_spawnTimer -= spawnInterval;
			SpawnObstacle();
		}
	}

	private void SpawnObstacle()
	{
		var obstacle = (Obstacle)_obstacleTemplate.Duplicate();
		AddChild(obstacle);
		obstacle.Scale = new Vector2(1.5f, 1.5f);
		int lane = _rng.RandiRange(0, ObstacleLanePositions.Length - 1);
		obstacle.Position = new Vector2(ObstacleLanePositions[lane], -360.0f);
		obstacle.Impact += OnObstacleImpact;
		obstacle.Configure(obstacles[_rng.RandiRange(0, obstacles.Length - 1)], _rover, _levelVelocity);
		_spawnedObstacles.Add(obstacle);
	}

	// Small decaying camera shake on impact.
	private void OnObstacleImpact()
	{
		if (!_gameStarted || _gameOver || _victory)
		{
			return;
		}

		_shakeTime = ShakeDuration;
	}

	private void UpdateShake(float delta)
	{
		if (_shakeTime <= 0f)
		{
			return;
		}

		_shakeTime -= delta;
		if (_shakeTime <= 0f)
		{
			_shakeTime = 0f;
			_camera.Offset = Vector2.Zero;
			return;
		}

		float amount = ShakeStrength * (_shakeTime / ShakeDuration);
		_camera.Offset = new Vector2(
			_rng.RandfRange(-amount, amount),
			_rng.RandfRange(-amount, amount)
		);
	}

	// HUD 
	private void OnCollisionRegistered(int collisions)
	{
		_hud.UpdateCollisions(collisions);
		_slowingAfterCrash = true;

		if (!_gameOver && !_victory)
		{
			_shakeTime = ShakeDuration;
		}
	}

	private void UpdateLevelVelocity(float delta)
	{
		bool accelerating = Input.IsActionPressed("move_up");
		bool decelerating = Input.IsActionPressed("move_down");

		if (_slowingAfterCrash || (!accelerating && !decelerating))
		{
			_levelVelocity = Mathf.MoveToward(_levelVelocity, DefaultVelocity, VelocityChangeRate * delta);
			if (_slowingAfterCrash && Mathf.IsEqualApprox(_levelVelocity, DefaultVelocity))
				_slowingAfterCrash = false;
		}
		else if (accelerating)
		{
			_levelVelocity = Mathf.MoveToward(_levelVelocity, MaximumVelocity, VelocityChangeRate * delta);
		}
		else if (decelerating)
		{
			_levelVelocity = Mathf.MoveToward(_levelVelocity, MinimumVelocity, VelocityChangeRate * delta);
		}

		float groundHeight = GetGroundHeight();
		_groundMaterial.SetShaderParameter("scroll_speed", new Vector2(0, -_levelVelocity / groundHeight));
		foreach (var obstacle in _spawnedObstacles)
		{
			if (GodotObject.IsInstanceValid(obstacle))
				obstacle.SetSpeed(_levelVelocity);
		}
	}

	private float GetGroundHeight()
	{
		Sprite2D groundSprite = _ground.GetNodeOrNull<Sprite2D>("Sprite2D");
		return groundSprite?.Texture != null ? groundSprite.Texture.GetHeight() * groundSprite.Scale.Y : 648.0f;
	}

	public override void _ExitTree()
	{
		if (_rover != null)
			_rover.CollisionRegistered -= OnCollisionRegistered;

		if (GodotObject.IsInstanceValid(_engineSound))
			_engineSound.Stop(0f);

		AudioManager.StopMusic(0.25);
	}

	// Text "Press Enter to start game"
	private async Task Blink()
	{
		while (!_gameStarted)
		{
			_label.Show();

			await ToSignal(
				GetTree().CreateTimer(1),
				SceneTreeTimer.SignalName.Timeout
			);

			if (_gameStarted) break;

			_label.Hide();

			await ToSignal(
				GetTree().CreateTimer(0.4),
				SceneTreeTimer.SignalName.Timeout
			);
		}

		_label.Hide();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_gameStarted || _gameOver || _victory)
			return;

		if (@event is InputEventKey inputKey && inputKey.Pressed)
		{
			if (inputKey.Keycode == Key.Enter || inputKey.Keycode == Key.KpEnter)
			{
				_gameStarted = true;
				StartGame();
			}
		}
	}

	private void StartGame()
	{
		GD.Print("Game started!");

		_survivalTime = 0f;
		_engineSound?.Play();

		_rover.CanMove = true;
		_roverAnimation.PlayAnimation("walk");
		_spawnTimer = ObstacleSpawnInterval;
		_levelVelocity = DefaultVelocity;
		_slowingAfterCrash = false;
		_groundMaterial.SetShaderParameter("scroll_speed", new Vector2(0, -_levelVelocity / GetGroundHeight()));
	}

	private void GameOver()
	{
		_gameOver = true;
		_engineSound?.Stop(0.25f);
		AudioManager.StopMusic(0.75);
		AudioManager.PlaySfx2D(SoundIds.RoverExplode, _rover.GlobalPosition);
		_groundMaterial.SetShaderParameter("scroll_speed", Vector2.Zero);
	}

	private void Victory()
	{
		if (_victory)
			return;

		_victory = true;
		_engineSound?.Stop(0.25f);
		AudioManager.StopMusic(1.0);
		AudioManager.PlaySfx(SoundIds.RoverVictory);

		_rover.CanMove = false;
		_roverAnimation.PlayAnimation("victory");
		_groundMaterial.SetShaderParameter("scroll_speed", Vector2.Zero);

		foreach (var obstacle in _spawnedObstacles)
		{
			if (GodotObject.IsInstanceValid(obstacle))
				obstacle.QueueFree();
		}
		_spawnedObstacles.Clear();

		_label.Text = "Mission complete";
		_label.Show();
	}
}

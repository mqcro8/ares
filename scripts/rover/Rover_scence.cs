namespace ares.scripts.rover;
using System.Threading.Tasks;
using Godot;

public partial class Rover_scence : Node2D
{
	private RoverAnimation _roverAnimation;
	private Rover _rover;
	private Label _label;
	public bool _gameStarted = false;
	private Hud _hud;
	private Texture2D[] obstacles = new Texture2D[6];
	private Obstacle _obstacleTemplate;
	private float _spawnTimer;
	// At 260 px/s this leaves over a second of clear lane after each obstacle passes.
	private const float ObstacleSpawnInterval = 1.5f;
	private const float EnvironmentSpeed  = 200f;
	private static readonly float[] ObstacleLanePositions = { -170.0f, 0.0f, 170.0f };
	private readonly RandomNumberGenerator _rng = new();
	private ShaderMaterial _groundMaterial;
	private Control _ground;
	public override void _Ready()
	{
		_roverAnimation = GetNodeOrNull<RoverAnimation>("CharacterBody2D/AnimatedSprite2D");
		_rover = GetNodeOrNull<Rover>("CharacterBody2D");
		_label = GetNodeOrNull<Label>("Label");
		_hud = GetNodeOrNull<Hud>("HUD");
		_obstacleTemplate = GetNodeOrNull<Obstacle>("Obstacle");
		_ground = GetNode<Control>("ground");
		_groundMaterial = _ground.Material as ShaderMaterial;

		if (_roverAnimation == null || _rover == null || _label == null || _hud == null || _obstacleTemplate == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		_roverAnimation.PlayAnimation("idle");
		Blink();

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
		if (!_gameStarted)
		{
			return;
		}
		if (_rover.IsGameOver)
		{
			GameOver();
		}

		_spawnTimer += (float)delta;
		if (_spawnTimer >= ObstacleSpawnInterval)
		{
			_spawnTimer -= ObstacleSpawnInterval;
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
		obstacle.Configure(obstacles[_rng.RandiRange(0, obstacles.Length - 1)], _rover, EnvironmentSpeed );
	}

	// HUD 
	private void OnCollisionRegistered(int collisions)
	{
		_hud.UpdateCollisions(collisions);
	}

	public override void _ExitTree()
	{
		_rover.CollisionRegistered -= OnCollisionRegistered;
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

		_rover.CanMove = true;
		_roverAnimation.PlayAnimation("walk");
		_spawnTimer = ObstacleSpawnInterval;
		_groundMaterial.SetShaderParameter("scroll_speed", new Vector2(0, -EnvironmentSpeed / _ground.Size.Y));
	}

	private void GameOver()
	{
		_groundMaterial.SetShaderParameter("scroll_speed", new Vector2(0, 0f));
	}

	private void Victory()
	{
		
	}
}

namespace ares.scripts.rover;
using System;
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
	private Texture2D[] obstacles = new Texture2D[7];
	private Texture2D _dustDevilTexture;
	private Obstacle _obstacleTemplate;
	private float _spawnTimer;
	// At 260 px/s this leaves over a second of clear lane after each obstacle passes.
	private const float ObstacleSpawnInterval = 1.5f;
	private const float DefaultVelocity = 200f;
	private const float MaximumVelocity = 600f;
	private const float MinimumVelocity = DefaultVelocity * 0.5f;
	private const float VelocityChangeRate = 100f;
	private float _levelVelocity = DefaultVelocity;
	private float _groundScroll;
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

	// Tutorial runs before the mission. While active the world can be soft-paused
	// (ground and obstacles frozen) so the player can steer out of danger.
	private bool _tutorialActive;
	private bool _tutorialPaused;
	private bool _tutorialSpeedActive;
	private Obstacle _tutorialObstacle;
	private const float TutorialObstacleStartY = -360.0f;
	private const float TutorialDangerY = -120.0f;
	private const int TutorialLane = 1; // Middle lane, where the rover starts.
	private ColorRect _fadeRect;
	// 0 disables the timer; useful while testing the scene.
	[Export(PropertyHint.Range, "0,300,1")]
	public float VictoryTime { get; set; } = 100f;
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
		_fadeRect = GetNode<ColorRect>("TransitionLayer/FadeRect");

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
		_dustDevilTexture = GD.Load<Texture2D>("res://art/rover/obstacle_dust_devil.png");
		obstacles[6] = _dustDevilTexture;
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

		if (_gameOver || _victory)
		{
			return;
		}

		if (_tutorialActive)
		{
			if (_tutorialPaused)
				return;

			if (_tutorialSpeedActive)
				UpdateLevelVelocity((float)delta);
			else
				_levelVelocity = DefaultVelocity;

			ScrollWorld((float)delta);
			return;
		}

		if (!_gameStarted)
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
		ScrollWorld((float)delta);

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
		Texture2D texture = obstacles[_rng.RandiRange(0, obstacles.Length - 1)];
		obstacle.Configure(texture, _rover, _levelVelocity, texture == _dustDevilTexture);
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
		if(_rover.TutorialEnded)_hud.UpdateCollisions(collisions);
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
	}

	private void ScrollWorld(float delta)
	{
		_groundScroll -= _levelVelocity / GetGroundHeight() * delta;
		_groundMaterial.SetShaderParameter("scroll_offset", new Vector2(0, _groundScroll));
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
		while (!_tutorialActive && !_gameStarted)
		{
			_label.Show();

			await ToSignal(
				GetTree().CreateTimer(1),
				SceneTreeTimer.SignalName.Timeout
			);

			if (_tutorialActive || _gameStarted) return;

			_label.Hide();

			await ToSignal(
				GetTree().CreateTimer(0.4),
				SceneTreeTimer.SignalName.Timeout
			);
		}

		// Only clear the prompt if the tutorial has not taken over the label.
		if (!_tutorialActive)
			_label.Hide();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_tutorialActive || _gameStarted || _gameOver || _victory)
			return;

		if (@event is InputEventKey inputKey && inputKey.Pressed)
		{
			if (inputKey.Keycode == Key.Enter || inputKey.Keycode == Key.KpEnter)
			{
				_tutorialActive = true;
				_ = BeginTutorial();
			}
		}
	}

	// Teaches steering and speed before the real mission begins.
	private async Task BeginTutorial()
	{
		GD.Print("Tutorial started");

		_label.Hide();
		_roverAnimation.PlayAnimation("walk");
		_engineSound?.Play();

		// No steering until the danger warning, so the rover stays in the middle lane.
		_rover.CanMove = false;

		_levelVelocity = DefaultVelocity;
		_slowingAfterCrash = false;
		_groundScroll = 0f;
		_spawnTimer = 0f;
		_survivalTime = 0f;

		await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
		ShowMessage("Obstacle ahead!");
		SpawnTutorialObstacle(TutorialLane);

		await WaitUntil(() =>
			!GodotObject.IsInstanceValid(_tutorialObstacle) ||
			_tutorialObstacle.Position.Y >= TutorialDangerY);

		if (!GodotObject.IsInstanceValid(_tutorialObstacle))
		{
			// Safety net: if the obstacle somehow disappeared, skip straight to the mission.
			StartMission();
			return;
		}

		_tutorialPaused = true;
		_tutorialObstacle.SetPhysicsProcess(false);
		_rover.CanMove = true;

		ShowMessage("Danger! Press 'A' or 'D' to dodge");

		await WaitUntil(() => _rover.CurrentLaneIndex != TutorialLane);
		await WaitUntil(() => !_rover.IsChangingLane);
		

		_tutorialPaused = false;
		if (GodotObject.IsInstanceValid(_tutorialObstacle))
			_tutorialObstacle.SetPhysicsProcess(true);

		HideMessage();

		// Let the obstacle slide past before praising the player.
		await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);

		ShowMessage("Well done!");
		await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);

		ShowMessage("It could take a bit...");
		await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
		ShowMessage("Hold W to speed up");
		_tutorialSpeedActive = true;
		

		bool triedUp = false;
		ulong deadline = Time.GetTicksMsec() + 8000;
		while (!triedUp && Time.GetTicksMsec() < deadline)
		{
			if (Input.IsActionPressed("move_up")) triedUp = true;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		HideMessage();
		_rover.CanMove = false;
		SpawnTutorialObstacle(0);
		SpawnTutorialObstacle(2);
		// Let the obstacle slide past before praising the player.
		await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);

		_rover.CanMove = true;
		ShowMessage("Actually, it can be somewhat dangerous");
		await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
		ShowMessage("Hold S to slow dow");
		
		bool triedDown = false;
		deadline = Time.GetTicksMsec() + 8000;
		while (!triedDown && Time.GetTicksMsec() < deadline)
		{
			if (Input.IsActionPressed("move_down")) triedDown = true;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		ShowMessage("Well done!");
		await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

		_tutorialSpeedActive = false;
		ShowMessage("Mission start!");
		await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

		HideMessage();
		StartMission();
	}

	private void SpawnTutorialObstacle(int Lane)
	{
		_tutorialObstacle = (Obstacle)_obstacleTemplate.Duplicate();
		AddChild(_tutorialObstacle);
		_tutorialObstacle.Scale = new Vector2(1.5f, 1.5f);
		_tutorialObstacle.Position = new Vector2(ObstacleLanePositions[Lane], TutorialObstacleStartY);
		_tutorialObstacle.Impact += OnObstacleImpact;
		_tutorialObstacle.Configure(obstacles[5], _rover, _levelVelocity);
		_spawnedObstacles.Add(_tutorialObstacle);
	}

	private async Task WaitUntil(Func<bool> condition)
	{
		while (!condition())
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private void ShowMessage(string text)
	{
		_label.Text = text;
		_label.Show();
	}

	private void HideMessage()
	{
		_label.Hide();
	}

	private void StartMission()
	{
		GD.Print("Game started!");

		_tutorialActive = false;
		_tutorialPaused = false;
		_tutorialSpeedActive = false;
		_tutorialObstacle = null;
		_gameStarted = true;
		_hud.Visible = true;

		_survivalTime = 0f;
		_spawnTimer = ObstacleSpawnInterval;
		_levelVelocity = DefaultVelocity;
		_slowingAfterCrash = false;

		_rover.CanMove = true;
		_rover.TutorialEnded = true;
		_roverAnimation.PlayAnimation("walk");
	}

	private async Task FadeAndChangeScene(string scenePath)
	{
		Tween tween = CreateTween();

		tween.TweenProperty(
			_fadeRect,
			"color:a",
			5.0f,
			4f
		);

		await ToSignal(tween, Tween.SignalName.Finished);

		GetTree().ChangeSceneToFile(scenePath);
	}
	private async void GameOver()
	{
		_gameOver = true;
		_engineSound?.Stop(0.25f);
		AudioManager.StopMusic(0.75);
		AudioManager.PlaySfx2D(SoundIds.RoverExplode, _rover.GlobalPosition);

		await FadeAndChangeScene("res://scenes/rover/GameOver.tscn");
	}

	private async void Victory()
	{
		if (_victory)
			return;

		_victory = true;
		_engineSound?.Stop(0.25f);
		AudioManager.StopMusic(1.0);
		AudioManager.PlaySfx(SoundIds.RoverVictory);

		_rover.CanMove = false;
		_roverAnimation.PlayAnimation("victory");

		foreach (var obstacle in _spawnedObstacles)
		{
			if (GodotObject.IsInstanceValid(obstacle))
				obstacle.QueueFree();
		}
		_spawnedObstacles.Clear();

		_label.Text = "Mission complete";
		_label.Show();

		await FadeAndChangeScene("res://scenes/rover/MissionCompleted.tscn");
	}
}

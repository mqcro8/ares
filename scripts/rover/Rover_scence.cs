namespace ares.scripts.rover;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ares.Systems.Audio;
using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;

public partial class Rover_scence : Node2D, IRoverMissionSkipTarget, ares.scripts.systems.IDevSceneSkipTarget
{
	private RoverAnimation _roverAnimation;
	private Rover _rover;
	private SfxEmitter2D _engineSound;
	private SfxEmitter2D _windSound;
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
	private long _roverRunSequence;
	private bool _roverWasAlreadyResolved;
	private float _survivalTime;
	private readonly List<Obstacle> _spawnedObstacles = new();

	private Label _text;
	private Label _Speaker;
	private PanelContainer _panel;
	private Button _continue;
	private int _line;

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
	private bool _leaving;
	private readonly List<TaskCompletionSource<bool>> _waits = new();
	private Button _safeRouteButton;
	private Button _shortcutButton;
	private Label _routeTitle;
	private HBoxContainer _routeChoices;
	private int _routeCheckpoint;
	private float _routeObstacleFactor = 1f;
	private bool _routePaused;
	private string _routeName = "STANDARD";
	internal Func<string, Error> SceneLoader { get; set; }
	public bool CanSkipRoverMission => IsInsideTree() && !_gameOver && !_victory &&
		MissionStateForRover?.LaunchSequence == _roverRunSequence &&
		MissionStateForRover?.RoverStageCompleted == false;
	public bool CanSkipWithDevCode => CanSkipRoverMission;
	private ares.scripts.mission.MissionState MissionStateForRover =>
		ares.scripts.mission.MissionState.Instance;
	// 0 disables the timer; useful while testing the scene.
	[Export(PropertyHint.Range, "0,300,1")]
	public float VictoryTime { get; set; } = 100f;
	public override void _Ready()
	{
		_roverWasAlreadyResolved = MissionState.Instance?.RoverStageResolved == true;
		if (MissionState.Instance != null && !_roverWasAlreadyResolved)
		{
			MissionState.Instance.RoverImpacts = MissionState.Instance.RoverCollisions = 0;
			MissionState.Instance.RoverDamageLimit = ToolEffects.RoverBaseDamageLimit +
				(MissionState.Instance.HasEquipped(ToolEffects.RoverBumper) ? ToolEffects.RoverBumperExtraDamageLimit : 0);
		}
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
		_text = GetNodeOrNull<Label>("Panel/Margin/VBox/Text");
		_Speaker = GetNodeOrNull<Label>("Panel/Margin/VBox/Speaker");
		_continue = GetNodeOrNull<Button>("Panel/Margin/VBox/Continue");
		_panel = GetNodeOrNull<PanelContainer>("Panel");

		if (_roverAnimation == null || _rover == null || _label == null || _hud == null || _obstacleTemplate == null || _camera == null || _engineSound == null || _text == null || _Speaker == null || _panel == null || _continue == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}
		var dialogueLayer = new CanvasLayer { Name = "RoverTutorialDialogueLayer", Layer = 30 };
		AddChild(dialogueLayer);
		_panel.Reparent(dialogueLayer, false);
		DialoguePanelStyle.ApplyPanel(_panel);
		DialoguePanelStyle.ApplyMargins(_panel.GetNodeOrNull<MarginContainer>("Margin"));
		DialoguePanelStyle.ApplyColumn(_panel.GetNodeOrNull<VBoxContainer>("Margin/VBox"));
		DialoguePanelStyle.ApplySpeaker(_Speaker);
		DialoguePanelStyle.ApplyBody(_text);
		DialoguePanelStyle.ApplyAction(_continue);
		FitDialoguePanel();
		GetViewport().SizeChanged += FitDialoguePanel;
		if (ares.scripts.mission.MissionState.Instance != null &&
			ares.scripts.mission.MissionState.Instance.Ending == ares.scripts.mission.MissionEnding.None)
		{
			_roverRunSequence = ares.scripts.mission.MissionState.Instance.LaunchSequence;
			ares.scripts.mission.MissionState.Instance.Stage = ares.scripts.mission.MissionStage.Rover;
		}
		_hud.ConfigureMaxImpactPoints(_rover.MaxImpactPoints);
		_hud.UpdateStatus(_rover.CanAccelerate, _rover.CurrentControlLag, _routeName);
		var dialogueColumn = _panel.GetNode<VBoxContainer>("Margin/VBox");
		_routeTitle = new Label
		{
			Text = "CHOOSE YOUR ROUTE",
			HorizontalAlignment = HorizontalAlignment.Center,
			ThemeTypeVariation = "Title",
		};
		_routeChoices = new HBoxContainer();
		_routeChoices.AddThemeConstantOverride("separation", 16);
		_safeRouteButton = MakeRouteButton("SAFE DETOUR\n+TIME  -RISK", new Color("285b43"));
		_shortcutButton = MakeRouteButton("SHORTCUT\n-TIME  +RISK", new Color("713c2d"));
		var safeCard = MakeRouteCard(false, _safeRouteButton);
		var riskyCard = MakeRouteCard(true, _shortcutButton);
		_routeChoices.AddChild(safeCard);
		_routeChoices.AddChild(riskyCard);
		dialogueColumn.AddChild(_routeTitle);
		dialogueColumn.AddChild(_routeChoices);
		_safeRouteButton.Pressed += () => SelectRoute(false);
		_shortcutButton.Pressed += () => SelectRoute(true);
		SetRouteButtons(false);
		if (_roverWasAlreadyResolved)
		{
			_gameOver = true;
			_rover.CanMove = false;
			_roverAnimation.PlayAnimation("rover_lost");
			_obstacleTemplate.Hide();
			_obstacleTemplate.Monitoring = false;
			_obstacleTemplate.Monitorable = false;
			_obstacleTemplate.SetPhysicsProcess(false);
			_obstacleTemplate.SetProcess(false);
			_panel.Hide();
			_label.Hide();
			_ = FadeAndChangeScene("res://scenes/mars/MarsBase.tscn");
			return;
		}

		_ = ShowPanel();

		_rover.CollisionRegistered += OnCollisionRegistered;
		_windSound = new SfxEmitter2D
		{
			Name = "RoverWind",
			SoundId = SoundIds.RoverWindSfx,
			PlayOnReady = false,
			ProcessMode = ProcessModeEnum.Pausable,
		};
		AddChild(_windSound);

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
	private void _on_continue_pressed()
	{	
		_line++;
		Showline();
	}

	private void Showline()
	{
		switch (_line)
		{
			case 1:
				_text.Text = "Bring it back to the operations headquarters";
			break;

			case 2:
				_text.Text = "Be careful. You might find obstacles along the way.";
			break;

			case 3:
				_Speaker.Text = "YOU";
				_text.Text = "Roger that. Will do";
			break;

			case 4:
				_panel.Visible = false;
				_Speaker.Text = "MISSION CONTROL";
				_continue.Visible = false;
				_ = Blink();
			break;
		}
	}

	private async Task ShowPanel()
	{
		_roverAnimation.Play("wakeup");
		await WaitSeconds(0.5);
		AudioManager.PlaySfx2D(SoundIds.RoverPowerUp, GlobalPosition);
		if (!await WaitUntil(() => !_roverAnimation.IsPlaying())) return;
		AudioManager.PlayMusic(SoundIds.RoverAdventure, 1.5);
		_windSound?.Play();
		_roverAnimation.Play("idle");
		_panel.Visible = true;
	}

	public override void _Process(double delta)
	{
		UpdateShake((float)delta);
		_hud.UpdateStatus(_rover.CanAccelerate, _rover.CurrentControlLag, _routeName, _levelVelocity);

		if (_gameOver || _victory)
		{
			return;
		}

		if (_tutorialActive)
		{
			if (!_rover.CanAccelerate && Input.IsActionJustPressed("move_up"))
				_hud.ShowServoHint();
			if (_tutorialPaused)
				return;

			if (_tutorialSpeedActive)
				UpdateLevelVelocity((float)delta);
			else
				_levelVelocity = DefaultVelocity;

			ScrollWorld((float)delta);
			return;
		}

		if (_rover.IsGameOver)
		{
			_levelVelocity = DefaultVelocity;
			GameOver();
			return;
		}
		if (!_gameStarted) return;
		if (!_rover.CanAccelerate && Input.IsActionJustPressed("move_up"))
			_hud.ShowServoHint();
		if (_routePaused) return;

		UpdateLevelVelocity((float)delta);
		ScrollWorld((float)delta);

		_survivalTime += (float)delta * (_levelVelocity / DefaultVelocity);
		if (VictoryTime > 0f && _survivalTime >= VictoryTime)
		{
			Victory();
			return;
		}

		_spawnTimer += (float)delta;
		if (_routeCheckpoint < 2 && _survivalTime >= (_routeCheckpoint + 1) * VictoryTime / 3f)
		{
			OpenRouteCheckpoint();
			return;
		}
		float spawnInterval = ObstacleSpawnInterval * (DefaultVelocity / _levelVelocity) / _routeObstacleFactor;
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
		_hud.UpdateStatus(_rover.CanAccelerate, _rover.CurrentControlLag, _routeName);
		_slowingAfterCrash = true;

		if (!_gameOver && !_victory)
		{
			_shakeTime = ShakeDuration;
		}
	}

	private void UpdateLevelVelocity(float delta)
	{
		bool accelerating = _rover.CanAccelerate && Input.IsActionPressed("move_up");
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

	private void OpenRouteCheckpoint()
	{
		_routePaused = true;
		_rover.CanMove = false;
		_levelVelocity = DefaultVelocity;
		SetObstaclesPaused(true);
		_text.Hide();
		_Speaker.Hide();
		_routeTitle.Show();
		_routeChoices.Show();
		_panel.CustomMinimumSize = new Vector2(0, 250);
		_panel.CustomMaximumSize = new Vector2(-1, 280);
		_panel.Show();
		SetRouteButtons(true);
		_continue.Hide();
	}

	private void SetRouteButtons(bool visible)
	{
		if (_safeRouteButton == null || _shortcutButton == null) return;
		_routeTitle.Visible = visible;
		_routeChoices.Visible = visible;
		_continue.Visible = !visible;
	}

	private static Button MakeRouteButton(string text, Color color)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(210, 62),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		var style = new StyleBoxFlat
		{
			BgColor = color,
			BorderColor = color.Lightened(0.45f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", style);
		button.AddThemeStyleboxOverride("pressed", style);
		return button;
	}

	private static VBoxContainer MakeRouteCard(bool risky, Button button)
	{
		var card = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		card.AddThemeConstantOverride("separation", 4);
		var map = new RouteForkGraphic
		{
			CustomMinimumSize = new Vector2(0, 62),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			RiskyRoute = risky,
		};
		card.AddChild(map);
		card.AddChild(button);
		return card;
	}

	private void SetObstaclesPaused(bool paused)
	{
		foreach (var obstacle in _spawnedObstacles)
		{
			if (!GodotObject.IsInstanceValid(obstacle)) continue;
			obstacle.SetPhysicsProcess(!paused);
		}
	}

	private void SelectRoute(bool shortcut)
	{
		_routeObstacleFactor = shortcut ? 1.6f : 0.65f;
		_routeName = shortcut ? "RISKY SHORTCUT" : "SAFE DETOUR";
		float remaining = Math.Max(0f, VictoryTime - _survivalTime);
		VictoryTime = _survivalTime + remaining * (shortcut ? 0.8f : 1.2f);
		_routeCheckpoint++;
		_routePaused = false;
		SetObstaclesPaused(false);
		_rover.CanMove = true;
		SetRouteButtons(false);
		_text.Show();
		_Speaker.Show();
		_panel.CustomMinimumSize = Vector2.Zero;
		_panel.CustomMaximumSize = new Vector2(-1, 175);
		_panel.Hide();
	}

	private void ScrollWorld(float delta)
	{
		_spawnedObstacles.RemoveAll(obstacle => !GodotObject.IsInstanceValid(obstacle));
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
		if (GetViewport() != null) GetViewport().SizeChanged -= FitDialoguePanel;
		_leaving = true;
		foreach (var wait in _waits.ToArray()) wait.TrySetResult(false);
		_waits.Clear();
		if (_rover != null)
			_rover.CollisionRegistered -= OnCollisionRegistered;

		if (GodotObject.IsInstanceValid(_engineSound))
			_engineSound.Stop(0f);
		if (GodotObject.IsInstanceValid(_windSound))
			_windSound.Stop(0.2f);

		AudioManager.StopMusic(0.25);
	}

	private void FitDialoguePanel() =>
		DialoguePanelStyle.FitBottom(_panel, GetViewport().GetVisibleRect().Size);

	// Text "Press Enter to start game"
	private async Task Blink()
	{
		while (!_tutorialActive && !_gameStarted)
		{
			_label.Show();

			if (!await WaitSeconds(1)) return;

			if (_tutorialActive || _gameStarted) return;

			_label.Hide();

			if (!await WaitSeconds(.4)) return;
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
			if (_line == 4 && (inputKey.Keycode == Key.Enter || inputKey.Keycode == Key.KpEnter))
			{
				_tutorialActive = true;
				_ = BeginTutorial();
			}
		}
	}

	// Teaches steering and speed before the real mission begins.
	private async Task BeginTutorial()
	{

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
		_routeCheckpoint = 0;
		_routeObstacleFactor = 1f;
		_routePaused = false;

		if (!await WaitSeconds(1.1)) return;
		ShowMessage("Obstacle ahead!");
		SpawnTutorialObstacle(TutorialLane);

		if (!await WaitUntil(() =>
			!GodotObject.IsInstanceValid(_tutorialObstacle) ||
			_tutorialObstacle.Position.Y >= TutorialDangerY)) return;

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

		if (!await WaitUntil(() => _rover.CurrentLaneIndex != TutorialLane)) return;
		if (!await WaitUntil(() => !_rover.IsChangingLane)) return;
		
		_rover.CanMove = false;
		_tutorialPaused = false;
		if (GodotObject.IsInstanceValid(_tutorialObstacle))
			_tutorialObstacle.SetPhysicsProcess(true);

		HideMessage();

		// Let the obstacle slide past before praising the player.
		if (!await WaitSeconds(1.1)) return;

		ShowMessage("Well done!");
		if (!await WaitSeconds(2)) return;

		ShowMessage("It could take a bit...");
		if (!await WaitSeconds(2)) return;
		ShowMessage(_rover.CanAccelerate ? "Hold W to speed up" : "Servos unlock W acceleration. Brake with S.");
		_tutorialSpeedActive = true;
		

		if (_rover.CanAccelerate &&
			!await WaitUntil(() => Input.IsActionPressed("move_up"), 8)) return;

		HideMessage();
		_rover.CanMove = false;
		SpawnTutorialObstacle(0);
		SpawnTutorialObstacle(2);
		// Let the obstacle slide past before praising the player.
		if (!await WaitSeconds(2)) return;

		_rover.CanMove = true;
		ShowMessage("Actually, it can be somewhat dangerous");
		if (!await WaitSeconds(2)) return;
		ShowMessage("Hold S to slow down");
		
		if (!await WaitUntil(() => Input.IsActionPressed("move_down"), 8)) return;

		ShowMessage("Well done!");
		if (!await WaitSeconds(.8)) return;

		_tutorialSpeedActive = false;
		ShowMessage("Mission start!");
		if (!await WaitSeconds(.8)) return;

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

	private async Task<bool> WaitUntil(Func<bool> condition, double timeout = double.PositiveInfinity)
	{
		double elapsed = 0;
		while (!_leaving && !condition() && elapsed < timeout)
		{
			if (!await WaitSeconds(.02)) return false;
			elapsed += .02;
		}
		return !_leaving;
	}

	private async Task<bool> WaitSeconds(double seconds, bool allowResult = false)
	{
		if (_leaving || !IsInsideTree()) return false;
		var completion = new TaskCompletionSource<bool>();
		var timer = new Timer { OneShot = true, WaitTime = seconds };
		_waits.Add(completion);
		AddChild(timer);
		timer.Timeout += () => completion.TrySetResult(true);
		timer.Start();
		bool elapsed = await completion.Task;
		_waits.Remove(completion);
		if (GodotObject.IsInstanceValid(timer)) timer.QueueFree();
		return elapsed && !_leaving && (allowResult || (!_gameOver && !_victory));
	}

	private void ShowMessage(string text)
	{
		_text.Text = text;
		_panel.Show();
	}

	private void HideMessage()
	{
		_panel.Hide();
	}

	private void StartMission()
	{

		_tutorialActive = false;
		_tutorialPaused = false;
		_tutorialSpeedActive = false;
		_tutorialObstacle = null;
		_gameStarted = true;
		_hud.Visible = true;

		_survivalTime = 0f;
		_routeCheckpoint = 0;
		_routeObstacleFactor = 1f;
		_routePaused = false;
		_routeName = "STANDARD";
		_spawnTimer = ObstacleSpawnInterval;
		_levelVelocity = DefaultVelocity;
		_slowingAfterCrash = false;

		_rover.CanMove = true;
		SetRouteButtons(false);
		_rover.TutorialEnded = true;
		_roverAnimation.PlayAnimation("walk");
	}

	private async Task FadeAndChangeScene(string scenePath)
	{
		GetNode<CanvasLayer>("TransitionLayer").Visible = true;
		_fadeRect.Color = new Color(0, 0, 0, 0);
		Tween tween = CreateTween();

		tween.TweenProperty(
			_fadeRect,
			"color:a",
			1.0f,
			4f
		);

		if (!await WaitSeconds(4, allowResult: true)) return;
		var error = SceneLoader != null ? SceneLoader(scenePath) : GetTree().ChangeSceneToFile(scenePath);
		if (error == Error.Ok) return;
		GetNode<CanvasLayer>("TransitionLayer").Visible = false;
		ShowMessage(scenePath == "res://scenes/mars/MarsBase.tscn"
			? "ROVER LOST. Continue on foot to Mars."
			: "Unable to open the next scene.");
		var back = new Button { Text = "Return to Mission Control" };
		_hud.AddChild(back);
		if (scenePath == "res://scenes/mars/MarsBase.tscn")
		{
			back.Text = "Continue to Mars";
			back.Pressed += () => _ = FadeAndChangeScene(scenePath);
		}
		else
			back.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn");
		back.GrabFocus();
	}
	private async void GameOver()
	{
		if (MissionStateForRover?.FailRoverStage(_roverRunSequence) != true) return;
		_gameOver = true;
		_engineSound?.Stop(0.25f);
		AudioManager.StopMusic(0.75);
		await FadeAndChangeScene("res://scenes/mars/MarsBase.tscn");
	}

	private async void Victory()
	{
		if (_victory)
			return;
		if (MissionStateForRover?.CompleteRoverStage(_roverRunSequence) != true)
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

	/// <summary>Developer shortcut: skip only this rover stage through its normal success screen.</summary>
	public bool SkipRoverMission()
	{
		if (!CanSkipRoverMission || _rover == null) return false;
		_tutorialActive = false;
		_tutorialPaused = false;
		_tutorialSpeedActive = false;
		_gameStarted = true;
		_rover.TutorialEnded = true;
		_rover.CanMove = false;
		_panel?.Hide();
		_continue?.Hide();
		_label.Text = "ROVER RECOVERED";
		_label.Show();
		Victory();
		return _victory;
	}

	public bool SkipWithDevCode() => SkipRoverMission();
}

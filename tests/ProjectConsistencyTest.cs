using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.rover;
using ares.scripts.systems;
using Ares.Systems.Audio;

/// <summary>Regression coverage for retained scenes, pause UI and board lifecycles.</summary>
public partial class ProjectConsistencyTest : Node
{
	private int _assertions;
	private bool Visual => OS.GetCmdlineUserArgs().Contains("--visual");
	private void Check(bool condition, string message)
	{
		_assertions++;
		if (!condition) throw new InvalidOperationException(message);
	}

	private async Task Frames(int count = 3)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task Wait(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async Task Capture(string filename)
	{
		if (!Visual) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/{filename}.png") == Error.Ok,
			"Dialogue screenshot saved: " + filename);
	}

	private static T Find<T>(Node root, Func<T, bool> match) where T : Node
	{
		if (root is T typed && match(typed)) return typed;
		foreach (Node child in root.GetChildren())
		{
			var result = Find(child, match);
			if (result != null) return result;
		}
		return null;
	}

	public override async void _Ready()
	{
		try
		{
			await RetainedScenes();
			await PauseUi();
			await BoardLifecycle();
			await RoverLocalLives();
			await RoverLifecycle();
			GD.Print($"CONSISTENCY PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private async Task RetainedScenes()
	{
		foreach (string path in new[]
		{
			"res://scripts/Hotbar.tscn", "res://scripts/hotbar_scene.tscn",
			"res://scripts/ui/button_1.tscn", "res://scenes/rover/GameOver.tscn",
			"res://scenes/rover/MissionCompleted.tscn", "res://scenes/main_menu/Control_menu.tscn"
		})
		{
			var node = GD.Load<PackedScene>(path).Instantiate();
			AddChild(node);
			await Frames();
			Check(node.IsInsideTree(), "Retained scene loads: " + path);
			if (node is GameOver || node is MissionCompleted)
			{
				var dialoguePanel = Find<PanelContainer>(node,
					panel => panel.Name == "Panel" && panel.GetParent() is CanvasLayer);
				Check(dialoguePanel != null && Mathf.IsEqualApprox(dialoguePanel.AnchorLeft, 0.08f) &&
					Mathf.IsEqualApprox(dialoguePanel.AnchorRight, 0.92f) &&
					dialoguePanel.CustomMinimumSize.Y == ares.scripts.dialogue.DialoguePanelStyle.StandardHeight,
					"Rover result dialogue uses the shared screen-space panel");
				await Wait(2.1);
				var next = Find<Button>(node, button => button.Name == "Continue");
				Check(next.IsVisibleInTree(), "Rover result dialogue becomes available");
				for (int i = 0; i < 3; i++) next.EmitSignal(BaseButton.SignalName.Pressed);
				await Wait(1.6);
				Check(Find<Label>(node, label => label.Name == "Title").IsVisibleInTree(),
					"Preserved rover signal connections reveal the shared result presentation");
			}
			node.QueueFree();
			await Frames();
		}
		Check(!ResourceLoader.Exists("res://scripts/test_scene.tscn") &&
			!ResourceLoader.Exists("res://scripts/player_scene.tscn"), "Only the approved demo scenes were removed");
	}

	private async Task PauseUi()
	{
		var manager = GetNode<PauseManager>("/root/PauseManager");
		AudioManager.PlayMusic(SoundIds.MenuMusic, 0, true);
		manager.OpenPauseMenu();
		await Frames();
		Check(Find<Label>(manager, l => l.Text == "PAUSED") != null &&
			Find<Button>(manager, b => b.Text == "Resume" && b.HasFocus()) != null,
			"Pause overlay is English and Resume has keyboard focus");
		Check(AudioManager.Instance.GetChildren().OfType<AudioStreamPlayer>().Any(p => p.Playing && p.CanProcess()),
			"Music player keeps processing while the scene tree is paused");
		Find<Button>(manager, b => b.Name == "Controls").EmitSignal(BaseButton.SignalName.Pressed);
		await Frames();
		Check(Find<Label>(manager, l => l.Text.Contains("SPACE — Jump") && l.IsVisibleInTree()) != null,
			"Pause controls submenu contains the real movement controls");
		if (OS.GetCmdlineUserArgs().Contains("--visual"))
		{
			var window = GetWindow();
			var oldSize = window.Size;
			var oldScale = window.ContentScaleSize;
			var oldMode = window.ContentScaleMode;
			window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
			foreach (var size in new[] { new Vector2I(640, 480), new Vector2I(1024, 768), new Vector2I(1280, 720) })
			{
				window.Size = size;
				window.ContentScaleSize = size;
				await Frames(4);
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/consistency-controls-{size.X}x{size.Y}.png") == Error.Ok,
					"Controls screenshot saved");
			}
			window.Size = oldSize;
			window.ContentScaleSize = oldScale;
			window.ContentScaleMode = oldMode;
		}
		manager.Close();
		await Frames();
		Check(!GetTree().Paused && AudioManager.Instance.CurrentMusicId == null,
			"Closing pause stops its music and restores gameplay");
	}

	private async Task BoardLifecycle()
	{
		var board = GD.Load<PackedScene>("res://scenes/minigames/FrequencyBoxMinigame.tscn").Instantiate<FrequencyBoxMinigame>();
		AddChild(board);
		await Frames();
		float initial = board.SelectedValue;
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = true });
		Input.FlushBufferedEvents();
		board._Process(3);
		Check(board.SelectedValue == initial && !board.IsProcessing(),
			"Frequency polling cannot change a board behind its tutorial");
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = false });
		Input.FlushBufferedEvents();
		Find<Button>(board.GetNode("PracticeReturn"), b => b.Text == "Start").EmitSignal(BaseButton.SignalName.Pressed);
		await Frames();
		int completions = 0;
		board.Completed += () => completions++;
		board.MatchThreshold = 0;
		board._Process(1);
		board.QueueFree();
		await Frames();
		await Wait(1);
		Check(completions == 0, "Leaving a completed board cancels its delayed completion callback");

		var hull = GD.Load<PackedScene>("res://scenes/minigames/HullRepairMinigame.tscn").Instantiate<HullRepairMinigame>();
		AddChild(hull);
		await Frames();
		var plates = hull.GetNode<Node2D>("Frame/World/Plates");
		var originalIds = plates.GetChildren().Select(node => node.GetInstanceId()).ToArray();
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = true });
		Input.FlushBufferedEvents();
		hull._Process(3);
		Check(!hull.IsProcessing() && originalIds.SequenceEqual(plates.GetChildren().Select(node => node.GetInstanceId())),
			"Hull cannot restart or rebuild behind its tutorial");
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, PhysicalKeycode = Key.R, Pressed = false });
		Input.FlushBufferedEvents();
		hull.QueueFree();
		await Frames();
	}

	private async Task RoverLifecycle()
	{
		var rover = GD.Load<PackedScene>("res://scenes/rover/rover.tscn").Instantiate<Rover_scence>();
		AddChild(rover);
		await Frames();
		await Wait(2.5);
		var roverDialogue = rover.GetNode<PanelContainer>("RoverTutorialDialogueLayer/Panel");
		Check(roverDialogue.IsVisibleInTree() && Mathf.IsEqualApprox(roverDialogue.AnchorLeft, 0.08f) &&
			Mathf.IsEqualApprox(roverDialogue.AnchorRight, 0.92f) &&
			roverDialogue.CustomMinimumSize.Y == ares.scripts.dialogue.DialoguePanelStyle.StandardHeight,
			$"Rover tutorial uses the shared screen-space dialogue panel (visible={roverDialogue.IsVisibleInTree()}, anchors={roverDialogue.AnchorLeft:F2}/{roverDialogue.AnchorRight:F2}, min={roverDialogue.CustomMinimumSize})");
		await Capture("consistent-rover-dialogue");
		var continueButton = Find<Button>(rover, button => button.Name == "Continue");
		for (int i = 0; i < 4; i++) continueButton.EmitSignal(BaseButton.SignalName.Pressed);
		await Frames();
		rover._UnhandledInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
		var pause = GetNode<PauseManager>("/root/PauseManager");
		pause.OpenPauseMenu();
		await Wait(1.3);
		Check(rover.GetChildren().OfType<Obstacle>().Count() == 1,
			"Rover tutorial cannot spawn obstacles during a global pause");
		pause.Close();
		await Wait(1.25);
		Check(rover.GetChildren().OfType<Obstacle>().Count() > 1,
			"Rover tutorial resumes its wait and spawns after unpausing");
		rover.QueueFree();
		await Frames();
		await Wait(.1);

		rover = GD.Load<PackedScene>("res://scenes/rover/rover.tscn").Instantiate<Rover_scence>();
		rover.SceneLoader = _ => Error.CantOpen;
		AddChild(rover);
		await Frames();
		rover.VictoryTime = .01f;
		rover._gameStarted = true;
		rover._Process(.1);
		await Wait(.2);
		var fade = rover.GetNode<ColorRect>("TransitionLayer/FadeRect");
		Check(fade.IsVisibleInTree() && fade.Color.A > 0 && fade.Color.A < 1,
			"Rover fade is visible and uses a normalized alpha");
		await Wait(4);
		Check(!rover.GetNode<CanvasLayer>("TransitionLayer").Visible &&
			Find<Button>(rover, b => b.Text == "Return to Mission Control") != null,
			"Failed rover transition restores a visible way out");
		rover.QueueFree();
		await Frames();
	}

	private async Task RoverLocalLives()
	{
		var mission = ares.scripts.mission.MissionState.Instance;
		mission.BeginLaunchSequence();
		mission.SpareParts = 17;
		mission.TrainingWiresComplete = true;
		var run = GD.Load<PackedScene>("res://scenes/rover/rover.tscn").Instantiate<Rover_scence>();
		string destination = "";
		run.SceneLoader = path => { destination = path; return Error.Ok; };
		AddChild(run);
		await Frames();
		var rover = run.GetNode<Rover>("CharacterBody2D");
		rover.TutorialEnded = true;
		for (int lost = 1; lost <= 5; lost++)
		{
			rover.RegisterObstacleCollision();
			if (lost < 5)
				await ToSignal(rover.GetNode<RoverAnimation>("AnimatedSprite2D"), AnimatedSprite2D.SignalName.AnimationFinished);
			Check(rover.RemainingLives == 5 - lost,
				$"Rover collision {lost} removes one local heart");
		}
		await Frames();
		Check(rover.IsGameOver && mission.Ending == ares.scripts.mission.MissionEnding.None &&
			mission.RoverStageResolved && !mission.RoverStageCompleted,
			$"Losing rover hearts resolves the stage as a failure without ending the campaign (gameover={rover.IsGameOver}, resolved={mission.RoverStageResolved}, success={mission.RoverStageCompleted}, seq={mission.LaunchSequence}, stage={mission.Stage})");
		Check(mission.Oxygen.IsEmergencyReserve && mission.Oxygen.CapacitySeconds > 0,
			"Rover loss provides a small rescue reserve when no tanks were equipped");
		Check(mission.SpareParts == 17 && mission.TrainingWiresComplete,
			"Rover failure preserves unrelated mission resources and training");
		await Wait(4.1);
		Check(destination == "res://scenes/mars/MarsBase.tscn",
			"Rover defeat continues to the Mars walk without a retry screen");
		run.QueueFree();
		await Frames();
		var mars = GD.Load<PackedScene>("res://scenes/mars/MarsBase.tscn").Instantiate<Node2D>();
		AddChild(mars);
		await Frames();
		var oxygenHud = mars.GetNode<ares.scripts.ui.OxygenHud>("OxygenHud");
		Check(mission.Stage == ares.scripts.mission.MissionStage.Mars && mission.Oxygen.IsActive &&
			mission.Oxygen.IsEmergencyReserve && !oxygenHud.IsEmergencyVisible,
			"Rover rescue arrives at the walk with emergency oxygen instead of bouncing to the menu");
		mars.QueueFree();
		await Frames();

		var reentry = GD.Load<PackedScene>("res://scenes/rover/rover.tscn").Instantiate<Rover_scence>();
		destination = "";
		reentry.SceneLoader = path => { destination = path; return Error.Ok; };
		AddChild(reentry);
		await Frames();
		Check(reentry.GetNode<Rover>("CharacterBody2D").RemainingLives == 0 && destination == "",
			"Re-entering a failed rover cannot reset lives or start another attempt");
		await Wait(4.1);
		Check(destination == "res://scenes/mars/MarsBase.tscn",
			"A resolved rover re-entry redirects to Mars");
		Check(mission.SpareParts == 17 && mission.TrainingWiresComplete &&
			mission.Ending == ares.scripts.mission.MissionEnding.None,
			"Rover failure preserves campaign progression and training");
		reentry.QueueFree();
		await Frames();
		mission.BeginLaunchSequence();
	}
}

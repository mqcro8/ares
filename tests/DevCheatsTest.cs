using Godot;
using System;
using System.Threading.Tasks;
using ares.scripts.mission;
using ares.scripts.rover;
using ares.scripts.systems;
using ares.scripts.training;

public partial class DevCheatsTest : Node, IRoverMissionSkipTarget, IDevSceneSkipTarget
{
	private int _assertions;
	public bool CanSkipRoverMission { get; set; }
	public bool CanSkipWithDevCode => CanSkipRoverMission;
	public int RoverSkips { get; private set; }
	public bool SkipRoverMission()
	{
		if (!CanSkipRoverMission) return false;
		RoverSkips++;
		if (MissionState.Instance != null)
			MissionState.Instance.CompleteRoverStage(MissionState.Instance.LaunchSequence);
		return true;
	}
	public bool SkipWithDevCode() => SkipRoverMission();

	private void Check(bool condition, string message)
	{
		_assertions++;
		if (!condition) throw new InvalidOperationException(message);
	}

	private async Task Frames(int count = 3)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private static void PressKey(Key key, bool shift)
	{
		Input.ParseInputEvent(new InputEventKey
		{
			Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = shift
		});
		Input.ParseInputEvent(new InputEventKey
		{
			Keycode = key, PhysicalKeycode = key, Pressed = false, ShiftPressed = shift
		});
		Input.FlushBufferedEvents();
	}

	public override async void _Ready()
	{
		try
		{
			await Run();
			GD.Print($"DEV CHEATS PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private async Task Run()
	{
		var cheats = GetNode<DevCheats>("/root/DevCheats");
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		mission.ResetTrainingProgress();

		PressKey(Key.A, true);
		PressKey(Key.R, true);
		PressKey(Key.E, true);
		Check(!mission.TrainingWiresComplete,
			"Partial codes do not unlock anything");
		PressKey(Key.S, true);
		await Frames();
		Check(mission.TrainingCourseComplete && mission.TrainingWiresComplete &&
			mission.TrainingFusesComplete && mission.TrainingFrequencyComplete &&
			mission.TrainingHullRepairComplete && mission.TrainingLaunchPracticeComplete,
			"The secret code completes every training exercise");
		Check(mission.LaunchPadUnlocked,
			"The launch pad unlocks once the secret code is used");
		Check(cheats.IsToastVisible, "The shortcut confirms itself on screen");

		PressKey(Key.E, true);
		PressKey(Key.R, true);
		PressKey(Key.A, true);
		PressKey(Key.S, true);
		PressKey(Key.E, true);
		await Frames();
		Check(!mission.TrainingWiresComplete && !mission.TrainingCourseComplete,
			"A second code clears the training progress again");

		PressKey(Key.A, false);
		PressKey(Key.R, false);
		PressKey(Key.E, false);
		PressKey(Key.S, false);
		await Frames();
		Check(!mission.TrainingWiresComplete,
			"The code only works with Shift held so gameplay keys stay free");

		var stations = new TrainingStations
		{
			Name = "CheatStations",
			Player = new ares.scripts.player.Player { Name = "Player" },
		};
		AddChild(stations);
		stations.Targets.Add(new("Launch pad", new Control { Name = "Pad" },
			"res://scenes/RocketLaunch/Launching.tscn", "launch.real", null,
			ares.scripts.training.PracticeSession.LaunchMode.Real,
			() => MissionState.Instance.LaunchPadUnlocked ? null : "LAUNCH PAD: LOCKED"));
		var pad = stations.Targets[^1];
		Check(pad.LockedText == "LAUNCH PAD: LOCKED",
			"The launch pad reports its locked state while the training is incomplete");
		mission.CompleteAllTraining();
		Check(pad.LockedText == null,
			"The launch pad unlocks immediately after the secret code");

		mission.BeginLaunchSequence();
		CanSkipRoverMission = false;
		foreach (Key key in new[] { Key.A, Key.R, Key.E, Key.S }) PressKey(key, true);
		Check(RoverSkips == 0 && !mission.RoverStageCompleted,
			"ARES does not skip a mission when its target rejects the skip");
		CanSkipRoverMission = true;
		foreach (Key key in new[] { Key.A, Key.R, Key.E, Key.S }) PressKey(key, true);
		Check(RoverSkips == 1 && mission.RoverStageCompleted && mission.Ending == MissionEnding.None,
			"ARES uses the active scene's normal rover-success route");

		stations.QueueFree();
		stations.Player.QueueFree();
		await Frames();
		mission.ResetTrainingProgress();
	}
}

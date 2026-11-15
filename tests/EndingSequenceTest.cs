using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.ending;
using ares.scripts.mission;

public partial class EndingSequenceTest : Node
{
	private int _assertions;

	private void Check(bool condition, string message)
	{
		_assertions++;
		if (!condition) throw new InvalidOperationException(message);
	}

	private async Task Frames(int count = 3)
	{
		for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private static T Find<T>(Node root, Func<T, bool> match) where T : Node
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T typed && match(typed)) return typed;
			var nested = Find(child, match);
			if (nested != null) return nested;
		}
		return null;
	}

	private async Task Wait(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async Task Capture(string name)
	{
		if (!OS.GetCmdlineUserArgs().Contains("--visual")) return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/ending-{name}.png") == Error.Ok,
			$"Ending screenshot saved: {name}");
	}

	public override async void _Ready()
	{
		try
		{
			TestSequenceResources();
			await TestGoodEnding();
			await TestBadEnding();
			await TestLaunchFailureResults();
			await TestUnrecordedOutcome();
			GD.Print($"ENDING PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GetTree().Paused = false;
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void TestSequenceResources()
	{
		var good = GD.Load<CinematicSequence>("res://data/cinematics/ending_good.tres");
		var bad = GD.Load<CinematicSequence>("res://data/cinematics/ending_bad.tres");
		Check(good != null && good.Id == "good" && good.Beats.Count == 3,
			"Good ending has three ordered image/dialogue beats");
		Check(bad != null && bad.Id == "bad" && bad.Beats.Count == 4,
			"Bad ending has four ordered image/dialogue beats");
		foreach (var beat in good.Beats)
			Check(beat.Image != null && !string.IsNullOrWhiteSpace(beat.Text) && beat.HoldSeconds > 0,
				"Good ending beat has artwork, dialogue, and a readable hold");
		foreach (var beat in bad.Beats)
			Check(beat.Image != null && !string.IsNullOrWhiteSpace(beat.Text) && beat.HoldSeconds > 0,
				"Bad ending beat has artwork, dialogue, and a readable hold");
	}

	private async Task TestGoodEnding()
	{
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		Check(mission.TryCompleteMission(), "Mission completion records good outcome");
		string resultsScene = "";
		var ending = LoadEnding(value => value.SceneLoader = path => { resultsScene = path; return Error.Ok; });
		Check(ending.Outcome == MissionEnding.Good && ending.SequenceId == "good",
			"The ending player chooses the good cinematic from mission state");
		var dialogue = Find<ares.scripts.dialogue.DialogueView>(ending, view => view.Name == "Dialogue");
		Check(dialogue != null && Mathf.IsEqualApprox(dialogue.AnchorLeft, 0.08f) &&
			Mathf.IsEqualApprox(dialogue.AnchorRight, 0.92f) &&
			dialogue.CustomMinimumSize.Y == ares.scripts.dialogue.DialoguePanelStyle.StandardHeight,
			"Ending dialogue uses the same viewport-sized panel as training");
		if (OS.GetCmdlineUserArgs().Contains("--visual"))
		{
			await Wait(.45);
			await Capture("good-opening");
			await Wait(.5);
			await Capture("good-dialogue");
		}
		ending.SkipToResults();
		await Wait(1.3);
		await Capture("good-results");
		Check(ending.IsResultsVisible && ending.ResultsMessage.Contains("OVERALL") &&
			ending.ResultsMessage.Contains("RESOURCES"),
			"Skip reaches results with weighted score and resource breakdown");
		Check(resultsScene == "res://scenes/ending/EndingResults.tscn",
			"Ending opens the dedicated mission-results scene");
		Check(ending.GetNode<Label>("ResultsOverlay/Score").Text.Contains("MISSION COMPLETE"),
			"Good result is presented as mission complete");
		ending.QueueFree();
		await Frames();
	}

	private async Task TestBadEnding()
	{
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		Check(mission.TryFailMission() && mission.Ending == MissionEnding.Bad,
			"An explicit whole-game failure records the bad outcome");
		var ending = LoadEnding(value => value.SceneLoader = _ => Error.Ok);
		Check(ending.Outcome == MissionEnding.Bad && ending.SequenceId == "bad",
			"The ending player chooses the bad cinematic from mission state");
		ending.SkipToResults();
		await Wait(1.3);
		Check(ending.IsResultsVisible && ending.GetNode<Label>("ResultsOverlay/Score").Text.Contains("MISSION FAILED"),
			"Bad result is presented without fake scores");
		ending.QueueFree();
		await Frames();
	}

	private async Task TestUnrecordedOutcome()
	{
		MissionState.Instance.BeginLaunchSequence();
		var ending = LoadEnding(value => value.SceneLoader = _ => Error.Ok);
		Check(ending.CurrentPhase == "Unavailable" && !ending.IsResultsVisible,
			"Direct ending-scene entry cannot invent a mission outcome");
		ending.QueueFree();
		await Frames();
	}

	private async Task TestLaunchFailureResults()
	{
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		mission.TrainingCourseComplete = mission.TrainingWiresComplete =
			mission.TrainingFusesComplete = mission.TrainingFrequencyComplete =
			mission.TrainingHullRepairComplete = mission.TrainingLaunchPracticeComplete = true;
		Check(mission.FailLaunch(mission.LaunchSequence), "Failed launch records a scored bad outcome");
		var ending = LoadEnding(value => value.SceneLoader = _ => Error.Ok);
		Check(ending.IsResultsVisible && ending.CurrentPhase == "Results" &&
			ending.Outcome == MissionEnding.Bad && ending.ResultsMessage.Contains("ALL MISSIONS WON: NO"),
			"Failed launch enters the score screen directly and marks the campaign unwon");
		ending.QueueFree();
		await Frames();
	}

	private Ending LoadEnding(Action<Ending> configure = null)
	{
		var ending = GD.Load<PackedScene>("res://scenes/ending/ending.tscn").Instantiate<Ending>();
		configure?.Invoke(ending);
		AddChild(ending);
		return ending;
	}
}

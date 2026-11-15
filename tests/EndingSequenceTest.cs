using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.ending;
using ares.scripts.mission;
using ares.scripts.rover;

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
			await TestRoverSuccessHandoff();
			await TestMissingMarsSceneFallback();
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
		var ending = LoadEnding();
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
		Check(ending.IsResultsVisible && ending.ResultsMessage.Contains("SCORING COMING SOON"),
			"Skip reaches honest results without inventing score values");
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
		var ending = LoadEnding();
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
		var ending = LoadEnding();
		Check(ending.CurrentPhase == "Unavailable" && !ending.IsResultsVisible,
			"Direct ending-scene entry cannot invent a mission outcome");
		ending.QueueFree();
		await Frames();
	}

	private async Task TestRoverSuccessHandoff()
	{
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		Check(mission.CompleteRoverStage(mission.LaunchSequence) && mission.Ending == MissionEnding.None,
			"Rover recovery completes its stage without ending the whole mission");
		var completed = GD.Load<PackedScene>("res://scenes/rover/MissionCompleted.tscn")
			.Instantiate<MissionCompleted>();
		string loaded = "";
		completed.SceneLoader = path => { loaded = path; return Error.Ok; };
		AddChild(completed);
		await Wait(2.1);
		var continueLine = Find<Button>(completed, button => button.Name == "Continue");
		continueLine.EmitSignal(BaseButton.SignalName.Pressed);
		continueLine.EmitSignal(BaseButton.SignalName.Pressed);
		continueLine.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(1.6);
		var nextStage = Find<Button>(completed, button => button.Name == "NextStage");
		Check(nextStage.IsVisibleInTree(), "Rover recovery offers the next mission stage after its dialogue");
		nextStage.EmitSignal(BaseButton.SignalName.Pressed);
		Check(loaded == "res://scenes/mars/MarsBase.tscn" && mission.Ending == MissionEnding.None,
			"Rover recovery continues to Mars without ending the whole mission");
		Find<Button>(completed, button => button.Name == "MainMenu").EmitSignal(BaseButton.SignalName.Pressed);
		Check(loaded == "res://scenes/main_menu/main_menu_take3.tscn",
			"The rover success screen still offers return to Mission Control");
		completed.QueueFree();
		await Frames();
	}

	private async Task TestMissingMarsSceneFallback()
	{
		var mission = MissionState.Instance;
		mission.BeginLaunchSequence();
		mission.CompleteRoverStage(mission.LaunchSequence);
		var completed = GD.Load<PackedScene>("res://scenes/rover/MissionCompleted.tscn")
			.Instantiate<MissionCompleted>();
		completed.NextScenePath = "res://scenes/mars/not_pushed_yet.tscn";
		string loaded = "";
		completed.SceneLoader = path => { loaded = path; return Error.Ok; };
		AddChild(completed);
		await Wait(2.1);
		var advance = Find<Button>(completed, button => button.Name == "Continue");
		advance.EmitSignal(BaseButton.SignalName.Pressed);
		advance.EmitSignal(BaseButton.SignalName.Pressed);
		advance.EmitSignal(BaseButton.SignalName.Pressed);
		await Wait(1.6);
		var next = Find<Button>(completed, button => button.Name == "NextStage");
		next.EmitSignal(BaseButton.SignalName.Pressed);
		Check(loaded == "" && next.Disabled && mission.Ending == MissionEnding.None &&
			Find<Label>(completed, label => label.Name == "Text").Text.Contains("not available"),
			"An absent future Mars scene reports the gap without playing a final cinematic");
		completed.QueueFree();
		await Frames();
	}

	private Ending LoadEnding()
	{
		var ending = GD.Load<PackedScene>("res://scenes/ending/ending.tscn").Instantiate<Ending>();
		AddChild(ending);
		return ending;
	}
}

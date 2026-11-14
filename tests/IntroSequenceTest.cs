using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.stages;

public partial class IntroSequenceTest : Node
{
    private int _assertions;
    private int _loads;
    private bool Visual => OS.GetCmdlineUserArgs().Contains("--visual");

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new Exception(message);
    }

    public override async void _Ready()
    {
        try
        {
            GetTree().CurrentScene = null;
            await Frames(2);
            await Run();
            GD.Print($"INTRO PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PushError(ex.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Run()
    {
        var transmission = GD.Load<DialogueSequence>("res://data/dialogue/intro_transmission.tres");
        Check(transmission != null, "Intro transmission loads");
        Check(transmission.Lines.Count == 6, "Intro transmission has six lines");
        Check(transmission.Lines[0].Speaker == "MISSION CONTROL", "Intro opens with Mission Control");
        Check(transmission.Lines[5].Speaker == "YOU" && transmission.Lines[5].Text.Contains("why I'm going"),
            "Intro closes on the player");

        var briefing = GD.Load<DialogueSequence>("res://data/dialogue/training_intro.tres");
        Check(briefing != null && briefing.Lines.Count == 3, "Training briefing has three lines");
        Check(briefing.Lines[0].Text.Contains("gym") && briefing.Lines[0].Text.Contains("repair stations") &&
            !briefing.Lines[0].Text.Contains("WASD") && !briefing.Lines[0].Text.Contains("SPACE"),
            "Training briefing introduces the spaces without dumping control instructions");
        Check(briefing.Lines[2].Text.Contains("Still silent"), "Training briefing keeps the probes");

        var completion = GD.Load<DialogueSequence>("res://data/dialogue/training_completion.tres");
        Check(completion != null && completion.Lines.Count == 1, "Training completion has one line");
        Check(completion.Lines[0].Text.Contains("All training is complete"), "Completion keeps the test substring");

        var sequence = await OpenIntro();
        Check(sequence.GetNodeOrNull<DialogueView>("Dialogue") != null, "Intro exposes its dialogue view");
        Check(sequence.GetNodeOrNull<Control>("Objectives") != null, "Intro exposes objectives");
        Check(sequence.GetNodeOrNull<Control>("TitleCard") != null, "Intro exposes the title card");
        Check(sequence.BackdropCount == 4, "Intro loads four cinematic backdrops");
        Check(sequence.GetNodeOrNull<TextureRect>("BackdropA") != null &&
            sequence.GetNodeOrNull<TextureRect>("BackdropB") != null, "Intro exposes crossfade backdrop layers");
        if (Visual) await CaptureStages(sequence);
        sequence.Advance(100);
        Check(sequence.Finished && !sequence.Skipped, "Timeline finishes");
        Check(sequence.BackdropIndex == 3, "Timeline reaches the departure backdrop");
        Check(_loads == 1, "Timeline transitions once");
        Check(sequence.LoadedScene == "res://scenes/training/Training.tscn", "Timeline loads training");
        Check(MissionState.Instance.Stage == MissionStage.Training, "Stage advances to training");
        sequence.QueueFree();
        await Frames();

        var skipped = await OpenIntro();
        Check(MissionState.Instance.Stage == MissionStage.Intro, "Intro sets the intro stage");
        skipped.Skip();
        skipped.Skip();
        Check(skipped.Finished && skipped.Skipped, "Skip finishes the intro");
        Check(_loads == 1, "Skip transitions once");
        Check(skipped.LoadedScene == "res://scenes/training/Training.tscn", "Skip loads training");
        Check(MissionState.Instance.Stage == MissionStage.Training, "Skip advances the stage");
        skipped.QueueFree();
        await Frames();

        var failed = await OpenIntro();
        failed.SceneLoader = _ => Error.CantOpen;
        failed.Skip();
        Check(!failed.Finished && MissionState.Instance.Stage == MissionStage.Intro,
            "Failed intro transition preserves its state for recovery");
        failed.SceneLoader = _ => Error.Ok;
        failed.Skip();
        Check(failed.Finished && MissionState.Instance.Stage == MissionStage.Training,
            "Failed intro transition can be retried successfully");
        failed.QueueFree();
        await Frames();

        await MenuRouting();
    }

    private async Task<IntroSequence> OpenIntro()
    {
        _loads = 0;
        var sequence = GD.Load<PackedScene>("res://scenes/intro/Intro.tscn").Instantiate<IntroSequence>();
        sequence.SetProcess(false);
        sequence.SceneLoader = _ =>
        {
            _loads++;
            return Error.Ok;
        };
        AddChild(sequence);
        await Frames();
        return sequence;
    }

    private async Task MenuRouting()
    {
        MissionState.Instance.Stage = MissionStage.MainMenu;
        var menu = GD.Load<PackedScene>("res://scenes/main_menu/main_menu_take3.tscn").Instantiate<Control>();
        GetTree().Root.AddChild(menu);
        GetTree().CurrentScene = menu;
        await Frames();
        if (Visual) await Capture("intro-00-menu");
        menu.Call("OnStartPressed");
        for (int i = 0; i < 20 && GetTree().CurrentScene is not IntroSequence; i++)
            await Frames();
        Check(GetTree().CurrentScene is IntroSequence, "Start routes to the intro");
        Check(MissionState.Instance.Stage == MissionStage.Intro, "Start sets the intro stage");
        if (GetTree().CurrentScene is IntroSequence intro)
        {
            intro.SetProcess(false);
            GetTree().CurrentScene = null;
            intro.QueueFree();
        }
        Ares.Systems.Audio.AudioManager.StopMusic(0.0);
        await Frames(2);
    }

    private async Task CaptureStages(IntroSequence sequence)
    {
        await AdvanceTo(sequence, 4.0);
        await Capture("intro-01-telemetry");
        await AdvanceTo(sequence, 6.2);
        await Capture("intro-02-probes");
        await AdvanceTo(sequence, 10.0);
        await Capture("intro-03-dialogue");
        await AdvanceTo(sequence, 16.0, 0.7);
        await Capture("intro-04-objectives");
        await AdvanceTo(sequence, 21.0, 1.6);
        await Capture("intro-05-title");
    }

    private async Task AdvanceTo(IntroSequence sequence, double target, double settle = 0.35)
    {
        while (sequence.Time < target && !sequence.Finished)
        {
            int previousBackdrop = sequence.BackdropIndex;
            sequence.Advance(0.1);
            if (sequence.BackdropIndex != previousBackdrop)
                await ToSignal(GetTree().CreateTimer(0.85), SceneTreeTimer.SignalName.Timeout);
        }
        await ToSignal(GetTree().CreateTimer(settle), SceneTreeTimer.SignalName.Timeout);
        await Frames(2);
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng($"/tmp/opencode/{name}.png") == Error.Ok,
            "Screenshot " + name);
    }
}

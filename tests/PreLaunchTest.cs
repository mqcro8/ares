using Godot;
using System;
using System.Threading.Tasks;
using ares.scripts.prelaunch;

public partial class PreLaunchTest : Node
{
    private int _assertions;
    private void Check(bool ok, string text) { _assertions++; if (!ok) throw new InvalidOperationException(text); }

    public override async void _Ready()
    {
        try
        {
            var scene = GD.Load<PackedScene>("res://scenes/pre-launch/pre-launch.tscn").Instantiate<PreLaunch>();
            AddChild(scene);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(scene.State == PreLaunch.FlightState.Orbiting, "Starts orbiting");
            Check(scene.Charge == 0f, "Starts with zero charge");
            Check(scene.GetNode<Label>("HUD/Message").Text == "", "Starts without result message");
            scene.GetNode<Label>("HUD/Message").Text = "test";
            scene.GetNode<Label>("HUD/Message").Text = "";
            Check(scene.GetNode<Sprite2D>("Rocket").Position.DistanceTo(scene.EarthPosition) > scene.EarthRadius,
                "Rocket starts outside Earth core");
            string destination = "";
            scene.SceneLoader = path => { destination = path; return Error.Ok; };
            Check(scene.SkipWithDevCode() && destination == ares.scripts.gateway.Gateway.ScenePath,
                "ARES skips pre-launch directly to Gateway");
            scene.QueueFree();
            GD.Print($"PRE-LAUNCH PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}

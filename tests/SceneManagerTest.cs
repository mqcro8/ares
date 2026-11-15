using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneManagerTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var manager = SceneManager.Instance ?? throw new InvalidOperationException("SceneManager autoload is missing");
			var originalScene = GetTree().CurrentScene;
			manager.Enter("res://scenes/missing-scene-for-test.tscn", Vector2.Zero);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(GetTree().CurrentScene == originalScene,
				"A failed scene load leaves the active scene unchanged");

			manager.Exit();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(GetTree().CurrentScene == originalScene,
				"A failed scene load does not leave a stale return-stack entry");

			GD.Print("SCENE MANAGER PASS (2 assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private static void Check(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}
}

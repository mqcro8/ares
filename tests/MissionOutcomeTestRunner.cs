using Godot;
using System;

public partial class MissionOutcomeTestRunner : Node
{
	private int _assertions;

	public override void _Ready()
	{
		try
		{
			MissionOutcomeTest.Run(Check);
			GD.Print($"MISSION OUTCOME PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void Check(bool condition, string message)
	{
		_assertions++;
		if (!condition) throw new InvalidOperationException(message);
	}
}

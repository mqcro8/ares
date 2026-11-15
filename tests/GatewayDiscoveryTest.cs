using Godot;
using System;
using System.Threading.Tasks;
using ares.scripts.mars;
using ares.scripts.mission;

public partial class GatewayDiscoveryTest : Node
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

	public override async void _Ready()
	{
		try
		{
			var mission = MissionState.Instance;
			mission.BeginLaunchSequence();
			var first = new GatewayDiscovery();
			AddChild(first);
			await Frames();
			mission.MarkSignalTableRepaired();
			Check(first.IsOpen && mission.GatewayDiscoveryPending && !mission.GatewayDiscoveryAcknowledged,
				"Restoring Gateway communications opens the archive reveal once");
			var image = first.GetNode<TextureRect>("DiscoveryOverlay/Image");
			Check(image.Texture?.ResourcePath.EndsWith("Mars Gateway Circuit Puzzle-2.png") == true,
				"The first reveal beat shows the corroded-circuit evidence");
			first.QueueFree();
			await Frames();
			Check(mission.GatewayDiscoveryPending && !mission.GatewayDiscoveryAcknowledged,
				"Leaving before acknowledgement preserves the discovery for re-entry");

			var resumed = new GatewayDiscovery();
			AddChild(resumed);
			await Frames();
			Check(resumed.IsOpen && resumed.GetNode<TextureRect>("DiscoveryOverlay/Image").Texture?
				.ResourcePath.EndsWith("Mars Gateway Circuit Puzzle-2.png") == true,
				"Re-entry resumes the unacknowledged archive reveal from its beginning");
			Check(resumed.SkipWithDevCode() && !mission.GatewayDiscoveryPending &&
				mission.GatewayDiscoveryAcknowledged && !resumed.IsOpen,
				"Skipping the reveal records acknowledgement without undoing repairs");
			resumed.QueueFree();
			await Frames();

			mission.BeginLaunchSequence();
			mission.MarkSignalTableRepaired();
			var duringReveal = new GatewayDiscovery();
			AddChild(duringReveal);
			await Frames();
			Check(duringReveal.IsOpen && mission.GatewayDiscoveryPending,
				"A live mission opens its pending discovery reveal");
			mission.FailMissionImmediately();
			Check(!duringReveal.IsOpen && !mission.GatewayDiscoveryPending &&
				!mission.AcknowledgeGatewayDiscovery(),
				"Mission death aborts an active reveal and permanently blocks acknowledgement");
			duringReveal.QueueFree();
			await Frames();
			GD.Print($"GATEWAY DISCOVERY PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}
}

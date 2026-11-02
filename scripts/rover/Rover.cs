using System.Collections.Generic;
using Godot;

// Top-down rover with delayed controls: every command takes LagSeconds to reach the wheels.
// Movement and impact detection only. Counting impacts and changing LagSeconds is the stage's job.
[GlobalClass]
public partial class Rover : CharacterBody2D
{
	public override void _Ready()
	{
		GD.Print("Node is ready!");
	}
}

using Godot;
using System;

public partial class Ending : Node2D
{
	// Called when the node enters the scene tree for the first time.
	private int ending_score = 0;
	private Sprite2D _background;
	public override void _Ready()
	{
		_background = GetNodeOrNull<Sprite2D>("Sprite2D");
		if(_background == null)
		{
			GD.PrintErr("error");
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	private void Pass()
	{
		Texture _texture = GD.Load<Texture>("res://art/cinematics/ending_pass_1.png");
		// _background
	}

	private void Fail()
	{
		
	}

	public override void _Process(double delta)
	{
	}
}

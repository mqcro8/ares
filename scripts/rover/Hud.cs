using Godot;
using System;

public partial class Hud : CanvasLayer
{
	private HBoxContainer _heartsContainer;
	private TextureRect lagometer;
	private Texture2D _emptyHeart;
	public override void _Ready()
	{
		_heartsContainer = GetNodeOrNull<HBoxContainer>("HBoxContainer");
		lagometer = GetNodeOrNull<TextureRect>("lagometer");

		if (_heartsContainer == null || lagometer == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		_emptyHeart = GD.Load<Texture2D>("res://art/rover/heart_lost.png");
	}

	public void UpdateCollisions(int collisions)
	{
		lagometer.Texture = (Texture2D)GD.Load($"res://art/rover/lag_{collisions}.png");
		_heartsContainer.GetChild<TextureRect>(5 - collisions).Texture = _emptyHeart;
	}
}

using Godot;
using System;
using ares.scripts.rover;
using ares.scripts.systems.items;

public partial class Hud : CanvasLayer
{
	private HBoxContainer _heartsContainer;
	private TextureRect lagometer;
	private Texture2D _fullHeart, _emptyHeart;
	public int HeartCount => _heartsContainer?.GetChildCount() ?? 0;
	public override void _Ready()
	{
		_heartsContainer = GetNodeOrNull<HBoxContainer>("HBoxContainer");
		lagometer = GetNodeOrNull<TextureRect>("lagometer");

		if (_heartsContainer == null || lagometer == null)
		{
			GD.PushError("RoverScene: missing elements");
			return;
		}

		_fullHeart = _heartsContainer.GetChild<TextureRect>(0).Texture;
		_emptyHeart = GD.Load<Texture2D>("res://art/rover/heart_lost.png");
	}

	public void ConfigureMaxImpactPoints(int desiredHearts)
	{
		if (_heartsContainer == null) return;
		desiredHearts = Mathf.Max(1, desiredHearts);
		while (_heartsContainer.GetChildCount() < desiredHearts)
		{
			var heart = (TextureRect)_heartsContainer.GetChild<TextureRect>(_heartsContainer.GetChildCount() - 1).Duplicate();
			heart.Name = $"heart{_heartsContainer.GetChildCount() + 1}";
			_heartsContainer.AddChild(heart);
		}
		while (_heartsContainer.GetChildCount() > desiredHearts)
		{
			var heart = _heartsContainer.GetChild(_heartsContainer.GetChildCount() - 1);
			_heartsContainer.RemoveChild(heart);
			heart.QueueFree();
		}
	}

	public void UpdateCollisions(int damagePoints)
	{
		int damage = Mathf.Clamp(damagePoints, 0, HeartCount);
		if (damage > 0)
			lagometer.Texture = GD.Load<Texture2D>($"res://art/rover/lag_{Mathf.Min(damage, ToolEffects.RoverBaseDamageLimit)}.png");
		for (int i = 0; i < HeartCount; i++)
			_heartsContainer.GetChild<TextureRect>(i).Texture = i < HeartCount - damage ? _fullHeart : _emptyHeart;
	}
}

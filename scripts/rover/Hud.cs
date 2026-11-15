using Godot;
using System;
using ares.scripts.rover;
using ares.scripts.systems.items;

public partial class Hud : CanvasLayer
{
	private HBoxContainer _heartsContainer;
	private TextureRect lagometer;
	private Texture2D _fullHeart, _emptyHeart;
	private Label _status;
	private Label _speed;
	private Label _servoKey;
	private Label _servoHint;
	private float _hintTimer;
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
		var indicators = new HBoxContainer
		{
			Position = new Vector2(16, 78),
		};
		_status = MakeHudLabel("STANDARD");
		_speed = MakeHudLabel("200");
		_servoKey = MakeHudLabel("W");
		_servoHint = MakeHudLabel("");
		_servoHint.Position = new Vector2(16, 112);
		_servoHint.AddThemeColorOverride("font_color", new Color("ffb24a"));
		var servoIcon = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://art/training/props/tool_rover_servos_placeholder.png"),
			CustomMinimumSize = new Vector2(24, 24),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		indicators.AddChild(_status);
		indicators.AddChild(_speed);
		indicators.AddChild(servoIcon);
		indicators.AddChild(_servoKey);
		AddChild(indicators);
		AddChild(_servoHint);
	}

	private static Label MakeHudLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.92f, 1f));
		label.AddThemeFontSizeOverride("font_size", 13);
		return label;
	}

	public void UpdateStatus(bool accelerationAvailable, float controlLag, string route = "STANDARD", float speed = 200f)
	{
		if (_status == null) return;
		_status.Text = route switch
		{
			"SAFE DETOUR" => "SAFE",
			"RISKY SHORTCUT" => "RISK",
			_ => "ROUTE",
		};
		_speed.Text = $"{speed:0} px/s";
		_servoKey.Modulate = accelerationAvailable ? Colors.White : new Color(0.48f, 0.52f, 0.56f, 0.65f);
		_servoKey.TooltipText = $"Control lag: {controlLag:0.0}s";
	}

	public void ShowServoHint()
	{
		if (_servoHint == null) return;
		_servoHint.Text = "SERVOS REQUIRED";
		_hintTimer = 1.8f;
	}

	public override void _Process(double delta)
	{
		if (_hintTimer <= 0f) return;
		_hintTimer -= (float)delta;
		if (_hintTimer <= 0f) _servoHint.Text = "";
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

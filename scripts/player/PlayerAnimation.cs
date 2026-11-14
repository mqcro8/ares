namespace ares.scripts.player;

using Godot;

public partial class PlayerAnimation : AnimatedSprite2D
{
	[Export] public float MoveThreshold { get; set; } = 5f;
	
	[Export] public bool ScaleSpeedWithVelocity { get; set; } = true;

	[Export] public float ReferenceSpeed { get; set; } = 200f;

	private CharacterBody2D _body;
	private SpriteFrames _frames;
	private string _facing = "down";
	public string Facing => _facing;
	public void RestoreFacing(string facing, bool flip)
	{
		_facing = facing; FlipH = flip; Play($"idle_{facing}");
	}

	public override void _Ready()
	{
		_body = GetParent<CharacterBody2D>();
		_frames = SpriteFrames;
		if (_body == null)
		{
			GD.PushError($"{nameof(PlayerAnimation)} must be a child of a CharacterBody2D.");
			SetProcess(false);
			return;
		}

		Play("idle_down");
	}

	public override void _Process(double delta)
	{
		if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || !GodotObject.IsInstanceValid(_body)) return;
		Vector2 velocity = _body.Velocity;
		bool moving = velocity.Length() > MoveThreshold;

		if (moving)
		{
			UpdateFacing(velocity);
		}

		string state = moving ? "walk" : "idle";
		string animation = $"{state}_{_facing}";

		if (_frames != null && GodotObject.IsInstanceValid(_frames) && _frames.HasAnimation(animation) && Animation != animation)
		{
			Play(animation);
		}

		SpeedScale = (moving && ScaleSpeedWithVelocity)
			? Mathf.Clamp(velocity.Length() / ReferenceSpeed, 0.4f, 1.5f)
			: 1f;
	}

	public override void _ExitTree()
	{
		SetProcess(false);
		_body = null;
		_frames = null;
	}

	private void UpdateFacing(Vector2 velocity)
	{
		if (Mathf.Abs(velocity.X) > 0.01f)
		{
			if (Mathf.Abs(velocity.X) >= Mathf.Abs(velocity.Y))
			{
				FlipH = velocity.X < 0;
			}
		}
		
		if (Mathf.Abs(velocity.X) >= Mathf.Abs(velocity.Y))
			_facing = "side";
		else
			_facing = velocity.Y > 0 ? "down" : "up";
	}
}

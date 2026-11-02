namespace ares.scripts.training;

using Godot;

/// <summary>Scene-local vertical tracking, independent of briefing and gameplay state.</summary>
public partial class TrainingCamera : Camera2D
{
	[Export] public NodePath TargetPath { get; set; }
	[Export] public float MinimumY { get; set; } = 300.0f;
	[Export] public float MaximumY { get; set; } = 780.0f;
	[Export] public float VerticalOffset { get; set; } = 20.0f;

	private Node2D _target;

	public override void _Ready()
	{
		_target = GetNodeOrNull<Node2D>(TargetPath);
		if (_target == null)
		{
			SetPhysicsProcess(false);
			return;
		}
		UpdatePosition();
		ResetSmoothing();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!GodotObject.IsInstanceValid(_target))
		{
			SetPhysicsProcess(false);
			return;
		}
		UpdatePosition();
	}

	private void UpdatePosition()
	{
		GlobalPosition = new Vector2(GlobalPosition.X,
			Mathf.Clamp(_target.GlobalPosition.Y + VerticalOffset, MinimumY, MaximumY));
	}
}

using Godot;

// Attach to an Area2D with a CollisionShape2D child.
// Use one with IsExit = false at a building's door, and one with IsExit = true inside.
public partial class SceneTrigger : Area2D
{
	// Scene to load when entering (ignored when IsExit is true).
	[Export(PropertyHint.File, "*.tscn")]
	public string TargetScene { get; set; }

	// If true, this trigger returns the player to where they entered from.
	[Export]
	public bool IsExit { get; set; } = false;

	// Where the player reappears relative to this door when they come back.
	// Keep it outside the trigger so they don't re-enter immediately.
	[Export]
	public Vector2 ReturnOffset { get; set; } = new Vector2(0, 32);

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (IsExit)
		{
			Callable.From(() => SceneManager.Instance.Exit()).CallDeferred();
			return;
		}

		if (string.IsNullOrEmpty(TargetScene))
		{
			GD.PushWarning("SceneTrigger: TargetScene is not set.");
			return;
		}

		Vector2 returnPosition = GlobalPosition + ReturnOffset;
		Callable.From(() => SceneManager.Instance.Enter(TargetScene, returnPosition)).CallDeferred();
	}
}

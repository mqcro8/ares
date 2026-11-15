using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using Ares.Systems.Audio;

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

	[Export] public bool RequirePower { get; set; }
	[Export] public bool RequireAccessPanels { get; set; }
	[Export] public DialogueSequence BlockedDialogue { get; set; }

	public override void _Ready()
	{
		if (BlockedDialogue == null)
		{
			if (RequireAccessPanels)
				BlockedDialogue = GD.Load<DialogueSequence>("res://data/dialogue/gate_doors_locked.tres");
			else if (RequirePower)
				BlockedDialogue = GD.Load<DialogueSequence>("res://data/dialogue/gate_power_locked.tres");
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (IsExit)
		{
			AudioManager.PlaySfx(SoundIds.OpenDoor);
			Callable.From(() => SceneManager.Instance.Exit()).CallDeferred();
			return;
		}

		if (!RequirementMet())
		{
			ShowBlocked();
			return;
		}

		if (string.IsNullOrEmpty(TargetScene))
		{
			GD.PushWarning("SceneTrigger: TargetScene is not set.");
			return;
		}

		AudioManager.PlaySfx(SoundIds.OpenDoor);
		Vector2 returnPosition = GlobalPosition + ReturnOffset;
		Callable.From(() => SceneManager.Instance.Enter(TargetScene, returnPosition)).CallDeferred();
	}

	private bool RequirementMet()
	{
		MissionState state = MissionState.Instance;
		if (state == null)
			return true;

		if (RequirePower && (!state.GeneratorRepaired || !state.WireBoxRepaired))
			return false;
		if (RequireAccessPanels && !state.AllDoorAccessOn)
			return false;
		return true;
	}

	private void ShowBlocked()
	{
		var manager = GetTree().GetFirstNodeInGroup("objective_dialogue") as ares.scripts.mars.ObjectiveDialogue;
		if (manager == null || BlockedDialogue == null)
		{
			GD.PushWarning("SceneTrigger: access blocked, but no objective dialogue is available.");
			return;
		}

		manager.ShowBlocked(BlockedDialogue);
	}
}

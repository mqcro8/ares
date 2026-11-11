namespace ares.scripts.player;

using Godot;
using systems.inventory;
using systems.items;
using ares.scripts.mission;

public partial class Player : CharacterBody2D
{
	// Opt-in scene-local controller. Still dispatched only by this body's physics tick,
	// so TrainingIntro's existing physics lock also blocks every training input.
	[Export] public ares.scripts.training.TrainingMovement TrainingMovement { get; set; }
	[Export] public float Speed { get; set; } = 200f;

	[Export] public float Acceleration { get; set; } = 1600f;

	[Export] public float Friction { get; set; } = 1400f;

	[Export] public Hotbar Hotbar { get; set; }

	// NOTE TO OTHERS: Use this to set default items on your particular scenes
	[Export] public Godot.Collections.Array<systems.items.ItemData> StartingItems { get; set; } = new();

	public Inventory Inventory { get; private set; }

	public override void _Ready()
	{
		int size = Hotbar?.SlotCount ?? Inventory.DefaultSlots;
		var mission = MissionState.Instance;
		bool created;
		if (mission != null)
		{
			Inventory = mission.GetOrCreatePlayerInventory(size, out created);
		}
		else
		{
			Inventory = new Inventory { Size = size };
			created = true;
		}

		if (created)
		{
			foreach (var item in StartingItems)
				Inventory.AddItem(item);
		}

		Hotbar?.Bind(Inventory);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (TrainingMovement != null && TrainingMovement.Tick(this, (float)delta))
			return;
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		float dt = (float)delta;

		if (direction != Vector2.Zero)
		{
			Velocity = Velocity.MoveToward(direction * Speed, Acceleration * dt);
		}
		else
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * dt);
		}

		MoveAndSlide();
	}

	/// Currently selected stack
	public ItemStack SelectedStack => Hotbar?.SelectedStack;
}

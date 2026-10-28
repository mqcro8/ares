namespace ares.scripts.systems.inventory;

using Godot;
using System.Collections.Generic;
using items;

public partial class Hotbar : HBoxContainer
{
	[Signal] public delegate void SelectionChangedEventHandler(int index);

	[Export] public PackedScene SlotScene { get; set; }
	[Export(PropertyHint.Range, "1,9,1")] public int SlotCount { get; set; } = 5;

	public int SelectedIndex { get; private set; }
	public Inventory Inventory { get; private set; }
	public ItemStack SelectedStack => Inventory?.GetStack(SelectedIndex);

	private readonly List<HotbarSlot> _slots = new();

	public void Bind(Inventory inventory)
	{
		if (Inventory != null)
			Inventory.InventoryChanged -= Refresh;

		foreach (var slot in _slots)
		{
			RemoveChild(slot);
			slot.QueueFree();
		}
		_slots.Clear();

		Inventory = inventory;
		if (Inventory == null) return;

		for (int i = 0; i < Inventory.SlotCount; i++)
		{
			var slot = SlotScene.Instantiate<HotbarSlot>();
			AddChild(slot);
			_slots.Add(slot);
		}

		Inventory.InventoryChanged += Refresh;
		SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Inventory.SlotCount - 1);
		Refresh();
	}

	public override void _ExitTree()
	{
		// avoid dangling when inv dies (can die before this node)
		if (Inventory != null)
			Inventory.InventoryChanged -= Refresh;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Inventory == null) return;

		if (Pressed(@event, "hotbar_next")) { Cycle(1); return; }
		if (Pressed(@event, "hotbar_prev")) { Cycle(-1); return; }

		for (int i = 0; i < Inventory.SlotCount; i++)
		{
			if (Pressed(@event, $"hotbar_{i + 1}"))
			{
				Select(i);
				return;
			}
		}
	}

	public void Select(int index)
	{
		if (Inventory == null || !Inventory.IsValidSlot(index) || index == SelectedIndex) return;

		SelectedIndex = index;
		Refresh();
		EmitSignal(SignalName.SelectionChanged, SelectedIndex);
	}

	public void Cycle(int direction) =>
		Select(Mathf.PosMod(SelectedIndex + direction, Inventory.SlotCount));

	private void Refresh()
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			_slots[i].SetStack(Inventory.GetStack(i));
			_slots[i].SetSelected(i == SelectedIndex);
		}
	}

	private static bool Pressed(InputEvent e, string action) =>
		InputMap.HasAction(action) && e.IsActionPressed(action);
}
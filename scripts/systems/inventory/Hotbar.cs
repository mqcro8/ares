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

		Inventory.InventoryChanged += Refresh;
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

	private void Refresh()
	{
		if (Inventory == null) return;
		while (_slots.Count > Inventory.SlotCount)
		{
			var slot = _slots[^1];
			_slots.RemoveAt(_slots.Count - 1);
			RemoveChild(slot);
			slot.QueueFree();
		}
		while (_slots.Count < Inventory.SlotCount)
		{
			var slot = SlotScene.Instantiate<HotbarSlot>();
			AddChild(slot);
			_slots.Add(slot);
		}
		int previous = SelectedIndex;
		SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Inventory.SlotCount - 1);
		for (int i = 0; i < _slots.Count; i++)
		{
			_slots[i].SetStack(Inventory.GetStack(i));
			_slots[i].SetSelected(i == SelectedIndex);
		}
		if (previous != SelectedIndex) EmitSignal(SignalName.SelectionChanged, SelectedIndex);
	}

	private static bool Pressed(InputEvent e, string action) =>
		InputMap.HasAction(action) && e.IsActionPressed(action);
}

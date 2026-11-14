namespace ares.scripts.systems.inventory;

using Godot;
using System;
using items;

[GlobalClass]
public partial class Inventory : Resource
{
	[Signal] public delegate void InventoryChangedEventHandler();

	public const int DefaultSlots = 5;

	private ItemStack[] _slots = new ItemStack[DefaultSlots];

	public int SlotCount => _slots.Length;
	
	[Export(PropertyHint.Range, "1,20")]
	public int Size
	{
		get => _slots.Length;
		set
		{
			TryResize(value);
		}
	}

	public bool TryResize(int size)
	{
		if (size < 1) return false;
		if (size == _slots.Length) return true;
		for (int i = size; i < _slots.Length; i++)
			if (_slots[i] != null && _slots[i].Count > 0) return false;
		Array.Resize(ref _slots, size);
		EmitSignal(SignalName.InventoryChanged);
		return true;
	}

	public ItemStack GetStack(int slot) => IsValidSlot(slot) ? _slots[slot] : null;

	public bool IsValidSlot(int slot) => slot >= 0 && slot < _slots.Length;

	public int AddItem(ares.scripts.systems.items.ItemData item, int amount = 1)
	{
		if (item == null || amount <= 0) return amount;

		int remaining = amount;

		foreach (var stack in _slots)
		{
			if (remaining == 0) break;
			if (stack != null && stack.CanStackWith(item) && !stack.IsFull)
				remaining = stack.Add(remaining);
		}

		for (int i = 0; i < _slots.Length && remaining > 0; i++)
		{
			if (_slots[i] != null) continue;
			int toPlace = Math.Min(remaining, item.MaxStackSize);
			_slots[i] = new ItemStack(item, toPlace);
			remaining -= toPlace;
		}

		if (remaining != amount) EmitSignal(SignalName.InventoryChanged);
		return remaining;
	}

	public int RemoveItem(ares.scripts.systems.items.ItemData item, int amount = 1)
	{
		if (item == null || amount <= 0) return 0;

		int toRemove = amount;
		for (int i = _slots.Length - 1; i >= 0 && toRemove > 0; i--)
		{
			var stack = _slots[i];
			if (stack == null || !stack.CanStackWith(item)) continue;

			toRemove -= stack.Remove(toRemove);
			if (stack.Count == 0) _slots[i] = null;
		}

		int removed = amount - toRemove;
		if (removed > 0) EmitSignal(SignalName.InventoryChanged);
		return removed;
	}

	public int RemoveFromSlot(int slot, int amount = 1)
	{
		if (!IsValidSlot(slot) || _slots[slot] == null || amount <= 0) return 0;

		int removed = _slots[slot].Remove(amount);
		if (_slots[slot].Count == 0) _slots[slot] = null;

		if (removed > 0) EmitSignal(SignalName.InventoryChanged);
		return removed;
	}

	public int CountOf(ares.scripts.systems.items.ItemData item)
	{
		int total = 0;
		foreach (var stack in _slots)
			if (stack != null && stack.CanStackWith(item)) total += stack.Count;
		return total;
	}

	public bool Contains(ares.scripts.systems.items.ItemData item, int amount = 1) => CountOf(item) >= amount;

	public int CargoUsed
	{
		get
		{
			int total = 0;
			foreach (var stack in _slots)
				if (stack != null) total += stack.Count * stack.Item.CargoCost;
			return total;
		}
	}

	public bool HasCapability(ItemCapability capability)
	{
		foreach (var stack in _slots)
			if (stack != null && stack.Item.HasCapability(capability)) return true;
		return false;
	}

	public bool CanFit(ares.scripts.systems.items.ItemData item, int amount = 1)
	{
		if (item == null || amount <= 0) return false;

		int space = 0;
		foreach (var stack in _slots)
		{
			if (stack == null) space += item.MaxStackSize;
			else if (stack.CanStackWith(item)) space += stack.FreeSpace;
			if (space >= amount) return true;
		}
		return false;
	}

	public void DebugPrint()
	{
		for (int i = 0; i < _slots.Length; i++)
		{
			var s = _slots[i];
			GD.Print($"[{i}] " + (s == null ? "empty" : $"{s.Item.DisplayName} x{s.Count}"));
		}
	}
}

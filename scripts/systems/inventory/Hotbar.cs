using Godot;
using System.Collections.Generic;

public partial class Hotbar : HBoxContainer
{
    [Export]
    public PackedScene SlotScene { get; set; }

    private Inventory _inventory;
    private readonly List<HotbarSlot> _slots = new();

    private int _selectedSlot = 0;

    public int SelectedSlot => _selectedSlot;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);

        for (int i = 0; i < Inventory.DefaultSlots; i++)
        {
            HotbarSlot slot = SlotScene.Instantiate<HotbarSlot>();

            slot.SlotIndex = i;
            AddChild(slot);
            _slots.Add(slot);
        }

        UpdateSelection();
        UpdateHotbar();
    }

    public void SetInventory(Inventory inventory)
    {
        if (_inventory != null)
            _inventory.InventoryChanged -= UpdateHotbar;

        _inventory = inventory;

        if (_inventory != null)
            _inventory.InventoryChanged += UpdateHotbar;

        UpdateHotbar();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key &&
            key.Pressed &&
            !key.Echo)
        {
            if (key.Keycode >= Key.Key1 &&
                key.Keycode <= Key.Key6)
            {
                int index = (int)key.Keycode - (int)Key.Key1;
                SelectSlot(index);
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventMouseButton mouse &&
            mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp)
            {
                SelectSlot(
                    (_selectedSlot - 1 + _slots.Count) % _slots.Count);
                GetViewport().SetInputAsHandled();
            }
            else if (mouse.ButtonIndex == MouseButton.WheelDown)
            {
                SelectSlot(
                    (_selectedSlot + 1) % _slots.Count);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        _selectedSlot = index;
        UpdateSelection();
    }

    public ItemStack GetSelectedStack()
    {
        return _inventory?.GetStack(_selectedSlot);
    }

    private void UpdateHotbar()
    {
        if (_inventory == null)
        {
            foreach (HotbarSlot slot in _slots)
                slot.UpdateSlot(null);

            return;
        }

        for (int i = 0; i < _slots.Count; i++)
            _slots[i].UpdateSlot(_inventory.GetStack(i));
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SetSelected(i == _selectedSlot);
    }

    public override void _ExitTree()
    {
        if (_inventory != null)
            _inventory.InventoryChanged -= UpdateHotbar;
    }
}

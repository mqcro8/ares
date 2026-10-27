namespace ares.scripts.ui;

using Godot;
using ares.scripts.player;
using ares.scripts.systems.items;

public partial class ItemButton : Button
{
    [Export] public Item Item
    {
        get => _item;
        set
        {
            _item = value;
            if (_item != null && _item.Texture != null)
                Icon = _item.Texture;
        }
    }

    private Item _item;

    public HandEquip HandEquip { get; set; }

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
            Pressed += OnPressed;
    }

    private void OnPressed()
    {
        if (!Engine.IsEditorHint() && Item is EquipableItem && HandEquip != null)
            HandEquip.EquippedItem = Item as EquipableItem;
    }
}

namespace ares.scripts.systems.inventory;

using Godot;
using items;

public partial class HotbarSlot : Control
{
    private TextureRect _icon;
    private TextureRect _selected;
    private Label _count;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _selected = GetNode<TextureRect>("Selected");
        _count = GetNode<Label>("Count");
    }

    public void SetStack(ItemStack stack)
    {
        _icon.Texture = stack?.Item.Icon;
        _icon.Visible = stack != null;

        bool showCount = stack != null && stack.Count > 1;
        _count.Visible = showCount;
        if (showCount) _count.Text = stack.Count.ToString();
    }

    public void SetSelected(bool selected) => _selected.Visible = selected;
}
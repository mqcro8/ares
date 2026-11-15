namespace ares.scripts.systems.inventory;

using Godot;
using items;

public partial class HotbarSlot : Control
{
    private TextureRect _icon;
    private TextureRect _selected;
    private Label _count;
    private Label _abbreviation;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _selected = GetNode<TextureRect>("Selected");
        _count = GetNode<Label>("Count");
		_abbreviation = new Label
		{
			Name = "Abbreviation",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipText = true,
		};
		_abbreviation.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_abbreviation.AddThemeColorOverride("font_color", Colors.White);
		_abbreviation.AddThemeColorOverride("font_outline_color", Colors.Black);
		_abbreviation.AddThemeConstantOverride("outline_size", 3);
		_abbreviation.AddThemeFontSizeOverride("font_size", 6);
		AddChild(_abbreviation);
    }

    public void SetStack(ItemStack stack)
    {
        _icon.Texture = stack?.Item.Icon;
		_icon.Visible = stack != null;
		_abbreviation.Text = stack?.Item.HotbarLabel ?? "";
		_abbreviation.Visible = stack != null && !string.IsNullOrWhiteSpace(_abbreviation.Text);

        bool showCount = stack != null && stack.Count > 1;
        _count.Visible = showCount;
        if (showCount) _count.Text = stack.Count.ToString();
    }

    public void SetSelected(bool selected) => _selected.Visible = selected;
}

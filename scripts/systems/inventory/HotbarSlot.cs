using Godot;

public partial class HotbarSlot : PanelContainer
{
    private TextureRect _icon;
    private Label _count;

    public int SlotIndex { get; set; }

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("MarginContainer/Content/Icon");
        _count = GetNode<Label>("MarginContainer/Content/Count");
    }

    public void UpdateSlot(ItemStack stack)
    {
        if (_icon == null || _count == null)
            return;

        bool isEmpty = stack == null;

        _icon.Visible = !isEmpty;
        _count.Text = isEmpty || stack.Count <= 1
            ? ""
            : stack.Count.ToString();

        _icon.Texture = isEmpty ? null : stack.Item.Icon;
    }

    public void SetSelected(bool selected)
    {
        var style = new StyleBoxFlat();

        style.BgColor = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        style.BorderColor = selected
            ? new Color(1.0f, 0.85f, 0.25f)
            : new Color(0.40f, 0.43f, 0.48f);

        style.SetBorderWidthAll(selected ? 3 : 1);
        style.SetCornerRadiusAll(5);
        style.ContentMarginLeft = 5;
        style.ContentMarginRight = 5;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 5;

        AddThemeStyleboxOverride("panel", style);
    }
}

namespace ares.scripts.systems.items;

using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export(PropertyHint.Range, "1,999,1")] public int MaxStackSize { get; set; } = 64;
}
namespace ares.scripts.systems.items;

using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public string HotbarLabel { get; set; } = "";
    [Export(PropertyHint.Range, "1,999,1")] public int MaxStackSize { get; set; } = 64;
    [Export(PropertyHint.Range, "0,20,1")] public int CargoCost { get; set; }
    [Export(PropertyHint.Flags, "Repair")] public ItemCapability Capabilities { get; set; }

    public bool HasCapability(ItemCapability capability) => (Capabilities & capability) == capability;
}

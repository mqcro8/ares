namespace ares.scripts.systems.items;

using Godot;

[GlobalClass]
public partial class Item : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Texture { get; set; }
}

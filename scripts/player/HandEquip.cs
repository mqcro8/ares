namespace ares.scripts.player;

using Godot;
using ares.scripts.systems.items;

public partial class HandEquip : Node
{
    [Export] public EquipableItem EquippedItem { get; set; }
}

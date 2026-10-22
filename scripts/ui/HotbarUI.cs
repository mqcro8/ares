namespace ares.scripts.ui;

using Godot;
using ares.scripts.player;

public partial class HotbarUI : Control
{
    private Player _player;
    private HandEquip _handEquip;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;

        if (_player != null)
        {
            _handEquip = _player.FindChild("HandEquip") as HandEquip;

            var gridContainer = GetNode<GridContainer>("GridContainer");
            foreach (var child in gridContainer.GetChildren())
            {
                if (child is ItemButton button)
                    button.HandEquip = _handEquip;
            }
        }
    }
}

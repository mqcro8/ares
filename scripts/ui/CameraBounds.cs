using Godot;

namespace ares.scripts.ui;

public partial class CameraBounds : Camera2D
{
	[Export] public TileMapLayer TileMapLayer;

	public override void _Ready()
	{
		SetLimitsFromTileMap(TileMapLayer);
	}
	
	public void SetLimitsFromTileMap(TileMapLayer layer)
	{
		Rect2I used = layer.GetUsedRect();
		Vector2I tileSize = layer.TileSet.TileSize;

		LimitLeft   = used.Position.X * tileSize.X;
		LimitTop    = used.Position.Y * tileSize.Y;
		LimitRight  = used.End.X * tileSize.X;
		LimitBottom = used.End.Y * tileSize.Y;
	}
}

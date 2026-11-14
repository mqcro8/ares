namespace ares.scripts.gateway;

using Godot;

/// <summary>Scene-authored wall runs, painted identically in the editor and at runtime.</summary>
[Tool]
public partial class GatewayWalls : TileMapLayer
{
    private Godot.Collections.Array<Rect2I> _wallRuns = new();

    [Export]
    public Godot.Collections.Array<Rect2I> WallRuns
    {
        get => _wallRuns;
        set
        {
            _wallRuns = value;
            if (IsInsideTree()) PaintWalls();
        }
    }

    public override void _Ready() => PaintWalls();

    private void PaintWalls()
    {
        Clear();
        foreach (var run in _wallRuns)
            for (int y = run.Position.Y; y < run.End.Y; y++)
                for (int x = run.Position.X; x < run.End.X; x++)
                    SetCell(new Vector2I(x, y), 0, Vector2I.Zero);
    }
}

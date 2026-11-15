using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ares.scripts.gateway;

/// <summary>Walls stop the player; each room is reachable only through its door.</summary>
public partial class GatewayWallsTest : Node
{
    private int _assertions;

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            await Run();
            GD.Print($"GATEWAY WALLS PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Run()
    {
        var gateway = GD.Load<PackedScene>(Gateway.ScenePath).Instantiate<Gateway>();
        AddChild(gateway);
        gateway.Player.SetPhysicsProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var walls = gateway.GetNode<TileMapLayer>("Tilemaps_layers/Walls");
        var solid = new HashSet<Vector2I>(walls.GetUsedCells());
            Check(solid.Count == 188, "All scene-authored wall cells are painted");

        // The map's static perimeter walls are not part of the TileMapLayer.
        for (int x = 0; x <= 40; x++)
        {
            solid.Add(new Vector2I(x, 3));
            solid.Add(new Vector2I(x, 28));
        }
        for (int y = 3; y <= 28; y++)
        {
            solid.Add(new Vector2I(0, y));
            solid.Add(new Vector2I(40, y));
        }

        void Crossing(Vector2 from, Vector2 to, bool blocked, string message)
        {
            bool collision = gateway.Player.TestMove(new Transform2D(0, from), to - from);
            Check(collision == blocked, message);
        }

        Crossing(new(120, 176), new(200, 176), true, "Start room wall blocks passage above the corridor");
        Crossing(new(120, 256), new(200, 256), false, "Start room opens into the corridor");
        Crossing(new(392, 128), new(408, 128), true, "Top rooms have a solid divider");
        Crossing(new(392, 368), new(408, 368), true, "Bottom rooms have a solid divider");
        Crossing(new(48, 232), new(-16, 232), true, "Outer boundary blocks escape");

        foreach (int doorX in new[] { 272, 512 })
        {
            Crossing(new(doorX, 192), new(doorX, 224), false, "Top doorway admits the player");
            Crossing(new(doorX, 272), new(doorX, 304), false, "Bottom doorway admits the player");
            Crossing(new(doorX - 80, 192), new(doorX - 80, 224), true, "Top wall blocks doorway bypass");
            Crossing(new(doorX - 80, 272), new(doorX - 80, 304), true, "Bottom wall blocks doorway bypass");
        }

        var spawn = new Vector2I(5, 14);
        var rooms = new[] { new Vector2I(17, 8), new Vector2I(32, 8),
            new Vector2I(17, 23), new Vector2I(32, 23) };
        foreach (var room in rooms)
            Check(Reachable(solid, spawn, room), "Room is reachable from the hub");

        var sealedGate = new HashSet<Vector2I>(solid);
        for (int y = 13; y < 18; y++) sealedGate.Add(new Vector2I(10, y));
        foreach (var room in rooms)
            Check(!Reachable(sealedGate, spawn, room), "Main gate is the only annex entrance");

        for (int i = 0; i < rooms.Length; i++)
        {
            var sealedDoor = new HashSet<Vector2I>(solid);
            int startX = i % 2 == 0 ? 15 : 30;
            int doorY = i < 2 ? 13 : 18;
            for (int x = startX; x < startX + 4; x++) sealedDoor.Add(new Vector2I(x, doorY));
            Check(!Reachable(sealedDoor, spawn, rooms[i]), "Room cannot be reached by bypassing its door");
        }

        gateway.QueueFree();
    }

    private static bool Reachable(HashSet<Vector2I> solid, Vector2I start, Vector2I target)
    {
        var visited = new HashSet<Vector2I> { start };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var cell))
        {
            if (cell == target) return true;
            foreach (var step in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                var next = cell + step;
                if (next.X < 0 || next.X >= 41 || next.Y < 3 || next.Y >= 29 ||
                    solid.Contains(next) || !visited.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }
}

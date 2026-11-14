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
        Check(solid.Count == 533, "All scene-authored wall cells are painted");

        void Crossing(Vector2 from, Vector2 to, bool blocked, string message)
        {
            bool collision = gateway.Player.TestMove(new Transform2D(0, from), to - from);
            Check(collision == blocked, message);
        }

        Crossing(new(464, 320), new(544, 320), true, "Hub wall blocks passage away from gate");
        Crossing(new(464, 432), new(544, 432), false, "Main gate admits the player");
        Crossing(new(1120, 320), new(1184, 320), true, "North rooms have a solid divider");
        Crossing(new(1120, 600), new(1184, 600), true, "South rooms have a solid divider");
        Crossing(new(48, 432), new(-16, 432), true, "Outer boundary blocks escape");

        foreach (int doorX in new[] { 976, 1328 })
        {
            Crossing(new(doorX, 360), new(doorX, 432), false, "North doorway admits player");
            Crossing(new(doorX, 432), new(doorX, 512), false, "South doorway admits player");
            Crossing(new(doorX - 80, 360), new(doorX - 80, 432), true, "North wall blocks doorway bypass");
            Crossing(new(doorX - 80, 432), new(doorX - 80, 512), true, "South wall blocks doorway bypass");
        }

        var spawn = new Vector2I(16, 26);
        var rooms = new[] { new Vector2I(61, 19), new Vector2I(83, 19),
            new Vector2I(61, 44), new Vector2I(83, 44) };
        foreach (var room in rooms)
            Check(Reachable(solid, spawn, room), "Room is reachable from the hub");

        var sealedGate = new HashSet<Vector2I>(solid);
        for (int y = 25; y < 29; y++) sealedGate.Add(new Vector2I(31, y));
        foreach (var room in rooms)
            Check(!Reachable(sealedGate, spawn, room), "Main gate is the only annex entrance");

        for (int i = 0; i < rooms.Length; i++)
        {
            var sealedDoor = new HashSet<Vector2I>(solid);
            int startX = i % 2 == 0 ? 59 : 81;
            int doorY = i < 2 ? 24 : 29;
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
                if (next.X < 0 || next.X >= 94 || next.Y < 0 || next.Y >= 53 ||
                    solid.Contains(next) || !visited.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }
}

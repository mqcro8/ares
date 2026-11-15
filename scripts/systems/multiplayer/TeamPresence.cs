using System.Collections.Generic;
using Godot;

namespace Ares.Systems.Multiplayer;

/// <summary>Draws simple remote crew markers in the active world scene.</summary>
public partial class TeamPresence : Node2D
{
    private readonly Dictionary<int, (string Scene, Vector2 Position)> _players = new();

    public void SetRemote(int slot, string scene, Vector2 position)
    {
        _players[slot] = (scene, position);
        QueueRedraw();
    }

    public override void _Draw()
    {
        string currentScene = GetTree()?.CurrentScene?.SceneFilePath ?? "";
        foreach (var (slot, state) in _players)
        {
            if (state.Scene != currentScene) continue;
            DrawCircle(state.Position, 10, new Color(0.2f, 0.85f, 1f, 0.9f));
            DrawArc(state.Position, 13, 0, Mathf.Tau, 24, Colors.White, 2);
            DrawString(ThemeDB.FallbackFont, state.Position + new Vector2(-28, -17),
                $"CREW {slot + 1}", HorizontalAlignment.Center, 56, 10, Colors.White);
        }
    }
}

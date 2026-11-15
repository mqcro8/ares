using System.Collections.Generic;
using Godot;

namespace Ares.Systems.Multiplayer;

/// <summary>Draws simple remote crew markers in the active world scene.</summary>
public partial class TeamPresence : Node2D
{
    private sealed class RemotePlayer
    {
        public string Scene;
        public Vector2 Current;
        public Vector2 Target;
        public bool HasPosition;
    }

    private readonly Dictionary<int, RemotePlayer> _players = new();
    private Label _networkLabel;
    private double _statsTimer;
    private int _receivedUpdates;
    private int _updatesPerSecond;

    public override void _Ready()
    {
        var layer = new CanvasLayer { Layer = 25, Name = "TeamNetworkStats" };
        AddChild(layer);
        _networkLabel = new Label
        {
            Position = new Vector2(16, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "CREW NETWORK  //  PING --",
        };
        _networkLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.92f, 1f));
        _networkLabel.AddThemeFontSizeOverride("font_size", 12);
        layer.AddChild(_networkLabel);
    }

    public void SetRemote(int slot, string scene, Vector2 position)
    {
        if (!_players.TryGetValue(slot, out RemotePlayer remote))
        {
            remote = new RemotePlayer { Scene = scene, Current = position, Target = position, HasPosition = true };
            _players[slot] = remote;
        }
        else
        {
            remote.Scene = scene;
            if (!remote.HasPosition)
            {
                remote.Current = position;
                remote.HasPosition = true;
            }
            remote.Target = position;
        }
        _receivedUpdates++;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        const float FollowRate = 14f;
        float blend = 1f - Mathf.Exp(-FollowRate * (float)delta);
        foreach (RemotePlayer player in _players.Values)
            player.Current = player.Current.Lerp(player.Target, blend);

        _statsTimer += delta;
        if (_statsTimer >= 1)
        {
            _updatesPerSecond = _receivedUpdates;
            _receivedUpdates = 0;
            _statsTimer = 0;
        }

        var team = TeamSession.Instance;
        if (_networkLabel != null)
        {
            _networkLabel.Visible = team?.Active == true;
            if (!_networkLabel.Visible) return;
            string ping = team.LatencyMs < 0 ? "--" : $"{team.LatencyMs:0}";
            _networkLabel.Text = $"CREW NETWORK  //  PING {ping} ms  //  UPDATES {_updatesPerSecond}/s";
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        string currentScene = GetTree()?.CurrentScene?.SceneFilePath ?? "";
        foreach (var (slot, player) in _players)
        {
            if (player.Scene != currentScene || !player.HasPosition) continue;
            DrawCircle(player.Current, 10, new Color(0.2f, 0.85f, 1f, 0.9f));
            DrawArc(player.Current, 13, 0, Mathf.Tau, 24, Colors.White, 2);
            DrawString(ThemeDB.FallbackFont, player.Current + new Vector2(-28, -17),
                $"CREW {slot + 1}", HorizontalAlignment.Center, 56, 10, Colors.White);
        }
    }
}

using Godot;
using Ares.Systems.Controllers;
using Ares.Systems.Multiplayer;

namespace ares.scripts.rocket_launch;

/// <summary>Launch-scene choice and two-to-four-player ready room.</summary>
public partial class TeamLaunchLobby : CanvasLayer
{
    [Signal] public delegate void LaunchSelectedEventHandler(bool teamMode);
    private LineEdit _relay, _room;
    private Label _status, _roster;
    private Button _host, _join, _solo, _ready, _start, _leave;
    private TeamSession _team;

    public override void _Ready()
    {
        Layer = 80;
        _team = TeamSession.Instance;
        Build();
        if (_team?.Active == true) ShowLobby();
        else ShowChoice();
        if (_team != null)
        {
            _team.TeamChanged += RefreshLobby;
            _team.TeamMessage += OnTeamMessage;
        }
    }

    private void Build()
    {
        var shade = new ColorRect { Color = new Color(0.015f, 0.025f, 0.04f, 0.95f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(600, 420) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);
        var title = new Label { Text = "MISSION CREW // LAUNCH" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(title);
        column.AddChild(new Label { Text = "Training remains individual. Start a crew mission with 2–4 players." });
        _relay = new LineEdit { PlaceholderText = "ngrok URL or ws://LAN-IP:8000", Text = _team?.RelayUrl ?? "" };
        column.AddChild(_relay);
        var codeRow = new HBoxContainer();
        _room = new LineEdit { PlaceholderText = "ROOM CODE", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        codeRow.AddChild(_room);
        column.AddChild(codeRow);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_status);
        _roster = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_roster);
        _host = AddButton(column, "CREATE CREW (HOST)", Host);
        _join = AddButton(column, "JOIN CREW", Join);
        _solo = AddButton(column, "GO SOLO", () => EmitSignal(SignalName.LaunchSelected, false));
        _ready = AddButton(column, "READY", () => { _team?.SetReady(true); _ready.Disabled = true; });
        _start = AddButton(column, "START TEAM MISSION", () =>
        {
            if (_team?.StartMission() == true) EmitSignal(SignalName.LaunchSelected, true);
        });
        _leave = AddButton(column, "LEAVE ROOM", () => { _team?.Stop(); ShowChoice(); });
    }

    private static Button AddButton(VBoxContainer parent, string text, System.Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 38) };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void Host()
    {
        if (_team == null) return;
        string code = ControllerRelay.GenerateRoomCode();
        _room.Text = code;
        if (!_team.StartHost(_relay.Text, code)) { _status.Text = "Enter a valid relay URL."; return; }
        _team.SetReady(true);
        ShowLobby();
    }

    private void Join()
    {
        if (_team == null) return;
        if (!_team.Join(_relay.Text, _room.Text)) { _status.Text = "Enter a valid relay URL and room code."; return; }
        ShowLobby();
    }

    private void ShowChoice()
    {
        _host.Show(); _join.Show(); _solo.Show();
        _ready.Hide(); _start.Hide(); _leave.Hide(); _roster.Hide();
        _status.Text = "Host: run the local relay, start ngrok on port 8000, then share the HTTPS URL and room code.";
    }

    private void ShowLobby()
    {
        _host.Hide(); _join.Hide(); _solo.Hide();
        _ready.Show(); _leave.Show(); _roster.Show();
        RefreshLobby();
    }

    private void RefreshLobby()
    {
        if (_team == null || !_team.Active) return;
        _status.Text = $"{_team.Status}  ROOM {_team.RoomCode}\nShare relay URL: {_team.RelayUrl}";
        var lines = new System.Collections.Generic.List<string>();
        for (int slot = 0; slot < 4; slot++)
        {
            Godot.Collections.Dictionary player = null;
            foreach (var candidate in _team.Players)
                if (candidate.TryGetValue("slot", out Variant value) && value.AsInt32() == slot) player = candidate;
            bool connected = player?.TryGetValue("connected", out Variant connectedValue) == true && connectedValue.AsBool();
            bool ready = player?.TryGetValue("ready", out Variant readyValue) == true && readyValue.AsBool();
            lines.Add($"PLAYER {slot + 1}: {(connected ? ready ? "READY" : "CONNECTED" : player == null ? "OPEN SLOT" : "WAITING TO RECONNECT")}");
        }
        _roster.Text = string.Join("\n", lines);
        if (_team.MissionStarted)
            _roster.Text += $"\nCREW SIZE: {_team.ParticipantCount}  //  MISSION IN PROGRESS";
        _ready.Visible = !_team.IsHost;
        _ready.Disabled = !_team.Active || _team.LocalSlot < 0;
        _start.Visible = _team.IsHost;
        _start.Disabled = !_team.EveryoneReady;
    }

    private void OnTeamMessage(string type, string json)
    {
        if (type == "team_start")
            EmitSignal(SignalName.LaunchSelected, true);
    }

    public override void _ExitTree()
    {
        if (_team != null)
        {
            _team.TeamChanged -= RefreshLobby;
            _team.TeamMessage -= OnTeamMessage;
        }
    }
}

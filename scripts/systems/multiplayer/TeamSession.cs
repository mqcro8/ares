using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ares.Systems.Multiplayer;

/// <summary>
/// Small reconnecting client for the host-run team relay. The relay only carries
/// lobby/state messages; the host remains authoritative for mission progress.
/// </summary>
public partial class TeamSession : Node
{
    private const string SavePath = "user://team-session.cfg";
    private const double RetrySeconds = 2;
    private WebSocketPeer _socket;
    private string _relayUrl = "";
    private string _room = "";
    private string _token = "";
    private double _retry;
    private bool _connecting;
    private bool _localReady;
    private bool _missionStarted;
    private readonly bool[] _gatewayTasks = new bool[4];
    private readonly bool[] _marsTasks = new bool[7];
    private readonly bool[] _roverResults = new bool[4];
    private readonly double[] _teamScores = { -1, -1, -1, -1 };
    private readonly bool[] _teamPasses = new bool[4];
    private readonly bool[] _participants = new bool[4];
    private readonly HashSet<string> _claimedOxygen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<bool>> _pendingOxygenClaims = new(StringComparer.Ordinal);
    private bool _applyingRemoteState;
    private double _positionTimer;
    private readonly Dictionary<int, Godot.Collections.Dictionary> _remotePositions = new();
    private TeamPresence _presence;

    [Signal] public delegate void TeamChangedEventHandler();
    [Signal] public delegate void TeamMessageEventHandler(string type, string json);
    [Signal] public delegate void TeamStateReceivedEventHandler(string json);
    [Signal] public delegate void TeamPositionReceivedEventHandler(int slot, string scene, Vector2 position, string facing, bool flip);

    public static TeamSession Instance { get; private set; }
    public bool Active { get; private set; }
    public bool IsHost { get; private set; }
    public int LocalSlot { get; private set; } = -1;
    public string RoomCode => _room;
    public string RelayUrl => _relayUrl;
    public string ReconnectToken => _token;
    public string Status { get; private set; } = "Offline";
    public Godot.Collections.Array<Godot.Collections.Dictionary> Players { get; private set; } = new();
    public bool EveryoneReady => Players.Count(player =>
        Get(player, "connected", false).AsBool() &&
        Get(player, "ready", false).AsBool()) >= 2 &&
        Players.Where(player => Get(player, "connected", false).AsBool())
            .All(player => Get(player, "ready", false).AsBool());
    public bool MissionStarted => _missionStarted;
    public bool IsGatewayTaskComplete(int slot) =>
        slot >= 0 && slot < _gatewayTasks.Length && _gatewayTasks[slot];
    public bool AllGatewayTasksComplete => _gatewayTasks.All(done => done);
    public int ParticipantCount => _participants.Count(participating => participating);
    public bool IsParticipant(int slot) => slot >= 0 && slot < _participants.Length && _participants[slot];
    public int GatewayTaskOwner(int taskIndex) => ParticipantCount == 0 || taskIndex < 0 || taskIndex > 3
        ? -1 : ActiveSlots[taskIndex % ParticipantCount];
    public bool AllRoversComplete => ActiveSlots.All(slot => _roverResults[slot]);
    public bool AllTeamScoresSubmitted => ActiveSlots.Length >= 2 && ActiveSlots.All(slot => _teamScores[slot] >= 0);
    public double TeamAverageScore => AllTeamScoresSubmitted ? ActiveSlots.Average(slot => _teamScores[slot]) : 0;
    public bool TeamPassed => AllTeamScoresSubmitted && ActiveSlots.All(slot => _teamPasses[slot]);
    public int[] ActiveSlots => Enumerable.Range(0, _participants.Length).Where(slot => _participants[slot]).ToArray();
    public bool ApplyingRemoteState => _applyingRemoteState;
    public bool IsOxygenPickupClaimed(string id) => _claimedOxygen.Contains(id);

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GetTree().SceneChanged += OnSceneChanged;
        OnSceneChanged();
        ApplyCommandLine();
    }

    private void OnSceneChanged()
    {
        if (_presence != null && GodotObject.IsInstanceValid(_presence))
            _presence.QueueFree();
        _presence = null;
        var scene = GetTree()?.CurrentScene;
        if (scene == null) return;
        _presence = new TeamPresence { Name = "TeamPresence" };
        scene.AddChild(_presence);
        foreach (var pair in _remotePositions)
        {
            var state = pair.Value;
            _presence.SetRemote(pair.Key, Get(state, "scene", "").AsString(),
                new Vector2((float)Get(state, "x", 0).AsDouble(), (float)Get(state, "y", 0).AsDouble()));
        }
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        ApplyDisconnectGate();
        PublishLocalPosition(delta);
        if (_socket == null)
        {
            _retry -= delta;
            if (_retry <= 0) Connect();
            return;
        }

        _socket.Poll();
        if (_socket.GetReadyState() == WebSocketPeer.State.Closed)
        {
            _socket = null;
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].TryGetValue("slot", out Variant slot) && slot.AsInt32() == LocalSlot)
                    Players[i]["connected"] = false;
            Status = "Disconnected — waiting to reconnect";
            ApplyDisconnectGate();
            _retry = RetrySeconds;
            EmitSignal(SignalName.TeamChanged);
            return;
        }
        while (_socket.GetReadyState() == WebSocketPeer.State.Open &&
               _socket.GetAvailablePacketCount() > 0)
            HandleMessage(_socket.GetPacket().GetStringFromUtf8());
    }

    private void PublishLocalPosition(double delta)
    {
        if (!_missionStarted || !IsParticipant(LocalSlot) || !EveryoneReady) return;
        _positionTimer -= delta;
        if (_positionTimer > 0) return;
        _positionTimer = 0.1;
        var player = GetTree()?.GetFirstNodeInGroup("player") as ares.scripts.player.Player;
        var scene = GetTree()?.CurrentScene;
        if (player == null || scene == null) return;
        var animation = player.GetNodeOrNull<ares.scripts.player.PlayerAnimation>("AnimatedSprite2D");
        string facing = animation?.Facing ?? "down";
        bool flip = animation?.FlipH ?? false;
        var packet = new Godot.Collections.Dictionary
        {
            ["type"] = "team_position", ["scene"] = scene.SceneFilePath,
            ["x"] = player.GlobalPosition.X, ["y"] = player.GlobalPosition.Y,
            ["facing"] = facing, ["flip"] = flip,
        };
        if (IsHost) packet["slot"] = 0;
        Send(packet);
    }

    private void ApplyDisconnectGate()
    {
        if (!_missionStarted || GetTree() == null) return;
        bool wait = ActiveSlots.Length < 2 || ActiveSlots.Any(slot =>
            !Players.Any(player => Get(player, "slot", -1).AsInt32() == slot &&
                                  Get(player, "connected", false).AsBool() &&
                                  Get(player, "ready", false).AsBool()));
        if (GetTree().Paused != wait) GetTree().Paused = wait;
        Status = wait ? "Crew member disconnected — waiting for reconnection" : "Crew connected";
    }

    public bool StartHost(string relayUrl, string roomCode)
        => Start(relayUrl, roomCode, host: true, token: "");

    public bool Join(string relayUrl, string roomCode)
        => Start(relayUrl, roomCode, host: false, token: LoadToken(roomCode));

    private bool Start(string relayUrl, string roomCode, bool host, string token)
    {
        string normalized = NormalizeRelayUrl(relayUrl);
        string room = roomCode.Trim().ToUpperInvariant();
        if (normalized.Length == 0 || room.Length is < 4 or > 8) return false;
        foreach (char c in room)
            if (!(c is >= 'A' and <= 'Z' or >= '0' and <= '9')) return false;
        Stop();
        Array.Clear(_gatewayTasks);
        Array.Clear(_marsTasks);
        Array.Clear(_roverResults);
        Array.Fill(_teamScores, -1);
        Array.Clear(_teamPasses);
        Array.Clear(_participants);
        _claimedOxygen.Clear();
        foreach (Action<bool> pending in _pendingOxygenClaims.Values)
            pending?.Invoke(false);
        _pendingOxygenClaims.Clear();
        _relayUrl = normalized;
        _room = room;
        _token = token;
        IsHost = host;
        _localReady = host;
        Active = true;
        LocalSlot = host ? 0 : -1;
        Status = host ? "Opening team room..." : "Joining team room...";
        Connect();
        return true;
    }

    public void Stop()
    {
        Active = false;
        _socket?.Close(1000, "Leaving team room");
        _socket = null;
        LocalSlot = -1;
        Players = new();
        Status = "Offline";
        _localReady = false;
        _missionStarted = false;
        if (GetTree() != null) GetTree().Paused = false;
        EmitSignal(SignalName.TeamChanged);
    }

    public void SetReady(bool ready)
    {
        if (LocalSlot < 0) return;
        _localReady = ready;
        Send(new Godot.Collections.Dictionary { ["type"] = "team_ready", ["slot"] = LocalSlot, ["ready"] = ready });
    }

    public bool StartMission()
    {
        if (!IsHost || !EveryoneReady) return false;
        Array.Clear(_participants);
        foreach (var player in Players)
        {
            int slot = Get(player, "slot", -1).AsInt32();
            if (slot is >= 0 and < 4 && Get(player, "connected", false).AsBool() &&
                Get(player, "ready", false).AsBool())
                _participants[slot] = true;
        }
        if (ParticipantCount < 2) return false;
        _missionStarted = true;
        Send(new Godot.Collections.Dictionary { ["type"] = "team_start" });
        return true;
    }

    public void PublishState(Godot.Collections.Dictionary state)
    {
        if (!IsHost) return;
        Send(new Godot.Collections.Dictionary { ["type"] = "team_state", ["state"] = state });
    }

    /// <summary>Reports only the repair assigned to this player's fixed crew slot.</summary>
    public bool MarkGatewayTaskComplete(int slot)
    {
        if (!Active || !IsParticipant(LocalSlot) || GatewayTaskOwner(slot) != LocalSlot ||
            slot < 0 || slot > 3) return false;
        if (!IsHost)
        {
            Send(new Godot.Collections.Dictionary
            {
                ["type"] = "team_event", ["name"] = "gateway_task_complete", ["index"] = slot,
            });
            return true;
        }
        _gatewayTasks[slot] = true;
        PublishSharedProgress();
        return true;
    }

    public bool MarkMarsTaskComplete(int index)
    {
        if (!Active || index < 0 || index >= _marsTasks.Length) return false;
        if (!IsHost)
        {
            Send(new Godot.Collections.Dictionary
            {
                ["type"] = "team_event", ["name"] = "mars_task_complete", ["index"] = index,
            });
            return true;
        }
        _marsTasks[index] = true;
        PublishSharedProgress();
        return true;
    }

    public bool MarkRoverComplete(int slot)
    {
        if (!Active || LocalSlot != slot || !IsParticipant(slot)) return false;
        if (!IsHost)
        {
            Send(new Godot.Collections.Dictionary
            {
                ["type"] = "team_event", ["name"] = "rover_complete", ["index"] = slot,
            });
            return true;
        }
        _roverResults[slot] = true;
        PublishSharedProgress();
        return true;
    }

    public void RequestOxygenPickup(string id, Action<bool> completed)
    {
        if (!Active || string.IsNullOrWhiteSpace(id) || id.Length > 160 ||
            id.Any(character => !(char.IsLetterOrDigit(character) || "_.:/-".Contains(character))))
        {
            completed?.Invoke(false);
            return;
        }
        if (_claimedOxygen.Contains(id))
        {
            completed?.Invoke(false);
            return;
        }
        _pendingOxygenClaims[id] = completed;
        SendOxygenClaim(id);
    }

    public bool SubmitTeamScore(double score, bool passed)
    {
        if (!Active || !IsParticipant(LocalSlot) || !double.IsFinite(score) || score is < 0 or > 100) return false;
        if (!IsHost)
        {
            Send(new Godot.Collections.Dictionary
            {
                ["type"] = "team_event", ["name"] = "score_submit",
                ["score"] = score, ["passed"] = passed,
            });
            return true;
        }
        _teamScores[0] = score;
        _teamPasses[0] = passed;
        PublishSharedProgress();
        return true;
    }

    private void SendOxygenClaim(string id) =>
        Send(new Godot.Collections.Dictionary
        {
            ["type"] = "team_event", ["name"] = "oxygen_pickup_claim",
            ["slot"] = LocalSlot, ["id"] = id,
        });

    private void RetryOxygenClaims()
    {
        foreach (string id in _pendingOxygenClaims.Keys.ToArray()) SendOxygenClaim(id);
    }

    private void PublishSharedProgress()
    {
        var gatewayTasks = new Godot.Collections.Array();
        foreach (bool complete in _gatewayTasks) gatewayTasks.Add(complete);
        var marsTasks = new Godot.Collections.Array();
        foreach (bool complete in _marsTasks) marsTasks.Add(complete);
        var roverResults = new Godot.Collections.Array();
        foreach (bool complete in _roverResults) roverResults.Add(complete);
        var oxygenClaims = new Godot.Collections.Array();
        foreach (string id in _claimedOxygen) oxygenClaims.Add(id);
        var scores = new Godot.Collections.Array();
        foreach (double score in _teamScores) scores.Add(score);
        var passes = new Godot.Collections.Array();
        foreach (bool passed in _teamPasses) passes.Add(passed);
        var state = new Godot.Collections.Dictionary
        {
            ["gateway_tasks"] = gatewayTasks,
            ["mars_tasks"] = marsTasks,
            ["rover_results"] = roverResults,
            ["claimed_oxygen"] = oxygenClaims,
            ["team_scores"] = scores,
            ["team_passes"] = passes,
        };
        PublishState(state);
        EmitSignal(SignalName.TeamStateReceived, Json.Stringify(state));
    }

    private void ApplyTeamState(Variant state)
    {
        if (state.VariantType != Variant.Type.Dictionary) return;
        var dictionary = state.AsGodotDictionary();
        _applyingRemoteState = true;
        try
        {
            ApplyTaskArray(dictionary, "gateway_tasks", _gatewayTasks);
            ApplyTaskArray(dictionary, "mars_tasks", _marsTasks);
            ApplyTaskArray(dictionary, "rover_results", _roverResults);
            ApplyScoreArray(dictionary, "team_scores", _teamScores);
            ApplyTaskArray(dictionary, "team_passes", _teamPasses);
            if (dictionary.TryGetValue("claimed_oxygen", out Variant claims) &&
                claims.VariantType == Variant.Type.Array)
            {
                _claimedOxygen.Clear();
                foreach (Variant id in claims.AsGodotArray()) _claimedOxygen.Add(id.AsString());
            }
            var mission = ares.scripts.mission.MissionState.Instance;
            if (mission == null) return;
            if (_marsTasks[0]) mission.MarkGeneratorRepaired();
            if (_marsTasks[1]) mission.MarkWireBoxRepaired();
            if (_marsTasks[2]) mission.MarkSignalTableRepaired();
            for (int i = 0; i < ares.scripts.mission.MissionState.DoorAccessCount; i++)
                if (_marsTasks[i + 3]) mission.SetDoorAccess(i, true);
        }
        finally { _applyingRemoteState = false; }
    }

    private static void ApplyTaskArray(Godot.Collections.Dictionary dictionary, string key, bool[] destination)
    {
        if (!dictionary.TryGetValue(key, out Variant tasks) || tasks.VariantType != Variant.Type.Array) return;
        var values = tasks.AsGodotArray();
        for (int i = 0; i < destination.Length && i < values.Count; i++)
            destination[i] = values[i].AsBool();
    }

    private static void ApplyScoreArray(Godot.Collections.Dictionary dictionary, string key, double[] destination)
    {
        if (!dictionary.TryGetValue(key, out Variant scores) || scores.VariantType != Variant.Type.Array) return;
        var values = scores.AsGodotArray();
        for (int i = 0; i < destination.Length && i < values.Count; i++)
            destination[i] = values[i].AsDouble();
    }

    private void Connect()
    {
        if (!Active || _connecting) return;
        _connecting = true;
        _socket = new WebSocketPeer();
        string role = IsHost ? "team_host" : "player";
        string token = string.IsNullOrEmpty(_token) ? "" : $"&token={Uri.EscapeDataString(_token)}";
        Error error = _socket.ConnectToUrl($"{_relayUrl}?room={_room}&role={role}{token}");
        _connecting = false;
        if (error != Error.Ok)
        {
            _socket = null;
            Status = "Relay unavailable — retrying";
            _retry = RetrySeconds;
        }
        else Status = "Connecting...";
        EmitSignal(SignalName.TeamChanged);
    }

    private void HandleMessage(string text)
    {
        var parsed = Json.ParseString(text);
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        var message = parsed.AsGodotDictionary();
        string type = Get(message, "type", "").AsString();
        if (type == "team_joined")
        {
            LocalSlot = (int)Get(message, "slot", -1).AsInt64();
            _token = Get(message, "token", "").AsString();
            SaveToken();
            ReadPlayers(Get(message, "players", new Godot.Collections.Array()));
            Status = $"Connected — player {LocalSlot + 1}/4";
            Send(new Godot.Collections.Dictionary { ["type"] = "team_ready", ["slot"] = LocalSlot, ["ready"] = _localReady });
            RetryOxygenClaims();
        }
        else if (type == "team_lobby")
        {
            ReadPlayers(Get(message, "players", new Godot.Collections.Array()));
            Status = "Connected — waiting for four ready players";
            if (IsHost && _localReady)
                Send(new Godot.Collections.Dictionary { ["type"] = "team_ready", ["slot"] = 0, ["ready"] = true });
            else if (!IsHost && LocalSlot >= 0 && _localReady)
                Send(new Godot.Collections.Dictionary { ["type"] = "team_ready", ["slot"] = LocalSlot, ["ready"] = true });
            if (IsHost && _missionStarted && EveryoneReady)
                Send(new Godot.Collections.Dictionary { ["type"] = "team_start" });
            RetryOxygenClaims();
        }
        else if (type == "team_state")
        {
            Variant state = Get(message, "state", new Godot.Collections.Dictionary());
            ApplyTeamState(state);
            string json = Json.Stringify(state);
            EmitSignal(SignalName.TeamStateReceived, json);
        }
        else if (type == "team_event" && IsHost)
        {
            int index = (int)Get(message, "index", -1).AsInt64();
            int slot = (int)Get(message, "slot", -1).AsInt64();
            string name = Get(message, "name", "").AsString();
            if (name == "gateway_task_complete" && index is >= 0 and <= 3 &&
                IsParticipant(slot) && GatewayTaskOwner(index) == slot)
            {
                _gatewayTasks[slot] = true;
                PublishSharedProgress();
            }
            else if (name == "mars_task_complete" && index is >= 0 and <= 6)
            {
                _marsTasks[index] = true;
                PublishSharedProgress();
            }
            else if (name == "rover_complete" && index == slot && IsParticipant(slot))
            {
                _roverResults[slot] = true;
                PublishSharedProgress();
            }
            else if (name == "score_submit" && index == slot && IsParticipant(slot))
            {
                _teamScores[slot] = Get(message, "score", -1).AsDouble();
                _teamPasses[slot] = Get(message, "passed", false).AsBool();
                PublishSharedProgress();
            }
        }
        else if (type == "team_event" && IsHost &&
                 Get(message, "name", "").AsString() == "oxygen_pickup_claimed")
        {
            string id = Get(message, "id", "").AsString();
            if (!string.IsNullOrEmpty(id) && _claimedOxygen.Add(id))
                PublishSharedProgress();
        }
        else if (type == "team_pickup_result")
        {
            string id = Get(message, "id", "").AsString();
            bool accepted = Get(message, "accepted", false).AsBool();
            if (_pendingOxygenClaims.Remove(id, out Action<bool> completed))
                completed?.Invoke(accepted);
            if (accepted) _claimedOxygen.Add(id);
        }
        else if (type == "team_position")
        {
            int slot = (int)Get(message, "slot", -1).AsInt64();
            if (slot < 0 || slot > 3 || slot == LocalSlot) return;
            string scene = Get(message, "scene", "").AsString();
            Vector2 position = new((float)Get(message, "x", 0).AsDouble(),
                (float)Get(message, "y", 0).AsDouble());
            string facing = Get(message, "facing", "down").AsString();
            bool flip = Get(message, "flip", false).AsBool();
            _remotePositions[slot] = new Godot.Collections.Dictionary
            {
                ["scene"] = scene, ["x"] = position.X, ["y"] = position.Y,
                ["facing"] = facing, ["flip"] = flip,
            };
            _presence?.SetRemote(slot, scene, position);
            EmitSignal(SignalName.TeamPositionReceived, slot, scene, position, facing, flip);
        }
        else if (type == "team_start")
        {
            if (message.TryGetValue("participants", out Variant participantValue) &&
                participantValue.VariantType == Variant.Type.Array)
            {
                Array.Clear(_participants);
                foreach (Variant slotValue in participantValue.AsGodotArray())
                {
                    int slot = (int)slotValue.AsInt64();
                    if (slot is >= 0 and < 4) _participants[slot] = true;
                }
            }
            _missionStarted = true;
            GetTree().Paused = !EveryoneReady;
        }
        EmitSignal(SignalName.TeamMessage, type, text);
        EmitSignal(SignalName.TeamChanged);
    }

    private void ReadPlayers(Variant value)
    {
        Players = new();
        if (value.VariantType != Variant.Type.Array) return;
        foreach (Variant player in value.AsGodotArray())
            if (player.VariantType == Variant.Type.Dictionary)
                Players.Add(player.AsGodotDictionary());
    }

    private static Variant Get(Godot.Collections.Dictionary dictionary, string key, Variant fallback)
        => dictionary.TryGetValue(key, out Variant value) ? value : fallback;

    private void Send(Godot.Collections.Dictionary message)
    {
        if (_socket?.GetReadyState() == WebSocketPeer.State.Open)
            _socket.SendText(Json.Stringify(message));
    }

    private void SaveToken()
    {
        var config = new ConfigFile();
        config.Load(SavePath);
        config.SetValue("rooms", _room, _token);
        config.Save(SavePath);
    }

    private string LoadToken(string room)
    {
        var config = new ConfigFile();
        return config.Load(SavePath) == Error.Ok
            ? config.GetValue("rooms", room, "").AsString()
            : "";
    }

    private void ApplyCommandLine()
    {
        string relay = "", room = "";
        bool host = false, join = false;
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--team-host", StringComparison.Ordinal)) host = true;
            else if (argument.StartsWith("--team-join", StringComparison.Ordinal)) join = true;
            else if (argument.StartsWith("--relay=", StringComparison.Ordinal)) relay = argument["--relay=".Length..];
            else if (argument.StartsWith("--room=", StringComparison.Ordinal)) room = argument["--room=".Length..];
        }
        if (host && room.Length == 0) room = Ares.Systems.Controllers.ControllerRelay.GenerateRoomCode();
        if (host) StartHost(relay, room);
        else if (join) Join(relay, room);
    }

    private static string NormalizeRelayUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string value = raw.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "wss://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return "";
        string scheme = uri.Scheme.ToLowerInvariant();
        if (scheme == "https") scheme = "wss";
        if (scheme == "http") scheme = "ws";
        if (scheme is not ("ws" or "wss") || string.IsNullOrEmpty(uri.Host)) return "";
        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/index.html".Length];
        if (path == "/") path = "";
        if (path.Length == 0 && scheme == "wss") path = "/api/relay";
        return $"{scheme}://{uri.Authority}{path}";
    }

    public override void _ExitTree()
    {
        GetTree().SceneChanged -= OnSceneChanged;
        Stop();
        if (Instance == this) Instance = null;
    }
}

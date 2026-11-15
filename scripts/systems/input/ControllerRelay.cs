using Godot;
using System;

namespace Ares.Systems.Controllers;

public enum ControllerLinkState
{
    Disabled,
    NoRelay,
    Connecting,
    WaitingForController,
    ControllerConnected,
    Reconnecting,
}

/// <summary>
/// Autoload that keeps a WebSocket connection to the phone relay and feeds
/// parsed controller messages into <see cref="ControllerInputBridge"/>.
/// </summary>
public partial class ControllerRelay : Node
{
    public const string ConfigPath = "user://controller.cfg";
    public const string RelaySetting = "ares_controller/relay_url";
    public const string EnabledSetting = "ares_controller/enabled";
    public const string RoomSetting = "ares_controller/room_code";
    public const string JoinPath = "/";
    public const string RelayEndpointPath = "/api/relay";

    private const string ConfigSection = "controller";
    private const double ReconnectDelay = 2.0;
    private const double ContextInterval = 1.0 / 15.0;
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int RoomLength = 5;

    public static ControllerRelay Instance { get; private set; }

    [Signal] public delegate void StatusChangedEventHandler();

    private readonly ControllerInputBridge _bridge = new();
    private WebSocketPeer _socket;
    private double _reconnectTimer;
    private double _contextTimer;
    private bool _controllerConnected;
    private bool _wasOpen;
    private bool _pendingStart;
    private bool _forceContext;
    private ulong _lastContextRevision;

    public bool Enabled { get; private set; }
    public string RelayUrl { get; private set; } = "";
    public string RoomCode { get; private set; } = "";
    public ControllerLinkState State { get; private set; } = ControllerLinkState.Disabled;
    public bool IsControllerConnected => _controllerConnected;

    public string JoinUrl => ToJoinUrl(RelayUrl, RoomCode);

    public string StatusText => State switch
    {
        ControllerLinkState.Disabled => "Off",
        ControllerLinkState.NoRelay => "Set relay URL",
        ControllerLinkState.Connecting => "Connecting...",
        ControllerLinkState.WaitingForController => "Waiting for phone",
        ControllerLinkState.ControllerConnected => "Phone linked",
        ControllerLinkState.Reconnecting => "Relay offline, retrying...",
        _ => "Unknown",
    };

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        if (IsHeadless || HasNoControllerArgument())
        {
            Enabled = false;
            return;
        }

        LoadConfig();
        ApplyOverrides();
        _pendingStart = true;
    }

    public override void _Process(double delta)
    {
        if (_pendingStart)
        {
            _pendingStart = false;
            StartConfigured();
            return;
        }

        if (!Enabled) return;

        if (_socket == null)
        {
            if (_reconnectTimer <= 0) return;
            _reconnectTimer -= delta;
            if (_reconnectTimer <= 0) Connect();
            return;
        }

        _socket.Poll();
        WebSocketPeer.State readyState = _socket.GetReadyState();

        if (readyState == WebSocketPeer.State.Open)
        {
            if (!_wasOpen)
            {
                _wasOpen = true;
                SetState(_controllerConnected
                    ? ControllerLinkState.ControllerConnected
                    : ControllerLinkState.WaitingForController);
            }

            while (_socket.GetAvailablePacketCount() > 0)
            {
                byte[] packet = _socket.GetPacket();
                HandleMessage(packet.GetStringFromUtf8());
            }

            PumpContext(delta);
        }
        else if (readyState == WebSocketPeer.State.Closed)
        {
            DropSocket();
            if (Enabled) ScheduleReconnect();
        }
    }

    public override void _ExitTree()
    {
        DisconnectSocket();
        if (Instance == this) Instance = null;
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
        {
            if (enabled && _socket == null && !string.IsNullOrEmpty(RelayUrl)) Connect();
            return;
        }

        Enabled = enabled;
        SaveConfig();

        if (!enabled)
        {
            DisconnectSocket();
            SetState(ControllerLinkState.Disabled);
        }
        else if (string.IsNullOrEmpty(RelayUrl))
        {
            SetState(ControllerLinkState.NoRelay);
        }
        else
        {
            Restart();
        }

        EmitSignal(SignalName.StatusChanged);
    }

    public void SetRelayUrl(string url)
    {
        string normalized = NormalizeRelayUrl(url);
        if (normalized == RelayUrl)
        {
            EmitSignal(SignalName.StatusChanged);
            return;
        }

        RelayUrl = normalized;
        SaveConfig();

        if (!Enabled) SetState(ControllerLinkState.Disabled);
        else if (string.IsNullOrEmpty(RelayUrl)) SetState(ControllerLinkState.NoRelay);
        else Restart();

        EmitSignal(SignalName.StatusChanged);
    }

    public void NewRoomCode()
    {
        RoomCode = GenerateRoomCode();
        SaveConfig();

        if (Enabled && !string.IsNullOrEmpty(RelayUrl)) Restart();
        EmitSignal(SignalName.StatusChanged);
    }

    public void ApplyOverrides()
    {
        string envRelay = OS.GetEnvironment("ARES_RELAY_URL");
        if (!string.IsNullOrWhiteSpace(envRelay)) RelayUrl = NormalizeRelayUrl(envRelay);

        string envController = OS.GetEnvironment("ARES_CONTROLLER");
        if (!string.IsNullOrWhiteSpace(envController))
            Enabled = envController.Trim().ToLowerInvariant() is "1" or "true" or "on" or "yes";

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--relay=", StringComparison.Ordinal))
                RelayUrl = NormalizeRelayUrl(argument["--relay=".Length..]);
            else if (argument.StartsWith("--room=", StringComparison.Ordinal))
                RoomCode = NormalizeRoomCode(argument["--room=".Length..]) is { Length: > 0 } code ? code : RoomCode;
            else if (argument is "--no-controller" or "--controller-off")
                Enabled = false;
        }
    }

    private void StartConfigured()
    {
        // Autoloads start before the main scene exists. Waiting one frame lets us
        // keep test scenes from opening a real socket even outside --headless.
        bool testScene = GetTree().CurrentScene?.SceneFilePath.StartsWith("res://tests/", StringComparison.Ordinal) == true;
        if (!Enabled || testScene)
        {
            Enabled = false;
            SetState(ControllerLinkState.Disabled);
            return;
        }

        if (string.IsNullOrEmpty(RelayUrl))
        {
            SetState(ControllerLinkState.NoRelay);
            return;
        }

        Connect();
    }

    public static string GenerateRoomCode()
    {
        var random = new Random();
        Span<char> code = stackalloc char[RoomLength];
        for (int i = 0; i < code.Length; i++)
            code[i] = RoomAlphabet[random.Next(RoomAlphabet.Length)];
        return new string(code);
    }

    public static string NormalizeRoomCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string code = raw.Trim().ToUpperInvariant();
        if (code.Length is < 4 or > 8) return "";

        foreach (char character in code)
        {
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9') continue;
            return "";
        }

        return code;
    }

    public static string NormalizeRelayUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string value = raw.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "wss://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return "";

        string scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is "https") scheme = "wss";
        else if (scheme is "http") scheme = "ws";
        if (scheme is not ("ws" or "wss")) return "";
        if (string.IsNullOrEmpty(uri.Host)) return "";

        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
            path = path[..^"/index.html".Length];
        if (path == "/") path = "";
        if (path == "" && scheme == "wss") path = RelayEndpointPath;

        return $"{scheme}://{uri.Authority}{path}";
    }

    public static string ToJoinUrl(string relayUrl, string roomCode)
    {
        string normalized = NormalizeRelayUrl(relayUrl);
        string room = NormalizeRoomCode(roomCode);
        if (normalized == "" || room == "") return "";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri)) return "";

        string scheme = uri.Scheme == "wss" ? "https" : "http";
        return $"{scheme}://{uri.Authority}{JoinPath}?room={room}";
    }

    private void Connect()
    {
        _reconnectTimer = 0;
        _wasOpen = false;
        _controllerConnected = false;
        DisconnectSocket();

        if (!Enabled)
        {
            SetState(ControllerLinkState.Disabled);
            return;
        }

        if (string.IsNullOrEmpty(RelayUrl))
        {
            SetState(ControllerLinkState.NoRelay);
            return;
        }

        _socket = new WebSocketPeer();
        string url = $"{RelayUrl}?room={RoomCode}&role=game";
        Error error = _socket.ConnectToUrl(url);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Controller relay: could not connect to {url}: {error}");
            _socket = null;
            ScheduleReconnect();
            return;
        }

        SetState(ControllerLinkState.Connecting);
    }

    private void Restart()
    {
        DisconnectSocket();
        Connect();
    }

    private void ScheduleReconnect()
    {
        _reconnectTimer = ReconnectDelay;
        SetState(ControllerLinkState.Reconnecting);
    }

    private void DisconnectSocket()
    {
        _socket?.Close(1000, "Controller relay closed");
        _socket = null;
        _wasOpen = false;
        _reconnectTimer = 0;
        _controllerConnected = false;
        _bridge.ReleaseAll();
    }

    private void DropSocket()
    {
        _socket = null;
        _wasOpen = false;
        _controllerConnected = false;
        _bridge.ReleaseAll();
    }

    private void HandleMessage(string text)
    {
        ControllerMessage message = ControllerMessage.Parse(text);

        switch (message.Kind)
        {
            case ControllerMessageKind.Input:
                _bridge.Apply(message);
                MarkControllerConnected();
                break;

            case ControllerMessageKind.Command:
                MarkControllerConnected();
                if (!ControllerCommandRouter.Dispatch(message.Command))
                    GD.PushWarning($"Controller relay: no minigame handled '{message.Command}'.");
                break;

            case ControllerMessageKind.ControllerConnected:
                MarkControllerConnected();
                break;

            case ControllerMessageKind.ControllerDisconnected:
                _controllerConnected = false;
                _bridge.ReleaseAll();
                SetState(ControllerLinkState.WaitingForController);
                break;
        }
    }

    private void MarkControllerConnected()
    {
        if (_controllerConnected) return;
        _controllerConnected = true;
        // A phone that joins late has missed every earlier context update.
        _forceContext = true;
        SetState(ControllerLinkState.ControllerConnected);
    }

    /// <summary>Sends the active minigame context, throttled to a few updates a second.</summary>
    private void PumpContext(double delta)
    {
        if (!_controllerConnected || _socket == null) return;

        _contextTimer += delta;
        if (!_forceContext && _contextTimer < ContextInterval) return;
        if (!_forceContext && ControllerContext.Revision == _lastContextRevision) return;

        _contextTimer = 0;
        _forceContext = false;
        _lastContextRevision = ControllerContext.Revision;

        Error error = _socket.SendText(ControllerContext.BuildMessage());
        if (error != Error.Ok)
            GD.PushWarning($"Controller relay: could not send context: {error}");
    }

    private void SetState(ControllerLinkState state)
    {
        if (State == state) return;
        State = state;
        EmitSignal(SignalName.StatusChanged);
    }

    private void LoadConfig()
    {
        Enabled = ProjectSettings.GetSetting(EnabledSetting, false).AsBool();
        RelayUrl = NormalizeRelayUrl(ProjectSettings.GetSetting(RelaySetting, "").AsString());
        RoomCode = NormalizeRoomCode(ProjectSettings.GetSetting(RoomSetting, "").AsString());

        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            Enabled = config.GetValue(ConfigSection, "enabled", Enabled).AsBool();
            RelayUrl = NormalizeRelayUrl(config.GetValue(ConfigSection, "relay_url", RelayUrl).AsString());
            RoomCode = NormalizeRoomCode(config.GetValue(ConfigSection, "room_code", RoomCode).AsString());
        }

        if (string.IsNullOrEmpty(RoomCode)) RoomCode = GenerateRoomCode();
    }

    private void SaveConfig()
    {
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(ConfigSection, "enabled", Enabled);
        config.SetValue(ConfigSection, "relay_url", RelayUrl);
        config.SetValue(ConfigSection, "room_code", RoomCode);

        Error error = config.Save(ConfigPath);
        if (error != Error.Ok) GD.PushWarning($"Controller relay: could not save {ConfigPath}: {error}");
    }

    private static bool IsHeadless =>
        OS.HasFeature("headless") || DisplayServer.GetName() == "headless";

    private static bool HasNoControllerArgument()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
            if (argument is "--no-controller" or "--controller-off") return true;
        return false;
    }
}

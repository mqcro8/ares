using Godot;
using System;
using System.Collections.Generic;
using Ares.Systems.Controllers;

public partial class ControllerInputTest : Node
{
    private int _assertions;
    private readonly ControllerInputBridge _bridge = new();

    private sealed partial class InputCapture : Node
    {
        public readonly List<InputEvent> Events = new();

        public override void _UnhandledInput(InputEvent @event)
        {
            Events.Add(@event);
        }
    }

    private sealed class CommandCapture : IControllerCommandSink
    {
        public readonly List<ControllerCommand> Commands = new();
        public bool Handled;

        public bool HandleControllerCommand(ControllerCommand command)
        {
            Commands.Add(command);
            return Handled;
        }
    }

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"CONTROLLER INPUT PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            _bridge.ReleaseAll();
            Input.FlushBufferedEvents();
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Run()
    {
        MessageParsing();
        CommandParsing();
        CommandValidation();
        CommandRouting();
        ContextPublishing();
        RoomCodes();
        RelayUrls();
        BridgeInjection();
        QrCodes();
    }

    private void MessageParsing()
    {
        var legacy = ControllerMessage.Parse("{\"action\":\"move_left\",\"strength\":0.5}");
        Check(legacy.IsInput && legacy.Action == "move_left" && Mathf.IsEqualApprox(legacy.Strength, 0.5f),
            "Legacy {action,strength} payloads parse");

        var typed = ControllerMessage.Parse("{\"type\":\"input\",\"action\":\"training_jump\",\"strength\":1}");
        Check(typed.IsInput && typed.Action == "training_jump" && Mathf.IsEqualApprox(typed.Strength, 1f),
            "Typed relay payloads parse");

        var interact = ControllerMessage.Parse("{\"action\":\"interact\",\"strength\":1}");
        Check(interact.IsInput && interact.Action == "interact" && interact.Pressed,
            "interact is a supported action");

        var noStrength = ControllerMessage.Parse("{\"action\":\"hotbar_3\"}");
        Check(noStrength.IsInput && Mathf.IsEqualApprox(noStrength.Strength, 1f),
            "Missing strength defaults to fully pressed");

        Check(ControllerMessage.Parse("{\"type\":\"controller_connected\"}").Kind == ControllerMessageKind.ControllerConnected,
            "Controller-connected messages parse");
        Check(ControllerMessage.Parse("{\"type\":\"controller_disconnected\"}").Kind == ControllerMessageKind.ControllerDisconnected,
            "Controller-disconnected messages parse");
        Check(ControllerMessage.Parse("{\"type\":\"game_connected\"}").Kind == ControllerMessageKind.GameConnected,
            "Game-connected messages parse");
        Check(ControllerMessage.Parse("{\"type\":\"room_ready\"}").Kind == ControllerMessageKind.RoomReady,
            "Room-ready messages parse");

        Check(!ControllerMessage.Parse("not json").IsInput, "Malformed JSON is rejected");
        Check(!ControllerMessage.Parse("[]").IsInput, "Non-object JSON is rejected");
        Check(!ControllerMessage.Parse("{\"action\":\"pause\",\"strength\":1}").IsInput, "Unknown actions are rejected");
        Check(!ControllerMessage.Parse("{\"action\":\"move_left\",\"strength\":1.5}").IsInput, "Strength above 1 is rejected");
        Check(!ControllerMessage.Parse("{\"action\":\"move_left\",\"strength\":-0.1}").IsInput, "Negative strength is rejected");
        Check(!ControllerMessage.Parse("{\"action\":\"move_left\",\"strength\":\"1\"}").IsInput, "String strength is rejected");
        Check(ControllerMessage.Parse("{\"type\":\"input\",\"action\":\"move_left\",\"strength\":0.5,\"extra\":1}").IsInput,
            "Unknown extra fields are tolerated");
    }

    private void CommandParsing()
    {
        var pick = ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"wire_pick\",\"index\":2}");
        Check(pick.IsCommand && pick.Command.Name == "wire_pick" && pick.Command.HasIndex &&
            pick.Command.Index == 2 && !pick.Command.HasValue, "Index commands parse");

        var set = ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"freq_set\",\"index\":1,\"value\":0.42}");
        Check(set.IsCommand && set.Command.HasIndex && set.Command.Index == 1 &&
            set.Command.HasValue && Mathf.IsEqualApprox(set.Command.Value, 0.42f),
            "Command values parse");

        var select = ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"hull_select\",\"index\":3}");
        Check(select.IsCommand && select.Command.HasIndex && select.Command.Index == 3 &&
            !select.Command.HasValue, "Hull plate selection parses");

        var restart = ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"hull_restart\"}");
        Check(restart.IsCommand && !restart.Command.HasIndex && !restart.Command.HasValue,
            "No-argument commands parse");

        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"pause\"}").IsCommand,
            "Unknown command names are rejected");
        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"fuse_press\",\"index\":8}").IsCommand,
            "Out-of-range command indexes are rejected");
        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"wire_pick\",\"index\":1.5}").IsCommand,
            "Fractional command indexes are rejected");
        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"freq_set\",\"index\":0,\"value\":2}").IsCommand,
            "Out-of-range command values are rejected");
        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"name\":\"wire_pick\",\"value\":1}").IsCommand,
            "Unexpected command values are rejected");
        Check(!ControllerMessage.Parse("{\"type\":\"command\",\"index\":0}").IsCommand,
            "Commands without a name are rejected");
    }

    private void CommandValidation()
    {
        ControllerCommand Make(string name, int index, bool hasIndex, float value, bool hasValue) =>
            new(name, index, hasIndex, value, hasValue);

        Check(ControllerCommands.Validate(Make("wire_pick", 0, true, 0, false), out _),
            "wire_pick accepts index 0");
        Check(!ControllerCommands.Validate(Make("wire_pick", -1, true, 0, false), out _),
            "wire_pick rejects negative indexes");
        Check(!ControllerCommands.Validate(Make("freq_set", 3, true, 0.5f, true), out _),
            "freq_set rejects knob indexes above 2");
        Check(!ControllerCommands.Validate(Make("freq_set", 0, true, float.NaN, true), out _),
            "freq_set rejects non-finite values");
        Check(!ControllerCommands.Validate(Make("hull_select", 0, true, 1f, true), out _),
            "hull_select rejects a value");
        Check(!ControllerCommands.Validate(Make("hull_select", 4, true, 0, false), out _),
            "hull_select rejects plates above 3");
        Check(ControllerCommands.Validate(Make("hull_select", 3, true, 0, false), out _),
            "hull_select accepts plate 3");
        Check(ControllerCommands.Validate(Make("launch_retry", 0, false, 0, false), out _),
            "launch_retry takes no arguments");
        Check(ControllerCommands.Validate(Make("ending_skip", 0, false, 0, false), out _) &&
            ControllerCommands.Validate(Make("ending_pause", 0, false, 0, false), out _) &&
            ControllerCommands.Validate(Make("ending_continue", 0, false, 0, false), out _),
            "Ending controls take no arguments");

        Check(ControllerActions.IsSupported(ControllerActions.MinigameAction),
            "minigame_action is a supported held action");
        Check(ControllerActions.IsSupported(ControllerActions.RotateLeft),
            "rotate actions are supported");
        Check(ControllerActions.IsSupported(ControllerActions.LaunchHold),
            "launch_hold is a supported held action");
    }

    private void CommandRouting()
    {
        ControllerCommandRouter.Clear();
        var first = new CommandCapture { Handled = false };
        var second = new CommandCapture { Handled = true };
        var owner = new object();
        ControllerCommandRouter.Register(owner, first);
        ControllerCommandRouter.Register(new object(), second);

        var command = new ControllerCommand("fuse_press", 1, true, 0, false);
        Check(ControllerCommandRouter.Dispatch(command), "A later sink can handle a command");
        Check(first.Commands.Count == 1 && second.Commands.Count == 1,
            "Every sink is tried until one handles the command");
        Check(first.Commands[0].Name == "fuse_press" && first.Commands[0].Index == 1,
            "Dispatched commands keep their payload");

        ControllerCommandRouter.Unregister(owner);
        Check(ControllerCommandRouter.Count == 1, "Unregister removes a sink");
        ControllerCommandRouter.Clear();
        Check(!ControllerCommandRouter.Dispatch(command), "Dispatch with no sinks fails cleanly");
    }

    private void ContextPublishing()
    {
        ControllerContext.Reset();
        var owner = new object();
        var stale = new object();

        ControllerContext.Begin(owner, ControllerContextMode.Wires);
        Check(ControllerContext.IsActive && ControllerContext.Mode == ControllerContextMode.Wires,
            "Begin activates the context");

        var state = new Godot.Collections.Dictionary { ["selected"] = 2 };
        ControllerContext.Update(owner, state);
        ControllerContext.Set(owner, "connected", 1);
        ulong revision = ControllerContext.Revision;

        var json = new Json();
        Check(json.Parse(ControllerContext.BuildMessage()) == Error.Ok, "Context builds valid JSON");
        var data = json.Data.AsGodotDictionary();
        Check(data["type"].AsString() == "context" && data["mode"].AsString() == "wires" &&
            data["active"].AsBool(), "Context message carries mode and active flag");
        var published = data["state"].AsGodotDictionary();
        Check(published["selected"].AsInt32() == 2 && published["connected"].AsInt32() == 1,
            "Context message carries published state");

        ControllerContext.Update(stale, new Godot.Collections.Dictionary { ["hijack"] = 1 });
        Check(ControllerContext.Revision == revision, "A stale owner cannot update the context");

        ControllerContext.End(stale);
        Check(ControllerContext.IsActive, "A stale owner cannot end the context");

        ControllerContext.Remove(owner, "connected");
        Check(ControllerContext.Revision > revision, "Removing a key bumps the revision");

        ControllerContext.End(owner);
        Check(!ControllerContext.IsActive, "End clears the context");
        json.Parse(ControllerContext.BuildMessage());
        data = json.Data.AsGodotDictionary();
        Check(data["mode"].AsString() == "none" && !data["active"].AsBool(),
            "Inactive context tells the phone to restore its previous view");
		ControllerContext.Begin(owner, ControllerContextMode.Ending);
		Check(ControllerContext.ModeId(ControllerContext.Mode) == "ending",
			"Ending context selects the cinematic phone view");
		ControllerContext.Reset();
    }

    private void RoomCodes()
    {
        Check(ControllerRelay.NormalizeRoomCode(" abcd ") == "ABCD", "Room codes are trimmed and uppercased");
        Check(ControllerRelay.NormalizeRoomCode("AB12") == "AB12", "Short rooms of four characters are valid");
        Check(ControllerRelay.NormalizeRoomCode("AB12CD78") == "AB12CD78", "Rooms up to eight characters are valid");
        Check(ControllerRelay.NormalizeRoomCode("ABC") == "", "Three-character rooms are rejected");
        Check(ControllerRelay.NormalizeRoomCode("AB12CD789") == "", "Nine-character rooms are rejected");
        Check(ControllerRelay.NormalizeRoomCode("AB-12") == "", "Punctuation in room codes is rejected");

        for (int i = 0; i < 64; i++)
        {
            string code = ControllerRelay.GenerateRoomCode();
            Check(code.Length == 5, "Generated room codes are five characters");
            Check(ControllerRelay.NormalizeRoomCode(code) == code, "Generated room codes are relay-safe");
        }
    }

    private void RelayUrls()
    {
        Check(ControllerRelay.NormalizeRelayUrl("example.ngrok.app") == "wss://example.ngrok.app",
            "Bare hosts default to wss");
        Check(ControllerRelay.NormalizeRelayUrl("https://example.ngrok.app/") == "wss://example.ngrok.app",
            "HTTPS relay URLs become wss");
        Check(ControllerRelay.NormalizeRelayUrl("http://localhost:8000") == "ws://localhost:8000",
            "HTTP relay URLs become ws");
        Check(ControllerRelay.NormalizeRelayUrl("wss://host/path/") == "wss://host/path",
            "Trailing slashes are removed");
        Check(ControllerRelay.NormalizeRelayUrl("wss://host/index.html") == "wss://host",
            "index.html is stripped");
        Check(ControllerRelay.NormalizeRelayUrl("ftp://host") == "", "Non-WebSocket schemes are rejected");
        Check(ControllerRelay.NormalizeRelayUrl("") == "", "Empty relay URLs stay empty");

        Check(ControllerRelay.ToJoinUrl("wss://example.ngrok.app", "ABCDE") == "https://example.ngrok.app/?room=ABCDE",
            "WSS relays produce HTTPS join links");
        Check(ControllerRelay.ToJoinUrl("ws://localhost:8000", "ABCDE") == "http://localhost:8000/?room=ABCDE",
            "WS relays produce HTTP join links");
        Check(ControllerRelay.ToJoinUrl("", "ABCDE") == "", "Join links need a relay URL");
    }

    private void BridgeInjection()
    {
        var legacy = ControllerMessage.Parse("{\"action\":\"move_left\",\"strength\":0.5}");
        Check(_bridge.Apply(legacy), "Bridge applies a parsed message");
        Input.FlushBufferedEvents();
        Check(Input.IsActionPressed("move_left"), "move_left reaches the input singleton");
        Check(_bridge.ActiveActionCount == 1, "Bridge tracks the held action");
        Check(!_bridge.Apply(legacy), "Bridge skips duplicate strengths");

        Check(_bridge.Apply("move_left", 0f), "Bridge releases held actions");
        Input.FlushBufferedEvents();
        Check(!Input.IsActionPressed("move_left"), "move_left release reaches the input singleton");

        Check(_bridge.Apply(ControllerMessage.Parse("{\"action\":\"interact\",\"strength\":1}")),
            "Bridge applies interact");
        Input.FlushBufferedEvents();
        Check(Input.IsKeyPressed(Key.E) || Input.IsPhysicalKeyPressed(Key.E),
            "interact presses the raw E key");
        _bridge.Apply("interact", 0f);
        Input.FlushBufferedEvents();
        Check(!Input.IsKeyPressed(Key.E) && !Input.IsPhysicalKeyPressed(Key.E),
            "interact releases the raw E key");

        var capture = new InputCapture();
        AddChild(capture);
        _bridge.Apply("hotbar_2", 1f);
        Input.FlushBufferedEvents();
        Check(capture.Events.Exists(@event =>
                @event is InputEventAction { Pressed: true } action && action.Action == "hotbar_2"),
            "Action events reach _UnhandledInput so Hotbar can consume them");

        _bridge.Apply("move_left", 1f);
        _bridge.Apply("move_right", 0.4f);
        Input.FlushBufferedEvents();
        _bridge.ReleaseAll();
        Input.FlushBufferedEvents();
        Check(!Input.IsActionPressed("move_left") && !Input.IsActionPressed("move_right"),
            "ReleaseAll clears every held action");
        Check(_bridge.ActiveActionCount == 0, "ReleaseAll empties the tracked action set");

        capture.QueueFree();
    }

    private void QrCodes()
    {
        byte[] png = ControllerQr.CreatePng("https://example.ngrok.app/?room=ABCDE", 4, 2);
        Check(png is { Length: > 100 }, "QR code generation returns a PNG");
        Check(png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47,
            "QR output has a PNG signature");
        Check(ControllerQr.CreatePng("") == null, "Empty QR text produces no image");
    }
}

using Godot;
using System.Collections.Generic;
using System.Linq;
using ares.scripts.training;
using ares.scripts.mission;
using ares.scripts.systems.items;
using Ares.Systems.Controllers;
using Ares.Systems.Audio;

public partial class FuseBoxMinigame : Control, IControllerCommandSink, IControllerContextSource
{
	[Signal] public delegate void CompletedEventHandler();

	[Export] public Color OnColor { get; set; } = new Color("3cdc50");
	[Export] public Color OffColor { get; set; } = new Color("d62828");
	[Export] public Color LightOn { get; set; } = new Color("f5c542");
	[Export] public Color LightOff { get; set; } = new Color("4a4220");

	private Panel _box;
	private Node2D _effects;
	private readonly List<Panel> _switches = new();
	private readonly List<Panel> _lights = new();

	private bool[] _state;
	private bool[] _initial;
	private int _double;      // switch that toggles two fuses
	private int _partner;     // the linked switch
	private int _maxPresses;
	private int _pressesLeft;
	private bool _busy;
	private Timer _pending;
	private Label _status;

	public override void _Ready()
	{
		_box = GetNode<Panel>("Box");
		_effects = GetNode<Node2D>("Effects");

		foreach (var c in GetNode("Box/TopRow").GetChildren())
			if (c is Panel p) _switches.Add(PreparePanel(p));
		foreach (var c in GetNode("Box/BottomRow").GetChildren())
			if (c is Panel p) _switches.Add(PreparePanel(p));
		var lightsRow = GetNode<HBoxContainer>("Box/Lights");
		foreach (var c in lightsRow.GetChildren())
			if (c is Panel p) _lights.Add(PreparePanel(p));

		if (MissionState.Instance?.HasEquipped(ToolEffects.SpareFuses) == true)
		{
			var extra = (Panel)_lights[0].Duplicate();
			extra.Name = "L5";
			lightsRow.AddChild(extra);
			_lights.Add(PreparePanel(extra));
			lightsRow.AddThemeConstantOverride("separation", 12);
		}
		_maxPresses = ToolEffects.FusePressesFor(MissionState.Instance?.HasEquipped(ToolEffects.SpareFuses) == true);
		_pending = new Timer { OneShot = true };
		AddChild(_pending);
		_pending.Timeout += ResolvePending;
		_status = new Label { Position = new Vector2(12, 92) };
		AddChild(_status);
		GeneratePuzzle();
		Refresh();
		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Fuses);
		PublishControllerContext();
		PracticeReturn.Attach(this);
	}

	// Duplicate each switch/light style so its color can be changed independently.
	private Panel PreparePanel(Panel p)
	{
		if (p.GetThemeStylebox("panel") is StyleBoxFlat sb)
			p.AddThemeStyleboxOverride("panel", (StyleBoxFlat)sb.Duplicate());
		return p;
	}

	// Generate a solvable puzzle with exactly one switch initially on.
	private void GeneratePuzzle()
	{
		int n = _switches.Count;
		_state = new bool[n];

		_double = GD.RandRange(0, n - 1);
		do { _partner = GD.RandRange(0, n - 1); } while (_partner == _double);

		// The initially-on switch is neither the double switch nor its partner.
		// Pressing the other four switches lets the double switch turn on its partner.
		int on;
		do { on = GD.RandRange(0, n - 1); } while (on == _double || on == _partner);
		_state[on] = true;

		_initial = (bool[])_state.Clone();
		_pressesLeft = _maxPresses;
	}

	// Toggle the selected switch and its partner when the double switch is selected.
	private void Toggle(int i)
	{
		_state[i] = !_state[i];
		if (i == _double)
			_state[_partner] = !_state[_partner];
	}

	private void Refresh()
	{
		for (int i = 0; i < _switches.Count; i++)
		{
			SetPanelColor(_switches[i], _state[i] ? OnColor : OffColor);
			var label = _switches[i].GetNodeOrNull<Label>("PracticeNumber");
			if (label == null)
			{
				label = new Label { Name = "PracticeNumber", Position = new Vector2(0, 36), MouseFilter = MouseFilterEnum.Ignore };
				label.AddThemeColorOverride("font_outline_color", Colors.Black);
				label.AddThemeConstantOverride("outline_size", 5);
				_switches[i].AddChild(label);
			}
			label.Text = $"{i + 1}: {(_state[i] ? "ON" : "OFF")}";
		}
		for (int i = 0; i < _lights.Count; i++)
			SetPanelColor(_lights[i], i < _pressesLeft ? LightOn : LightOff);
		_status.Text = $"Presses: {_pressesLeft}/{_maxPresses}";
		PublishControllerContext();
	}

	private void SetPanelColor(Panel p, Color color)
	{
		if (p.GetThemeStylebox("panel") is StyleBoxFlat sb)
			sb.BgColor = color;
	}

	public override void _Input(InputEvent @event)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this) || _busy) return;
		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			int index = (int)key.Keycode - (int)Key.Key1;
			if (index >= 0 && index < _switches.Count)
			{
				Press(index);
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		// Handle switch clicks.
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			for (int i = 0; i < _switches.Count; i++)
			{
				if (_switches[i].GetGlobalRect().HasPoint(mb.GlobalPosition))
				{
					Press(i);
					break;
				}
			}
		}
	}

	private void Press(int i)
	{
		if (_busy || i < 0 || i >= _switches.Count) return;

		Toggle(i);
		AudioManager.PlaySfx(SoundIds.Enter, -7f, 0.9f + i * 0.035f);

		// Spend one press.
		_pressesLeft--;
		Refresh();

		Sparks(_switches[i], _state[i] ? OnColor : OffColor);
		if (i == _double)
			Sparks(_switches[_partner], _state[_partner] ? OnColor : OffColor);

		// Check whether all switches are on.
		if (_state.All(s => s))
		{
			AudioManager.PlaySfx(SoundIds.RoverPowerUp, -8f);
			// Emit completed after the success animation.
			_busy = true;
			foreach (var sw in _switches) Sparks(sw, OnColor);
			_status.Text = "All switches ON!";
			_pending.Start(0.6);
		}
		// Check whether any presses remain.
		else if (_pressesLeft <= 0)
		{
			// Reset the box after the failure message.
			_busy = true;
			_status.Text = "No presses left. Resetting the box…";
			_pending.Start(0.5);
		}
	}

	private void ResolvePending()
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this)) return;
		if (_state.All(s => s)) EmitSignal(SignalName.Completed);
		else ResetBox();
		PublishControllerContext();
	}

	public void StopPending() { _pending.Stop(); }
	public override void _ExitTree()
	{
		_pending.Stop();
		_pending.Timeout -= ResolvePending;
		ControllerCommandRouter.Unregister(this);
		ControllerContext.End(this);
	}

	public bool HandleControllerCommand(ControllerCommand command)
	{
		switch (command.Name)
		{
			case ControllerCommands.MinigameStart:
			case ControllerCommands.MinigameCancel:
				return false;

			case ControllerCommands.FusePress:
				if (GameplayBlocked) return true;
				Press(command.Index);
				return true;

			default:
				return false;
		}
	}

	public void PublishControllerContext()
	{
		var switches = new Godot.Collections.Array();
		foreach (bool on in _state) switches.Add(on);

		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["switches"] = switches,
			["presses"] = _pressesLeft,
			["maxPresses"] = _maxPresses,
			["double"] = _double,
			["partner"] = _partner,
			["busy"] = _busy,
			["overlay"] = OverlayState,
		});
	}

	private bool GameplayBlocked => GetNodeOrNull<PracticeReturn>("PracticeReturn")?.BlocksGameplay ?? false;

	private string OverlayState =>
		GetNodeOrNull<PracticeReturn>("PracticeReturn")?.ControllerOverlay ?? "active";

	private void ResetBox()
	{
		_state = (bool[])_initial.Clone();
		_pressesLeft = _maxPresses;
		Refresh();
		Shake();
		_busy = false;
	}

	private void Shake()
	{
		float x = _box.Position.X;
		var tw = CreateTween();
		tw.TweenProperty(_box, "position:x", x + 8, 0.05);
		tw.TweenProperty(_box, "position:x", x - 8, 0.05);
		tw.TweenProperty(_box, "position:x", x + 4, 0.05);
		tw.TweenProperty(_box, "position:x", x, 0.05);
	}

	private void Sparks(Control t, Color color)
	{
		MinigameEffects.Sparks(_effects, _effects.ToLocal(t.GetGlobalRect().GetCenter()), color, 14, .4f, 180);
	}
}

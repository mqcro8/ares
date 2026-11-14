using Godot;
using System.Collections.Generic;
using System.Linq;
using ares.scripts.training;
using Ares.Systems.Controllers;

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
	private int _double;      // interruptor que cambia dos a la vez
	private int _partner;     // el que cambia junto con él
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
		foreach (var c in GetNode("Box/Lights").GetChildren())
			if (c is Panel p) _lights.Add(PreparePanel(p));

		_maxPresses = _lights.Count;
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

	// Duplica el estilo de cada interruptor/luz para poder cambiar su color por separado
	private Panel PreparePanel(Panel p)
	{
		if (p.GetThemeStylebox("panel") is StyleBoxFlat sb)
			p.AddThemeStyleboxOverride("panel", (StyleBoxFlat)sb.Duplicate());
		return p;
	}

	// Bloque 2: generar puzzle con un solo interruptor encendido (siempre tiene solución)
	private void GeneratePuzzle()
	{
		int n = _switches.Count;
		_state = new bool[n];

		_double = GD.RandRange(0, n - 1);
		do { _partner = GD.RandRange(0, n - 1); } while (_partner == _double);

		// El único encendido no es el doble ni su pareja: la solución es pulsar
		// los otros 4, de modo que el doble encienda también a su pareja.
		int on;
		do { on = GD.RandRange(0, n - 1); } while (on == _double || on == _partner);
		_state[on] = true;

		_initial = (bool[])_state.Clone();
		_pressesLeft = _maxPresses;
	}

	// Bloque 4: alternar (el interruptor doble cambia también a su pareja)
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
		_status.Text = $"Presses: {_pressesLeft}/{_maxPresses}\nDOUBLE: {_double + 1} → also toggles {_partner + 1}";
		PublishControllerContext();
	}

	private void SetPanelColor(Panel p, Color color)
	{
		if (p.GetThemeStylebox("panel") is StyleBoxFlat sb)
			sb.BgColor = color;
	}

	public override void _Input(InputEvent @event)
	{
		if (_busy) return;
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

		// Bloque 3: clic en interruptor
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

		// Bloque 5: gastar un intento
		_pressesLeft--;
		Refresh();

		Sparks(_switches[i], _state[i] ? OnColor : OffColor);
		if (i == _double)
			Sparks(_switches[_partner], _state[_partner] ? OnColor : OffColor);

		// Bloque 6: ¿todos encendidos?
		if (_state.All(s => s))
		{
			// Bloque 7a: emitir completed
			_busy = true;
			foreach (var sw in _switches) Sparks(sw, OnColor);
			_status.Text = "All switches ON!";
			_pending.Start(0.6);
		}
		// Bloque 7b: ¿quedan intentos?
		else if (_pressesLeft <= 0)
		{
			// Bloque 8: reiniciar caja
			_busy = true;
			_status.Text = "No presses left. Resetting the box…";
			_pending.Start(0.5);
		}
	}

	private void ResolvePending()
	{
		if (_state.All(s => s)) EmitSignal(SignalName.Completed);
		else ResetBox();
		PublishControllerContext();
	}

	public void StopPending() { _busy = true; _pending.Stop(); }
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
		var p = new CpuParticles2D
		{
			Position = _effects.ToLocal(t.GetGlobalRect().GetCenter()),
			OneShot = true,
			Emitting = true,
			Amount = 14,
			Lifetime = 0.4f,
			Explosiveness = 1f,
			Direction = Vector2.Zero,
			Spread = 180f,
			InitialVelocityMin = 80f,
			InitialVelocityMax = 180f,
			Gravity = Vector2.Zero,
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			Color = color.Lightened(0.5f)
		};
		_effects.AddChild(p);
		var cleanup = new Timer { OneShot = true, WaitTime = 1.0 };
		p.AddChild(cleanup);
		cleanup.Timeout += p.QueueFree;
		cleanup.Start();
	}
}

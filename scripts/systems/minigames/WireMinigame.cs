using Godot;
using System.Collections.Generic;
using System.Linq;
using ares.scripts.training;
using ares.scripts.mission;
using ares.scripts.systems.items;
using Ares.Systems.Controllers;
using Ares.Systems.Audio;

public partial class WireMinigame : Control, IControllerCommandSink, IControllerContextSource
{
	[Signal] public delegate void CompletedEventHandler();
	private readonly FeedbackSounds _feedbackSounds = new();

	[Export] public Godot.Collections.Array<Color> WireColors { get; set; } = new()
	{
		Colors.Red, Colors.Blue, Colors.Yellow, Colors.Magenta
	};
	[Export] public float WireWidth { get; set; } = 8f;

	private VBoxContainer _leftBox;
	private VBoxContainer _rightBox;
	private Node2D _wires;
	private Node2D _effects;
	private Panel _frame;
	private float _frameBaseX;
	private Tween _shakeTween;

	private readonly List<ColorRect> _leftTerminals = new();
	private readonly List<ColorRect> _rightTerminals = new();
	private readonly HashSet<ColorRect> _connected = new();
	private int _connectedCount;
	public int ConnectedCount => _connectedCount;

	private Line2D _dragWire;
	private Line2D _dragGlow;
	private ColorRect _dragFrom;
	private ColorRect _hover;
	private int _keyboardTarget;
	private int _selectedLeft = -1;
	private bool _finished;

	private readonly List<(Line2D Wire, Line2D Glow, ColorRect From, ColorRect To)> _links = new();

	// Bloque 2: asignar colores
	public override void _Ready()
	{
		_leftBox = GetNode<VBoxContainer>("LeftTerminals");
		_rightBox = GetNode<VBoxContainer>("RightTerminals");
		_wires = GetNode<Node2D>("Wires");
		_effects = GetNode<Node2D>("Effects");
		_frame = GetNode<Panel>("Frame");
		_frameBaseX = _frame.Position.X;

		var right = new List<Color>(WireColors);
		for (int i = right.Count - 1; i > 0; i--)
		{
			int j = (int)(GD.Randi() % (uint)(i + 1));
			(right[i], right[j]) = (right[j], right[i]);
		}
		// Evita que quede idéntico a la izquierda
		if (right.SequenceEqual(WireColors))
		{
			right.Add(right[0]);
			right.RemoveAt(0);
		}

		for (int i = 0; i < WireColors.Count; i++)
		{
			var left = _leftBox.GetChild<ColorRect>(i);
			var rightTerminal = _rightBox.GetChild<ColorRect>(i);
			left.Color = WireColors[i];
			rightTerminal.Color = right[i];
			_leftTerminals.Add(left);
			_rightTerminals.Add(rightTerminal);
		}
		if (MissionState.Instance?.HasEquipped(ToolEffects.WirePliers) == true)
			PreconnectOneWire();
		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Wires);
		PublishControllerContext();
		PracticeReturn.Attach(this);
	}

	public override void _Input(InputEvent @event)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this)) return;
		if (_finished) return;
		if (@event is InputEventKey { Pressed: true, Echo: false } key)
		{
			int index = (int)key.Keycode - (int)Key.Key1;
			if (index >= 0 && index < WireColors.Count)
			{
				var terminal = _leftBox.GetChild<ColorRect>(index);
				if (!_connected.Contains(terminal)) { StartDrag(terminal); AimKeyboard(); }
			}
			else if (_dragWire != null && (key.Keycode == Key.Up || key.Keycode == Key.Down))
			{
				_keyboardTarget = (_keyboardTarget + (key.Keycode == Key.Up ? WireColors.Count - 1 : 1)) % WireColors.Count;
				AimKeyboard();
			}
			else if (_dragWire != null && key.Keycode == Key.Enter)
				EndDrag(_rightBox.GetChild<ColorRect>(_keyboardTarget).GetGlobalRect().GetCenter());
			else return;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				// Bloque 3: presionar terminal izquierda libre
				var t = TerminalAt(_leftBox, mb.GlobalPosition);
				if (t != null && !_connected.Contains(t))
					StartDrag(t);
			}
			else if (_dragWire != null)
			{
				// Bloque 6: soltar
				EndDrag(mb.GlobalPosition);
			}
		}
		else if (@event is InputEventMouseMotion mm)
		{
			if (_dragWire != null)
			{
				// Bloque 5: arrastrar
				var point = _wires.ToLocal(mm.GlobalPosition);
				_dragWire.SetPointPosition(1, point);
				if (_dragGlow != null)
					_dragGlow.SetPointPosition(1, point);
				UpdateDragHighlights();
				UpdateDragCursor(mm.GlobalPosition);
			}
			else
			{
				UpdateHover(mm.GlobalPosition);
			}
		}
	}

	private ColorRect TerminalAt(Control box, Vector2 pos)
	{
		foreach (var child in box.GetChildren())
		{
			if (child is ColorRect t && t.GetGlobalRect().HasPoint(pos))
				return t;
		}
		return null;
	}

	private Vector2 Center(ColorRect t)
		=> _wires.ToLocal(t.GetGlobalRect().GetCenter());

	private void PreconnectOneWire()
	{
		if (!IsInsideTree() || _leftTerminals.Count == 0 || _rightTerminals.Count == 0) return;
		ColorRect from = _leftTerminals[0];
		ColorRect to = _rightTerminals.FirstOrDefault(terminal => terminal.Color == from.Color);
		if (to == null || _connected.Contains(from) || _connected.Contains(to)) return;

		var wire = new Line2D
		{
			Width = WireWidth,
			DefaultColor = from.Color,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Antialiased = true
		};
		wire.AddPoint(Vector2.Zero);
		wire.AddPoint(Vector2.Zero);
		_wires.AddChild(wire);
		_links.Add((wire, null, from, to));
		_connected.Add(from);
		_connected.Add(to);
		_connectedCount++;
		Callable.From(RefreshWireLayout).CallDeferred();
		PublishControllerContext();
	}

	// Bloque 4: crear cable
	private void StartDrag(ColorRect t)
	{
		_feedbackSounds.Play(SoundIds.EquipmentTake);
		_dragWire?.QueueFree();
		_dragGlow?.QueueFree();
		_dragFrom = t;
		_selectedLeft = _leftTerminals.IndexOf(t);
		var start = Center(t);

		// Halo suave detrás del cable para darle profundidad
		_dragGlow = new Line2D
		{
			Width = WireWidth * 3f,
			DefaultColor = new Color(t.Color.R, t.Color.G, t.Color.B, 0.22f),
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Antialiased = true
		};
		_dragGlow.AddPoint(start);
		_dragGlow.AddPoint(start);
		_wires.AddChild(_dragGlow);

		_dragWire = new Line2D
		{
			Width = WireWidth,
			DefaultColor = t.Color,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Antialiased = true
		};
		_dragWire.AddPoint(start);
		_dragWire.AddPoint(start);
		_wires.AddChild(_dragWire);

		Input.SetDefaultCursorShape(Input.CursorShape.Drag);
		UpdateDragHighlights();
		PublishControllerContext();
	}

	public void CancelInteraction()
	{
		_dragWire?.QueueFree();
		_dragGlow?.QueueFree();
		_dragWire = null;
		_dragGlow = null;
		_dragFrom = null;
		_selectedLeft = -1;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
		ResetHighlights();
		PublishControllerContext();
	}

	private void AimKeyboard()
	{
		if (_dragWire == null) return;
		var end = Center(_rightBox.GetChild<ColorRect>(_keyboardTarget));
		_dragWire.SetPointPosition(1, end);
		if (_dragGlow != null)
			_dragGlow.SetPointPosition(1, end);
		UpdateDragHighlights();
	}

	public void RefreshWireLayout()
	{
		foreach (var link in _links)
		{
			link.Wire.SetPointPosition(0, Center(link.From));
			link.Wire.SetPointPosition(1, Center(link.To));
			if (link.Glow != null)
			{
				link.Glow.SetPointPosition(0, Center(link.From));
				link.Glow.SetPointPosition(1, Center(link.To));
			}
		}
		if (_dragWire != null) _dragWire.SetPointPosition(0, Center(_dragFrom));
		if (_dragGlow != null) _dragGlow.SetPointPosition(0, Center(_dragFrom));
	}

	private void EndDrag(Vector2 pos) => EndDragAt(TerminalAt(_rightBox, pos));

	private void EndDragAt(ColorRect target)
	{
		var from = _dragFrom;
		var wire = _dragWire;
		var glow = _dragGlow;
		// Bloque 7: ¿mismo color y libre?
		bool success = from != null && target != null
			&& !_connected.Contains(target) && target.Color == from.Color;

		if (success)
		{
			_feedbackSounds.Play(SoundIds.ConnectorSnap);
			// Bloque 8a: fijar cable
			var end = Center(target);
			wire.SetPointPosition(1, end);
			if (glow != null)
				glow.SetPointPosition(1, end);

			_links.Add((wire, glow, from, target));
			_connected.Add(from);
			_connected.Add(target);
			_connectedCount++;

			Sparks(from, from.Color);
			Sparks(target, target.Color);

			// Bloques 9 y 10: ¿ya son cuatro? -> Completed
			if (_connectedCount == WireColors.Count)
			{
				_finished = true;
				ResetHighlights();
			}
		}
		else
		{
			if (target != null) _feedbackSounds.Play(SoundIds.ElectricalFault, 300);
			// Bloque 8b: borrar cable
			wire?.QueueFree();
			glow?.QueueFree();
			if (target != null)
				ShakeFrame();
		}

		_dragWire = null;
		_dragGlow = null;
		_dragFrom = null;
		_selectedLeft = -1;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
		ResetHighlights();
		if (_finished)
		{
			_feedbackSounds.Play(SoundIds.ObjectiveComplete);
			EmitSignal(SignalName.Completed);
		}
		PublishControllerContext();
	}

	private void UpdateHover(Vector2 pos)
	{
		var t = TerminalAt(_leftBox, pos);
		bool valid = t != null && !_connected.Contains(t);

		if (_hover != t)
		{
			if (_hover != null && !_connected.Contains(_hover))
				_hover.Modulate = Colors.White;
			_hover = valid ? t : null;
			if (_hover != null && !_connected.Contains(_hover))
				_hover.Modulate = new Color(1.35f, 1.35f, 1.35f, 1f);
		}

		Input.SetDefaultCursorShape(valid ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
	}

	private void UpdateDragHighlights()
	{
		if (_dragFrom == null)
			return;

		foreach (var t in _leftTerminals)
		{
			if (t == _dragFrom)
				t.Modulate = new Color(1.5f, 1.5f, 1.5f, 1f);
			else
				t.Modulate = _connected.Contains(t)
					? Colors.White
					: new Color(0.55f, 0.55f, 0.55f, 1f);
		}

		float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 170f);
		float glow = 1.15f + 0.35f * pulse;
		foreach (var t in _rightTerminals)
		{
			if (_connected.Contains(t))
				t.Modulate = Colors.White;
			else if (t.Color == _dragFrom.Color)
				t.Modulate = new Color(glow, glow, glow, 1f);
			else
				t.Modulate = new Color(0.55f, 0.55f, 0.55f, 1f);
		}
	}

	private void UpdateDragCursor(Vector2 pos)
	{
		if (_dragFrom == null)
			return;

		var t = TerminalAt(_rightBox, pos);
		bool valid = t != null && !_connected.Contains(t) && t.Color == _dragFrom.Color;
		Input.SetDefaultCursorShape(valid ? Input.CursorShape.PointingHand : Input.CursorShape.Drag);
	}

	private void ResetHighlights()
	{
		foreach (var t in _leftTerminals)
			t.Modulate = Colors.White;
		foreach (var t in _rightTerminals)
			t.Modulate = Colors.White;
		_hover = null;
	}

	private void ShakeFrame()
	{
		_shakeTween?.Kill();
		_shakeTween = CreateTween();
		_shakeTween.TweenProperty(_frame, "position:x", _frameBaseX + 9f, 0.05);
		_shakeTween.TweenProperty(_frame, "position:x", _frameBaseX - 9f, 0.05);
		_shakeTween.TweenProperty(_frame, "position:x", _frameBaseX + 5f, 0.05);
		_shakeTween.TweenProperty(_frame, "position:x", _frameBaseX, 0.05);
	}

	public override void _ExitTree()
	{
		CancelInteraction();
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

			case ControllerCommands.WirePick:
				if (GameplayBlocked || _finished) return true;
				if (command.Index >= 0 && command.Index < _leftTerminals.Count &&
					!_connected.Contains(_leftTerminals[command.Index]))
				{
					StartDrag(_leftTerminals[command.Index]);
				}
				return true;

			case ControllerCommands.WireConnect:
				if (GameplayBlocked || _finished) return true;
				if (_dragWire != null && command.Index >= 0 && command.Index < _rightTerminals.Count)
					EndDragAt(_rightTerminals[command.Index]);
				else
					PublishControllerContext();
				return true;

			default:
				return false;
		}
	}

	public void PublishControllerContext()
	{
		var left = new Godot.Collections.Array();
		var right = new Godot.Collections.Array();
		var leftConnected = new Godot.Collections.Array();
		var rightConnected = new Godot.Collections.Array();

		for (int i = 0; i < _leftTerminals.Count; i++)
		{
			left.Add(Hex(_leftTerminals[i].Color));
			leftConnected.Add(_connected.Contains(_leftTerminals[i]));
		}
		for (int i = 0; i < _rightTerminals.Count; i++)
		{
			right.Add(Hex(_rightTerminals[i].Color));
			rightConnected.Add(_connected.Contains(_rightTerminals[i]));
		}

		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["left"] = left,
			["right"] = right,
			["leftConnected"] = leftConnected,
			["rightConnected"] = rightConnected,
			["selected"] = _selectedLeft,
			["done"] = _finished,
			["overlay"] = OverlayState,
		});
	}

	private static string Hex(Color color) => "#" + color.ToHtml(false);

	private bool GameplayBlocked => GetNodeOrNull<PracticeReturn>("PracticeReturn")?.BlocksGameplay ?? false;

	private string OverlayState =>
		GetNodeOrNull<PracticeReturn>("PracticeReturn")?.ControllerOverlay ?? "active";

	private void Sparks(Control t, Color color)
	{
		MinigameEffects.Sparks(_effects, _effects.ToLocal(t.GetGlobalRect().GetCenter()), color, 14, .4f, 180);
	}
}

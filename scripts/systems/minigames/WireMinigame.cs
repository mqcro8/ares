using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class WireMinigame : Control
{
	[Signal] public delegate void CompletedEventHandler();

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

	private Line2D _dragWire;
	private Line2D _dragGlow;
	private ColorRect _dragFrom;
	private ColorRect _hover;

	public override void _Ready()
	{
		GD.Print("Wires listo, colores: ", WireColors.Count);
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
	}

	public override void _Input(InputEvent @event)
	{
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

	// Bloque 4: crear cable
	private void StartDrag(ColorRect t)
	{
		_dragFrom = t;
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
	}

	private void EndDrag(Vector2 pos)
	{
		var from = _dragFrom;
		var wire = _dragWire;
		var glow = _dragGlow;
		var target = TerminalAt(_rightBox, pos);
		// Bloque 7: ¿mismo color y libre?
		bool success = from != null && target != null
			&& !_connected.Contains(target) && target.Color == from.Color;

		if (success)
		{
			// Bloque 8a: fijar cable
			var end = Center(target);
			wire.SetPointPosition(1, end);
			if (glow != null)
				glow.SetPointPosition(1, end);

			_connected.Add(from);
			_connected.Add(target);
			_connectedCount++;

			Sparks(from, from.Color);
			Sparks(target, target.Color);

			// Bloques 9 y 10: ¿ya son cuatro? -> Completed
			if (_connectedCount == WireColors.Count)
			{
				ResetHighlights();
				GetTree().CreateTimer(0.45, false).Timeout += () => EmitSignal(SignalName.Completed);
			}
		}
		else
		{
			// Bloque 8b: borrar cable
			wire?.QueueFree();
			glow?.QueueFree();
			if (target != null)
				ShakeFrame();
		}

		_dragWire = null;
		_dragGlow = null;
		_dragFrom = null;
		Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
		ResetHighlights();
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
		GetTree().CreateTimer(1.0).Timeout += p.QueueFree;
	}
}

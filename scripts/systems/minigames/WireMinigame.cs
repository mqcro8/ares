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

	private readonly HashSet<ColorRect> _connected = new();
	private int _connectedCount;
	private Line2D _dragWire;
	private ColorRect _dragFrom;

	// Bloque 2: asignar colores
	public override void _Ready()
	{
		GD.Print("Wires listo, colores: ", WireColors.Count);
		_leftBox = GetNode<VBoxContainer>("LeftTerminals");
		_rightBox = GetNode<VBoxContainer>("RightTerminals");
		_wires = GetNode<Node2D>("Wires");

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
			_leftBox.GetChild<ColorRect>(i).Color = WireColors[i];
			_rightBox.GetChild<ColorRect>(i).Color = right[i];
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
		else if (@event is InputEventMouseMotion mm && _dragWire != null)
		{
			// Bloque 5: arrastrar
			_dragWire.SetPointPosition(1, _wires.ToLocal(mm.GlobalPosition));
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
		_dragWire = new Line2D
		{
			Width = WireWidth,
			DefaultColor = t.Color,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round
		};
		_dragWire.AddPoint(Center(t));
		_dragWire.AddPoint(Center(t));
		_wires.AddChild(_dragWire);
	}

	private void EndDrag(Vector2 pos)
	{
		var target = TerminalAt(_rightBox, pos);
		// Bloque 7: ¿mismo color y libre?
		if (target != null && !_connected.Contains(target) && target.Color == _dragFrom.Color)
		{
			// Bloque 8a: fijar cable
			_dragWire.SetPointPosition(1, Center(target));
			_connected.Add(_dragFrom);
			_connected.Add(target);
			_connectedCount++;
			// Bloques 9 y 10: ¿ya son cuatro? -> Completed
			if (_connectedCount == WireColors.Count)
				EmitSignal(SignalName.Completed);
		}
		else
		{
			// Bloque 8b: borrar cable
			_dragWire.QueueFree();
		}
		_dragWire = null;
		_dragFrom = null;
	}
}

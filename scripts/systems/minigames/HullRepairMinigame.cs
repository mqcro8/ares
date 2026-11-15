using Godot;
using System.Collections.Generic;
using System.Linq;
using ares.scripts.training;
using ares.scripts.mission;
using ares.scripts.systems.items;
using Ares.Systems.Controllers;

public partial class HullRepairMinigame : Control, IControllerCommandSink, IControllerContextSource
{
	[Signal] public delegate void CompletedEventHandler();
	[Signal] public delegate void FailedEventHandler();

	// Fase 1: parchar
	[Export] public float PlateSpeed { get; set; } = 220f;        // px/s
	[Export] public float PlateRotSpeed { get; set; } = 120f;     // grados/s
	[Export] public float PlaceTolerance { get; set; } = ToolEffects.HullBasePlaceTolerance; // px
	[Export] public float AngleTolerance { get; set; } = 10f;     // grados

	// Fase 2: soldar
	[Export] public float TorchSpeed { get; set; } = 140f;        // px/s
	[Export] public float WeldTolerance { get; set; } = ToolEffects.HullBaseWeldTolerance; // px
	[Export] public float HeatOffSeam { get; set; } = ToolEffects.HullBaseHeatOffSeam; // calor/s fuera de la costura
	[Export] public float HeatStill { get; set; } = 0.5f;         // calor/s si no avanza
	[Export] public float MaxStillSeconds { get; set; } = 0.6f;
	[Export] public float CoolOff { get; set; } = 0.5f;           // enfriamiento/s con soplete apagado
	[Export] public float CoolMoving { get; set; } = 0.15f;       // enfriamiento/s soldando bien
	[Export] public float UndoFraction { get; set; } = 0.25f;     // progreso que se pierde al sobrecalentar

	// Estabilidad del casco
	[Export] public float StabilityLoss { get; set; } = 0.25f;          // estabilidad perdida por sobrecalentamiento
	[Export] public float StabilityLossThreshold { get; set; } = 0.2f;  // por debajo de esto se pierde

	[Export] public float FineFactor { get; set; } = 0.35f;       // multiplicador con Shift

	[Export] public Color PlateColor { get; set; } = new Color("8a949f");
	[Export] public Color PlateSelectedColor { get; set; } = new Color("c3ccd6");
	[Export] public Color OutlineColor { get; set; } = new Color("1a1c1e");
	[Export] public Color SelectedOutlineColor { get; set; } = new Color("ffffff");
	[Export] public Color HoleIdleColor { get; set; } = new Color("8a8d90");
	[Export] public Color HoleOkColor { get; set; } = new Color("3cdc50");
	[Export] public Color BeadColor { get; set; } = new Color("ffe9a8");
	[Export] public Color TorchColor { get; set; } = new Color("ffd24a");

	private enum Phase { Pick, Hold, Locked, Weld, Done, Failed }

	private static readonly Vector2[][] Shapes = new Vector2[][]
	{
		// 0: triángulo
		new Vector2[] { new Vector2(0, -50), new Vector2(45, 35), new Vector2(-45, 35) },
		// 1: trapecio
		new Vector2[] { new Vector2(-30, -35), new Vector2(30, -35), new Vector2(55, 35), new Vector2(-55, 35) },
		// 2: casa
		new Vector2[] { new Vector2(-40, 40), new Vector2(-40, -10), new Vector2(0, -50), new Vector2(40, -10), new Vector2(40, 40) },
		// 3: L
		new Vector2[] { new Vector2(-40, -45), new Vector2(-10, -45), new Vector2(-10, 15), new Vector2(40, 15), new Vector2(40, 45), new Vector2(-40, 45) }
	};

	private static readonly Vector2[] HoleCenters =
	{
		new Vector2(120, 150),
		new Vector2(280, 150),
		new Vector2(440, 150),
		new Vector2(600, 150)
	};
	private static readonly float[] TrayX = { 150f, 290f, 430f, 570f };
	private const float TrayY = 330f;
	private static readonly Rect2 Bounds = new Rect2(10, 50, 700, 360);
	private const int Lookahead = 6;
	private const float SeamStep = 6f;
	private const float ResetHoldSeconds = 1.0f;

	private Panel _frame;
	private Node2D _world, _plates, _effects, _torch, _markers, _damage;
	private Polygon2D _hole;
	private Line2D _holeOutline, _seamLine, _beadLine;
	private ProgressBar _heatBar, _stabilityBar;
	private Label _phaseLabel, _hint;
	private CpuParticles2D _torchSparks;

	private Polygon2D[] _holes;
	private Line2D[] _outlines;
	private Line2D[] _seams;
	private Line2D[] _beads;
	private Vector2[][] _seamPoints;

	private readonly List<Polygon2D> _trayPlates = new();
	private readonly List<int> _plateShape = new();
	private readonly List<bool> _platePlaced = new();
	private int _selected;
	private Polygon2D _held;
	private int _heldSlot;

	private readonly int[] _holeShape = new int[4];
	private readonly float[] _holeAngle = new float[4];
	private readonly bool[] _holeFilled = new bool[4];
	private int _placedCount;

	private Phase _phase = Phase.Pick;
	private Timer _completionTimer;

	private Vector2[] _seam;
	private int _seamIndex;
	private int _progress;
	private float _heat;
	private float _stillTime;
	private Vector2 _torchPos;
	private bool _requireRelease;

	private float _stability = 1f;

	private Tween _rejectTween;

	private bool _mouseHeld;
	private bool _pressStartedInHold;
	private bool _dragMoved;
	private Vector2 _dragStart;

	private float _resetHold;
	private bool _resetArmed = true;
	private bool _actionWasPressed;
	private float _contextTimer;

	public override void _Ready()
	{
		if (MissionState.Instance?.HasEquipped(ToolEffects.PrecisionWelder) == true)
		{
			WeldTolerance = ToolEffects.HullPrecisionWeldTolerance;
			HeatOffSeam = ToolEffects.HullPrecisionHeatOffSeam;
		}
		_frame = GetNode<Panel>("Frame");
		_world = GetNode<Node2D>("Frame/World");
		_hole = GetNode<Polygon2D>("Frame/World/Hole");
		_plates = GetNode<Node2D>("Frame/World/Plates");
		_holeOutline = GetNode<Line2D>("Frame/World/HoleOutline");
		_seamLine = GetNode<Line2D>("Frame/World/Seam");
		_beadLine = GetNode<Line2D>("Frame/World/Bead");
		_torch = GetNode<Node2D>("Frame/World/Torch");
		_effects = GetNode<Node2D>("Frame/World/Effects");
		_heatBar = GetNode<ProgressBar>("Frame/HeatBar");
		_phaseLabel = GetNode<Label>("Frame/PhaseLabel");
		_hint = GetNode<Label>("Frame/Hint");

		_damage = new Node2D { Name = "Damage" };
		_world.AddChild(_damage);

		BuildTorch();
		CreateStabilityBar();
		BuildScene();
		SetPhase(Phase.Pick);
		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Hull);
		PublishControllerContext();
		PracticeReturn.Attach(this);
	}

	private void BuildTorch()
	{
		_torch.AddChild(new Polygon2D { Polygon = Circle(6f), Color = TorchColor });
		_torchSparks = new CpuParticles2D
		{
			Emitting = false,
			Amount = 24,
			Lifetime = 0.35f,
			Direction = Vector2.Up,
			Spread = 180f,
			InitialVelocityMin = 60f,
			InitialVelocityMax = 160f,
			Gravity = new Vector2(0f, 300f),
			ScaleAmountMin = 2f,
			ScaleAmountMax = 3f,
			Color = TorchColor
		};
		_torch.AddChild(_torchSparks);
	}

	private void CreateStabilityBar()
	{
		_stabilityBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 100,
			Value = 100,
			ShowPercentage = false,
			Position = new Vector2(420, 40),
			Size = new Vector2(284, 18),
			CustomMinimumSize = new Vector2(284, 18),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var label = new Label
		{
			Text = "HULL STABILITY",
			Position = new Vector2(8, 0),
			Size = new Vector2(160, 18),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		_stabilityBar.AddChild(label);

		_frame.AddChild(_stabilityBar);
		UpdateStabilityBar();
	}

	// Bloque 1: construir escena (cuatro huecos, cuatro placas)
	private void BuildScene()
	{
		_seamLine.Visible = false;
		_beadLine.Visible = false;
		_torch.Visible = false;
		_heatBar.Visible = false;

		// Crear los nodos de los cuatro huecos, contornos, costuras y cordones
		_holes = new Polygon2D[4];
		_outlines = new Line2D[4];
		_seams = new Line2D[4];
		_beads = new Line2D[4];
		_seamPoints = new Vector2[4][];

		for (int i = 0; i < 4; i++)
		{
			if (i == 0)
			{
				_holes[i] = _hole;
				_outlines[i] = _holeOutline;
				_seams[i] = _seamLine;
				_beads[i] = _beadLine;
			}
			else
			{
				_holes[i] = new Polygon2D();
				_outlines[i] = new Line2D { Width = 3f, Antialiased = true };
				_seams[i] = new Line2D { Width = 4f, Antialiased = true, DefaultColor = new Color(1f, 0.69f, 0.125f) };
				_beads[i] = new Line2D
				{
					Width = 6f,
					Antialiased = true,
					DefaultColor = new Color(1f, 0.914f, 0.659f),
					JointMode = Line2D.LineJointMode.Round,
					BeginCapMode = Line2D.LineCapMode.Round,
					EndCapMode = Line2D.LineCapMode.Round
				};
				_world.AddChild(_holes[i]);
				_world.AddChild(_outlines[i]);
				_world.AddChild(_seams[i]);
				_world.AddChild(_beads[i]);
			}

			_seams[i].Visible = false;
			_beads[i].Visible = false;
		}

		_markers = new Node2D { Name = "Markers" };
		_world.AddChild(_markers);

		// Orden de dibujado dentro de World
		var order = new List<Node>();
		for (int i = 0; i < 4; i++) order.Add(_holes[i]);
		order.Add(_damage);
		order.Add(_plates);
		for (int i = 0; i < 4; i++) order.Add(_outlines[i]);
		order.Add(_markers);
		for (int i = 0; i < 4; i++) order.Add(_seams[i]);
		for (int i = 0; i < 4; i++) order.Add(_beads[i]);
		order.Add(_torch);
		order.Add(_effects);
		for (int i = 0; i < order.Count; i++)
			_world.MoveChild(order[i], i);

		// Un hueco por forma, en orden aleatorio, con ángulo aleatorio
		var holeOrder = Enumerable.Range(0, Shapes.Length).OrderBy(_ => GD.Randi()).ToList();
		for (int h = 0; h < 4; h++)
		{
			_holeShape[h] = holeOrder[h];
			_holeAngle[h] = GD.RandRange(-150, 150);

			_holes[h].Polygon = Shapes[_holeShape[h]];
			_holes[h].Position = HoleCenters[h];
			_holes[h].RotationDegrees = _holeAngle[h];
			_holes[h].Color = new Color(0.04f, 0.04f, 0.05f);

			_outlines[h].Position = HoleCenters[h];
			_outlines[h].RotationDegrees = _holeAngle[h];
			SetClosedPoints(_outlines[h], Shapes[_holeShape[h]]);
			_outlines[h].DefaultColor = HoleIdleColor;
			_outlines[h].Visible = true;
		}

		// Placas en la bandeja, en orden aleatorio
		var plateOrder = Enumerable.Range(0, Shapes.Length).OrderBy(_ => GD.Randi()).ToList();
		for (int slot = 0; slot < plateOrder.Count; slot++)
		{
			int shape = plateOrder[slot];
			var plate = MakePlate(shape);
			plate.Position = new Vector2(TrayX[slot], TrayY);

			int hole = System.Array.IndexOf(_holeShape, shape);
			float rot;
			do { rot = GD.RandRange(-180, 180); }
			while (Mathf.Abs(Mathf.Wrap(rot - _holeAngle[hole], -180f, 180f)) < 30f);
			plate.RotationDegrees = rot;

			_plates.AddChild(plate);
			_trayPlates.Add(plate);
			_plateShape.Add(shape);
			_platePlaced.Add(false);
		}
	}

	private Polygon2D MakePlate(int shape)
	{
		var plate = new Polygon2D { Polygon = Shapes[shape], Color = PlateColor };
		var outline = new Line2D { Width = 2f, DefaultColor = OutlineColor, Name = "Outline" };
		SetClosedPoints(outline, Shapes[shape]);
		plate.AddChild(outline);
		return plate;
	}

	private static void SetClosedPoints(Line2D line, Vector2[] pts)
	{
		line.ClearPoints();
		foreach (var p in pts) line.AddPoint(p);
		line.AddPoint(pts[0]);
	}

	private static Vector2[] Circle(float r, int n = 16)
	{
		var pts = new Vector2[n];
		for (int i = 0; i < n; i++)
			pts[i] = Vector2.Right.Rotated(Mathf.Tau * i / n) * r;
		return pts;
	}

	private void SetPhase(Phase p)
	{
		_phase = p;
			switch (p)
		{
			case Phase.Pick:
				_phaseLabel.Text = "PHASE 1: PATCH";
				_hint.Text = "Click a plate to pick it   |   A/D or ←/→: select   |   Space: pick\nHold R to restart";
				break;
			case Phase.Hold:
				_phaseLabel.Text = "PHASE 1: PATCH";
				_hint.Text = "Drag or use WASD/arrows to move   |   Q/E: rotate   |   Shift: fine control\nClick or Space to place   |   Hold R to restart";
				break;
			case Phase.Weld:
				_phaseLabel.Text = "PHASE 2: WELD";
				_hint.Text = "Move torch with mouse or WASD/arrows   |   Hold click or Space to weld\nShift: fine control   |   Hold R to restart";
				break;
			case Phase.Done:
				_phaseLabel.Text = "HULL REPAIRED";
				_hint.Text = "Hold R to restart";
				break;
		}
		UpdateSelection();
		PublishControllerContext();
	}

	private void UpdateSelection()
	{
		if (_phase == Phase.Locked || _phase == Phase.Failed) return;
		for (int i = 0; i < _trayPlates.Count; i++)
		{
			bool selectable = !_platePlaced[i] && (_phase == Phase.Pick || _phase == Phase.Hold);
			bool sel = selectable && i == _selected;
			_trayPlates[i].Color = sel ? PlateSelectedColor : PlateColor;
			_trayPlates[i].GetNode<Line2D>("Outline").DefaultColor = sel ? SelectedOutlineColor : OutlineColor;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this)) return;
		if (_phase == Phase.Done || _phase == Phase.Failed) return;

		if (@event is InputEventKey k && k.Pressed && !k.Echo)
		{
			HandleKey(k);
			return;
		}

		if (@event is InputEventAction action && action.Pressed &&
			action.Action == ControllerActions.MinigameAction && !GameplayBlocked)
		{
			if (_phase == Phase.Pick) PickPlate();
			else if (_phase == Phase.Hold) TryPlace();
			_actionWasPressed = true;
			Handle();
			return;
		}

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			HandleMouseButton(mb);
			return;
		}

		if (@event is InputEventMouseMotion mm)
			HandleMouseMotion(mm);
	}

	private void HandleKey(InputEventKey k)
	{
		switch (_phase)
		{
			case Phase.Pick:
				if (k.Keycode == Key.Left || k.Keycode == Key.A)
				{
					MoveSelection(-1);
					Handle();
				}
				else if (k.Keycode == Key.Right || k.Keycode == Key.D || k.Keycode == Key.Tab)
				{
					MoveSelection(1);
					Handle();
				}
				else if (k.Keycode == Key.Space || k.Keycode == Key.Enter)
				{
					PickPlate();
					Handle();
				}
				break;

			case Phase.Hold:
				if (k.Keycode == Key.Space || k.Keycode == Key.Enter)
				{
					TryPlace();
					Handle();
				}
				break;
		}
	}

	private void HandleMouseButton(InputEventMouseButton mb)
	{
		if (mb.Pressed)
		{
			if (_phase == Phase.Pick)
			{
				int slot = PlateAt(mb.GlobalPosition);
				if (slot >= 0)
				{
					_selected = slot;
					UpdateSelection();
					PickPlate();
					_mouseHeld = true;
					_pressStartedInHold = false;
					_dragMoved = false;
					_dragStart = MouseWorld(mb.GlobalPosition);
					Handle();
				}
			}
			else if (_phase == Phase.Hold)
			{
				_mouseHeld = true;
				_pressStartedInHold = true;
				_dragMoved = false;
				_dragStart = MouseWorld(mb.GlobalPosition);
				Handle();
			}
			else if (_phase == Phase.Weld)
			{
				_torchPos = ClampToBounds(MouseWorld(mb.GlobalPosition));
				_torch.Position = _torchPos;
				Handle();
			}
		}
		else
		{
			if (_phase == Phase.Hold && _mouseHeld)
			{
				_mouseHeld = false;
				if (_dragMoved || _pressStartedInHold)
					TryPlace();
				Handle();
			}
			else
			{
				_mouseHeld = false;
			}
		}
	}

	private void HandleMouseMotion(InputEventMouseMotion mm)
	{
		if (_phase == Phase.Hold && _mouseHeld && _held != null)
		{
			Vector2 pos = MouseWorld(mm.GlobalPosition);
			_held.Position = ClampToBounds(pos);
			if (pos.DistanceTo(_dragStart) > 4f) _dragMoved = true;
			Handle();
		}
		else if (_phase == Phase.Weld)
		{
			_torchPos = ClampToBounds(MouseWorld(mm.GlobalPosition));
			_torch.Position = _torchPos;
			Handle();
		}
	}

	private int PlateAt(Vector2 globalPos)
	{
		for (int i = 0; i < _trayPlates.Count; i++)
		{
			if (_platePlaced[i]) continue;
			Vector2 local = _trayPlates[i].ToLocal(globalPos);
			if (Geometry2D.IsPointInPolygon(local, Shapes[_plateShape[i]]))
				return i;
		}
		return -1;
	}

	private Vector2 MouseWorld(Vector2 globalPos) => _world.ToLocal(globalPos);

	private void Handle() => GetViewport().SetInputAsHandled();

	private void MoveSelection(int dir)
	{
		int count = _trayPlates.Count;
		for (int step = 0; step < count; step++)
		{
			_selected = (_selected + dir + count) % count;
			if (!_platePlaced[_selected]) break;
		}
		UpdateSelection();
		PublishControllerContext();
	}

	private void SelectNextAvailable()
	{
		int count = _trayPlates.Count;
		for (int step = 0; step < count; step++)
		{
			int idx = (_selected + step) % count;
			if (!_platePlaced[idx])
			{
				_selected = idx;
				break;
			}
		}
	}

	private void PickPlate()
	{
		if (_platePlaced[_selected]) return;
		_held = _trayPlates[_selected];
		_heldSlot = _selected;
		_held.ZIndex = 5;
		SetPhase(Phase.Hold);
	}

	public override void _Process(double delta)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this)) return;
		float dt = (float)delta;

		// Mantener R para reiniciar todo el minijuego
		if (Input.IsKeyPressed(Key.R))
		{
			if (_resetArmed)
			{
				_resetHold += dt;
				if (_resetHold >= ResetHoldSeconds)
				{
					_resetHold = 0f;
					_resetArmed = false;
					Restart();
					return;
				}
			}
		}
		else
		{
			_resetHold = 0f;
			_resetArmed = true;
		}

		if (_phase == Phase.Hold) ProcessHold(dt);
		else if (_phase == Phase.Weld) ProcessWeld(dt);

		// The phone sends minigame_action as a held action; use its rising edge to
		// pick or place just like a Space tap.
		bool action = Input.IsActionPressed(ControllerActions.MinigameAction);
		if (action && !_actionWasPressed && !GameplayBlocked)
		{
			if (_phase == Phase.Pick) PickPlate();
			else if (_phase == Phase.Hold) TryPlace();
		}
		_actionWasPressed = action;

		_contextTimer += dt;
		if (_phase == Phase.Weld && _contextTimer >= 0.15f)
		{
			_contextTimer = 0f;
			PublishControllerContext();
		}
	}

	private static Vector2 ReadDirection()
	{
		Vector2 move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		float x = move.X, y = move.Y;
		if (Input.IsKeyPressed(Key.Left)) x -= 1f;
		if (Input.IsKeyPressed(Key.Right)) x += 1f;
		if (Input.IsKeyPressed(Key.Up)) y -= 1f;
		if (Input.IsKeyPressed(Key.Down)) y += 1f;
		return new Vector2(x, y).LimitLength(1f);
	}

	private float Fine() =>
		Input.IsKeyPressed(Key.Shift) || Input.IsActionPressed(ControllerActions.MinigameFine)
			? FineFactor
			: 1f;

	private static Vector2 ClampToBounds(Vector2 p) => new Vector2(
		Mathf.Clamp(p.X, Bounds.Position.X, Bounds.End.X),
		Mathf.Clamp(p.Y, Bounds.Position.Y, Bounds.End.Y));

	// Bloque 2: mover y girar la placa con el teclado
	private void ProcessHold(float dt)
	{
		float f = Fine();
		_held.Position = ClampToBounds(_held.Position + ReadDirection() * PlateSpeed * f * dt);

		float rot = 0f;
		if (Input.IsKeyPressed(Key.E) || Input.IsActionPressed(ControllerActions.RotateRight)) rot += 1f;
		if (Input.IsKeyPressed(Key.Q) || Input.IsActionPressed(ControllerActions.RotateLeft)) rot -= 1f;
		_held.RotationDegrees += rot * PlateRotSpeed * f * dt;

		// Guía: el contorno del hueco se pone verde cuando la placa correcta está alineada
		int fit = FindFittingHole();
		for (int h = 0; h < _outlines.Length; h++)
			if (!_holeFilled[h])
				_outlines[h].DefaultColor = h == fit ? HoleOkColor : HoleIdleColor;
	}

	private int FindFittingHole()
	{
		int shape = _plateShape[_heldSlot];
		for (int h = 0; h < _holes.Length; h++)
		{
			if (_holeFilled[h] || _holeShape[h] != shape) continue;
			if (_held.Position.DistanceTo(HoleCenters[h]) > PlaceTolerance) continue;
			float diff = Mathf.Abs(Mathf.Wrap(_held.RotationDegrees - _holeAngle[h], -180f, 180f));
			if (diff <= AngleTolerance) return h;
		}
		return -1;
	}

	// Bloques 3 y 4: soltar sobre el hueco y validar
	private void TryPlace()
	{
		int hole = FindFittingHole();
		if (hole >= 0)
			Rivet(hole);         // Bloque 5a
		else
			RejectPlace();       // Bloque 5b: se queda en la mano para ajustar
	}

	// Bloque 5a: remachar placa
	private void Rivet(int hole)
	{
		_rejectTween?.Kill();
		_rejectTween = null;
		_held.Position = HoleCenters[hole];
		_held.RotationDegrees = _holeAngle[hole];
		_held.ZIndex = 0;
		_held.Color = PlateColor;
		_outlines[hole].DefaultColor = HoleIdleColor;
		_outlines[hole].Visible = false;
		_holeFilled[hole] = true;
		_platePlaced[_heldSlot] = true;

		_held = null;
		Sparks(HoleCenters[hole], TorchColor);
		_placedCount++;

		if (_placedCount >= _trayPlates.Count)
		{
			StartWeld();
		}
		else
		{
			SelectNextAvailable();
			SetPhase(Phase.Pick);
		}
	}

	// Bloque 5b: rechazo sin devolver la placa a la bandeja
	private void RejectPlace()
	{
		_held.Color = new Color(0.85f, 0.25f, 0.25f);
		_rejectTween?.Kill();
		_rejectTween = CreateTween();
		_rejectTween.TweenProperty(_held, "color", PlateSelectedColor, 0.35);
	}

	// Bloque 6: generar las cuatro costuras (borde de cada placa)
	private void StartWeld()
	{
		for (int h = 0; h < _holes.Length; h++)
		{
			var shape = Shapes[_holeShape[h]];
			var xf = new Transform2D(Mathf.DegToRad(_holeAngle[h]), HoleCenters[h]);
			var pts = new List<Vector2>();
			for (int i = 0; i < shape.Length; i++)
			{
				Vector2 a = xf * shape[i];
				Vector2 b = xf * shape[(i + 1) % shape.Length];
				int steps = Mathf.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / SeamStep));
				for (int s = 0; s < steps; s++)
					pts.Add(a.Lerp(b, s / (float)steps));
			}
			pts.Add(pts[0]);   // cerrar la costura
			_seamPoints[h] = pts.ToArray();

			_seams[h].ClearPoints();
			foreach (var p in _seamPoints[h]) _seams[h].AddPoint(p);
			_seams[h].Visible = true;

			_beads[h].ClearPoints();
			_beads[h].Visible = true;

			// Punto A de cada costura
			_markers.AddChild(new Polygon2D
			{
				Polygon = Circle(7f),
				Color = new Color(0.24f, 0.86f, 0.31f),
				Position = _seamPoints[h][0],
				Name = $"StartMarker{h}"
			});
		}

		_heat = 0f;
		_requireRelease = true;
		_heatBar.Value = 0;
		_heatBar.Visible = true;
		_torch.Visible = true;
		BeginSeam(0);
		SetPhase(Phase.Weld);
	}

	private void BeginSeam(int index)
	{
		_seamIndex = index;
		_seam = _seamPoints[index];
		_progress = 0;
		_stillTime = 0f;
		_torchPos = HoleCenters[index];
		_torch.Position = _torchPos;
		_torchSparks.Emitting = false;
		UpdateSeamHighlight();
		PublishControllerContext();
	}

	private void UpdateSeamHighlight()
	{
		for (int h = 0; h < _seams.Length; h++)
			_seams[h].DefaultColor = h == _seamIndex
				? new Color(1f, 0.69f, 0.125f)
				: new Color(0.45f, 0.31f, 0.06f);
	}

	// Bloques 7 a 12: soldar
	private void ProcessWeld(float dt)
	{
		// Mover el soplete con el teclado (el mouse lo mueve en HandleMouseMotion)
		_torchPos = ClampToBounds(_torchPos + ReadDirection() * TorchSpeed * Fine() * dt);
		_torch.Position = _torchPos;

		// Bloque 7: mantener Espacio o clic izquierdo (se ignora hasta soltarlo una vez)
		bool on = Input.IsKeyPressed(Key.Space) || Input.IsMouseButtonPressed(MouseButton.Left) ||
			Input.IsActionPressed(ControllerActions.MinigameAction);
		if (_requireRelease)
		{
			if (!on) _requireRelease = false;
			on = false;
		}

		bool onSeam = false;
		bool advanced = false;
		_torchSparks.Emitting = false;

		if (on)
		{
			// Bloques 8 y 9: seguir la costura y evaluar el trazo
			int end = Mathf.Min(_progress + Lookahead, _seam.Length - 1);
			int best = -1;
			for (int i = _progress; i <= end; i++)
				if (_torchPos.DistanceTo(_seam[i]) <= WeldTolerance)
					best = i;

			onSeam = best >= 0;
			if (onSeam)
			{
				_torchSparks.Emitting = true;
				if (best > _progress)
				{
					_progress = best;
					advanced = true;
					RedrawBead();
				}
			}
		}

		// Calor
		if (on && !onSeam)
		{
			_heat += HeatOffSeam * dt;
		}
		else if (on && advanced)
		{
			_stillTime = 0f;
			_heat -= CoolMoving * dt;
		}
		else if (on)
		{
			_stillTime += dt;
			if (_stillTime > MaxStillSeconds)
				_heat += HeatStill * dt;
		}
		else
		{
			_stillTime = 0f;
			_heat -= CoolOff * dt;
		}

		_heat = Mathf.Clamp(_heat, 0f, 1f);
		_heatBar.Value = _heat * 100f;
		_heatBar.Modulate = Colors.White.Lerp(new Color(1f, 0.3f, 0.3f), _heat);

		// Bloque 10: ¿calor al máximo?
		if (_heat >= 1f)
		{
			Overheat();   // Bloque 11a
			return;
		}

		// Bloque 11b: ¿costura completa?
		if (_progress >= _seam.Length - 3)
			CompleteSeam();
	}

	private void CompleteSeam()
	{
		_progress = _seam.Length - 1;
		RedrawBead();

		if (_seamIndex < _seams.Length - 1)
		{
			Sparks(HoleCenters[_seamIndex], BeadColor);
			BeginSeam(_seamIndex + 1);
		}
		else
		{
			Finish();     // Bloque 12
		}
	}

	private void RedrawBead()
	{
		var bead = _beads[_seamIndex];
		bead.ClearPoints();
		for (int i = 0; i <= _progress && i < _seam.Length; i++)
			bead.AddPoint(_seam[i]);
	}

	// Bloque 11a: deshacer parte de la soldadura y dañar el casco
	private void Overheat()
	{
		_progress = (int)(_progress * (1f - UndoFraction));
		_heat = 0f;
		_stillTime = 0f;
		_torchPos = _seam[_progress];
		_torch.Position = _torchPos;
		_torchSparks.Emitting = false;
		RedrawBead();
		Sparks(_torchPos, new Color(1f, 0.3f, 0.2f));
		Shake();

		_stability = Mathf.Clamp(_stability - StabilityLoss, 0f, 1f);
		UpdateStabilityBar();
		AddDamageMark();
		PublishControllerContext();

		if (_stability <= StabilityLossThreshold)
			Fail();
	}

	private void AddDamageMark()
	{
		const int n = 8;
		var pts = new Vector2[n];
		float radius = (float)GD.RandRange(10.0, 22.0);
		for (int i = 0; i < n; i++)
		{
			float ang = Mathf.Tau * i / n;
			float r = radius * (float)GD.RandRange(0.55, 1.15);
			pts[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
		}

		_damage.AddChild(new Polygon2D
		{
			Polygon = pts,
			Position = new Vector2(
				(float)GD.RandRange(Bounds.Position.X + 30f, Bounds.End.X - 30f),
				(float)GD.RandRange(Bounds.Position.Y + 30f, Bounds.End.Y - 140f)),
			Color = new Color(0.015f, 0.015f, 0.02f)
		});
	}

	private void UpdateStabilityBar()
	{
		if (_stabilityBar == null) return;
		_stabilityBar.Value = _stability * 100f;
		_stabilityBar.Modulate = new Color(1f, 0.3f, 0.3f).Lerp(new Color(0.3f, 1f, 0.4f), _stability);
	}

	private void Fail()
	{
		_phase = Phase.Failed;
		_phaseLabel.Text = "HULL UNSTABLE";
		_hint.Text = "REPAIR FAILED   |   Hold R to restart";
		_torchSparks.Emitting = false;
		_torch.Visible = false;
		_heatBar.Visible = false;
		EmitSignal(SignalName.Failed);
		PublishControllerContext();
	}

	// Bloque 12: emitir completed
	private void Finish()
	{
		SetPhase(Phase.Done);
		_progress = _seam.Length - 1;
		RedrawBead();
		_torchSparks.Emitting = false;
		_torch.Visible = false;
		_heatBar.Visible = false;
		for (int h = 0; h < _holes.Length; h++)
			Sparks(HoleCenters[h], BeadColor);
		_completionTimer = MinigameEffects.CompleteAfter(this, .8, () =>
		{
			if (_phase == Phase.Done)
				EmitSignal(SignalName.Completed);
		});
	}

	private void Shake()
	{
		float x = _frame.Position.X;
		var tw = CreateTween();
		tw.TweenProperty(_frame, "position:x", x + 8, 0.05);
		tw.TweenProperty(_frame, "position:x", x - 8, 0.05);
		tw.TweenProperty(_frame, "position:x", x + 4, 0.05);
		tw.TweenProperty(_frame, "position:x", x, 0.05);
	}

	private void Sparks(Vector2 worldPos, Color color)
	{
		MinigameEffects.Sparks(_effects, worldPos, color);
	}

	private void Restart()
	{
		if (GodotObject.IsInstanceValid(_completionTimer))
		{
			_completionTimer.Stop();
			_completionTimer.QueueFree();
		}
		_completionTimer = null;
		ClearDynamic();
		BuildScene();
		SetPhase(Phase.Pick);
	}

	private void ClearDynamic()
	{
		_rejectTween?.Kill();
		_rejectTween = null;

		foreach (var plate in _trayPlates)
			RemoveAndFree(plate);
		_trayPlates.Clear();
		_plateShape.Clear();
		_platePlaced.Clear();

		for (int i = 1; i < _holes.Length; i++)
		{
			RemoveAndFree(_holes[i]);
			RemoveAndFree(_outlines[i]);
			RemoveAndFree(_seams[i]);
			RemoveAndFree(_beads[i]);
		}

		RemoveAndFree(_markers);
		_markers = null;

		foreach (var child in _damage.GetChildren())
			RemoveAndFree(child);

		_hole.Polygon = new Vector2[0];
		_holeOutline.ClearPoints();
		_seamLine.ClearPoints();
		_beadLine.ClearPoints();
		_holeOutline.Visible = false;
		_seamLine.Visible = false;
		_beadLine.Visible = false;
		_torch.Visible = false;
		_torchSparks.Emitting = false;
		_heatBar.Visible = false;
		_heatBar.Value = 0;

		_held = null;
		_heldSlot = 0;
		_selected = 0;
		_placedCount = 0;
		for (int i = 0; i < _holeFilled.Length; i++) _holeFilled[i] = false;
		_seam = null;
		_seamPoints = null;
		_seamIndex = 0;
		_progress = 0;
		_heat = 0f;
		_stillTime = 0f;
		_requireRelease = false;
		_stability = 1f;
		_mouseHeld = false;
		_pressStartedInHold = false;
		_dragMoved = false;
		_resetHold = 0f;
		_phase = Phase.Pick;
		UpdateStabilityBar();
	}

	public override void _ExitTree()
	{
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

			case ControllerCommands.HullSelect:
				if (GameplayBlocked) return true;
				if (_phase == Phase.Pick && command.Index >= 0 && command.Index < _platePlaced.Count &&
					!_platePlaced[command.Index])
				{
					_selected = command.Index;
					UpdateSelection();
					PublishControllerContext();
				}
				return true;

			case ControllerCommands.HullRestart:
				if (GameplayBlocked) return true;
				Restart();
				return true;

			default:
				return false;
		}
	}

	public void PublishControllerContext()
	{
		var placed = new Godot.Collections.Array();
		for (int i = 0; i < _platePlaced.Count; i++) placed.Add(_platePlaced[i]);

		float progress = _seam is { Length: > 1 } ? _progress / (float)(_seam.Length - 1) : 0f;

		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["phase"] = PhaseId(_phase),
			["selected"] = _selected,
			["heldSlot"] = _held != null ? _heldSlot : -1,
			["placed"] = placed,
			["seam"] = _seamIndex,
			["seams"] = _seamPoints?.Length ?? 0,
			["progress"] = Mathf.Snapped(Mathf.Clamp(progress, 0f, 1f), 0.01f),
			["heat"] = Mathf.Snapped(_heat, 0.01f),
			["stability"] = Mathf.Snapped(_stability, 0.01f),
			["overlay"] = OverlayState,
		});
	}

	private static string PhaseId(Phase phase) => phase switch
	{
		Phase.Pick => "pick",
		Phase.Hold => "hold",
		Phase.Weld => "weld",
		Phase.Done => "done",
		Phase.Failed => "failed",
		_ => "locked",
	};

	private bool GameplayBlocked => GetNodeOrNull<PracticeReturn>("PracticeReturn")?.BlocksGameplay ?? false;

	private string OverlayState =>
		GetNodeOrNull<PracticeReturn>("PracticeReturn")?.ControllerOverlay ?? "active";

	private static void RemoveAndFree(Node node)
	{
		if (node == null) return;
		node.GetParent()?.RemoveChild(node);
		node.QueueFree();
	}
}

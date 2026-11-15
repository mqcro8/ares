using Godot;
using System.Collections.Generic;
using ares.scripts.training;
using ares.scripts.mission;
using ares.scripts.systems.items;
using Ares.Systems.Controllers;

public partial class FrequencyBoxMinigame : Control, IControllerCommandSink, IControllerContextSource
{
	[Signal] public delegate void CompletedEventHandler();

	[Export] public float MatchThreshold { get; set; } = ToolEffects.FrequencyBaseThreshold; // similitud mínima (0 a 1)
	[Export] public float HoldSeconds { get; set; } = ToolEffects.FrequencyBaseHoldSeconds; // tiempo que debe mantenerse
	[Export] public float KeyAdjustSpeed { get; set; } = 1f;          // valor/s con flechas (Shift = fino)
	[Export] public string RevealWord { get; set; } = "";            // palabra que se muestra al completar
	[Export] public Color TargetColor { get; set; } = new Color("3cdc50");
	[Export] public Color PlayerColor { get; set; } = new Color("d62828");
	[Export] public Color LedOff { get; set; } = new Color("1c4a22");
	[Export] public Color LedOn { get; set; } = new Color("3cdc50");

	[Export] public float Smoothing { get; set; } = 18f;    // qué tan rápido la perilla alcanza al valor pedido
	[Export] public float PixelSize { get; set; } = 2f;     // onda "8 bits" en píxeles (0 = curva suave)
	[Export] public int Seed { get; set; } = 0;             // 0 = onda nueva y distinta en cada instancia

	private const int Samples = 128;
	private const float HalfWidth = 85f;      // la onda cabe dentro del círculo (radio 100)
	private const float MaxAmp = 50f;
	private const float MinAmpFactor = 0.2f;
	private const float MinFreq = 1f;
	private const float MaxFreq = 4f;

	// Estado compartido entre instancias para que ninguna repita la anterior
	private static int _instanceCount;
	private static float[] _lastTarget;

	private Panel _screen;
	private Line2D _targetWave;
	private Line2D _playerWave;
	private ColorRect _led;
	private Label _wordLabel;
	private Label _brandLabel;
	private Node2D _effects;
	private readonly List<Panel> _knobs = new();
	private readonly List<Control> _pointers = new();
	private readonly RandomNumberGenerator _rng = new();
	private int _selectedKnob;

	// 0 = amplitud, 1 = frecuencia, 2 = fase (valores de 0 a 1)
	private readonly float[] _target = new float[3];
	private readonly float[] _value = new float[3];        // valor que se ve (suavizado)
	private readonly float[] _targetValue = new float[3];  // valor al que apunta el jugador

	private float _hold;
	private float _time;
	private bool _done;
	private float _contextTimer;
	public int SelectedKnob => _selectedKnob;
	public float SelectedValue => _targetValue[_selectedKnob];

	public override void _Ready()
	{
		if (MissionState.Instance?.HasEquipped(ToolEffects.FrequencyAnalyzer) == true)
		{
			MatchThreshold = ToolEffects.FrequencyBaseThreshold + ToolEffects.FrequencyThresholdDelta;
		}
		_screen = GetNode<Panel>("Box/Screen");
		_effects = GetNode<Node2D>("Effects");
		_targetWave = GetNode<Line2D>("Box/Screen/TargetWave");
		_playerWave = GetNode<Line2D>("Box/Screen/PlayerWave");
		_led = GetNode<ColorRect>("Box/Knobs/FrequencyKnob/StatusLight");
		_wordLabel = GetNode<Label>("Box/WordLabel");

		string[] names = { "AmplitudeKnob", "FrequencyKnob", "PhaseKnob" };
		foreach (var n in names)
		{
			_knobs.Add(GetNode<Panel>($"Box/Knobs/{n}/Knob"));
			_pointers.Add(GetNode<Control>($"Box/Knobs/{n}/Knob/Pointer"));
		}

		_targetWave.DefaultColor = TargetColor;
		_playerWave.DefaultColor = PlayerColor;
		_targetWave.Antialiased = PixelSize <= 0f;
		_playerWave.Antialiased = PixelSize <= 0f;
		_wordLabel.Text = "";
		_brandLabel = GetNode<Label>("Box/BrandLabel");
		_brandLabel.Text = "1/2/3 SELECT   ARROWS ADJUST";
		_led.Color = LedOff;

		SeedGenerator();
		GeneratePuzzle();
		DrawWave(_targetWave, _target);
		Refresh();
		UpdateKnobHighlight();
		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Frequency);
		PublishControllerContext();
		PracticeReturn.Attach(this);
	}

	// Semilla propia de esta instancia: nunca sale la misma onda dos veces
	private void SeedGenerator()
	{
		if (Seed != 0)
		{
			_rng.Seed = (ulong)Seed;
			return;
		}
		_rng.Seed = Time.GetTicksUsec() + (ulong)(_instanceCount++ * 7919);
	}

	// Generar onda objetivo y posiciones iniciales (que no empiecen cerca)
	private void GeneratePuzzle()
	{
		int attempts = 0;
		do
		{
			for (int i = 0; i < 3; i++)
			{
				_target[i] = _rng.RandfRange(0.1f, 0.9f);
				_targetValue[i] = _rng.Randf();
			}
			attempts++;
		}
		while ((SimilarityOf(_target, _targetValue) > 0.6f || TooSimilarToLast()) && attempts < 128);

		_lastTarget = (float[])_target.Clone();
		for (int i = 0; i < 3; i++)
			_value[i] = _targetValue[i];
	}

	// Evita que dos cajas instanciadas seguidas tengan la misma onda
	private bool TooSimilarToLast()
	{
		if (Seed != 0 || _lastTarget == null)
			return false;
		return Mathf.Abs(_target[0] - _lastTarget[0]) < 0.1f
			&& Mathf.Abs(_target[1] - _lastTarget[1]) < 0.1f
			&& Mathf.Abs(_target[2] - _lastTarget[2]) < 0.1f;
	}

	private static float WaveY(float[] p, float t)
	{
		float amp = Mathf.Lerp(MinAmpFactor, 1f, p[0]) * MaxAmp;
		float freq = Mathf.Lerp(MinFreq, MaxFreq, p[1]);
		float phase = p[2] * Mathf.Tau;
		return amp * Mathf.Sin(Mathf.Tau * freq * t + phase);
	}

	private void DrawWave(Line2D line, float[] p)
	{
		line.ClearPoints();
		for (int i = 0; i < Samples; i++)
		{
			float t = i / (float)(Samples - 1);
			float x = Mathf.Lerp(-HalfWidth, HalfWidth, t);
			float y = -WaveY(p, t);
			// Ajuste a la grilla de píxeles para el look de 8 bits
			if (PixelSize > 0f)
			{
				x = Mathf.Round(x / PixelSize) * PixelSize;
				y = Mathf.Round(y / PixelSize) * PixelSize;
			}
			line.AddPoint(new Vector2(x, y));
		}
	}

	// Similitud entre la onda objetivo y la del jugador (0 a 1)
	private float Similarity() => SimilarityOf(_target, _value);

	private static float SimilarityOf(float[] a, float[] b)
	{
		float sum = 0f;
		for (int i = 0; i < Samples; i++)
		{
			float t = i / (float)(Samples - 1);
			sum += Mathf.Abs(WaveY(a, t) - WaveY(b, t));
		}
		float meanAbs = sum / Samples;
		return Mathf.Clamp(1f - meanAbs / (2f * MaxAmp), 0f, 1f);
	}

	private void Refresh()
	{
		for (int i = 0; i < 3; i++)
			_pointers[i].RotationDegrees = Mathf.Lerp(-135f, 135f, _value[i]);
		DrawWave(_playerWave, _value);
	}

	// Lleva el valor visible hacia el valor pedido, sin saltos
	private void AnimateValues(float dt)
	{
		float k = 1f - Mathf.Exp(-Mathf.Max(Smoothing, 0.01f) * dt);
		bool changed = false;
		for (int i = 0; i < 3; i++)
		{
			float diff = _targetValue[i] - _value[i];
			if (Mathf.Abs(diff) > 0.0002f)
			{
				_value[i] += diff * k;
				changed = true;
			}
			else if (diff != 0f)
			{
				_value[i] = _targetValue[i];
				changed = true;
			}
		}
		if (changed)
			Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this) || _done) return;
		if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;

		if (k.Keycode is Key.Key1 or Key.Key2 or Key.Key3)
		{
			_selectedKnob = (int)k.Keycode - (int)Key.Key1;
			UpdateKnobHighlight();
			PublishControllerContext();
			GetViewport().SetInputAsHandled();
		}
		else if (k.Keycode is Key.Left or Key.Right)
		{
			GetViewport().SetInputAsHandled();
		}
	}

	private float FineFactor() => Input.IsKeyPressed(Key.Shift) ? 0.25f : 1f;

	private void UpdateKnobHighlight()
	{
		for (int i = 0; i < _knobs.Count; i++)
		{
			bool selected = i == _selectedKnob;

			_knobs[i].Modulate = selected ? new Color(1f, 1f, 0.7f)
				: Colors.White;

			var caption = _knobs[i].GetParent().GetNode<Label>("Caption");
			caption.AddThemeColorOverride("font_color", selected ? Colors.White
				: new Color(0.8f, 0.8f, 0.8f));
		}
	}

	public override void _Process(double delta)
	{
		if (!ares.scripts.training.PracticeReturn.CanPlay(this) || _done) return;

		float dt = (float)delta;
		_time += dt;

		float dir = 0f;
		if (Input.IsKeyPressed(Key.Left)) dir -= 1f;
		if (Input.IsKeyPressed(Key.Right)) dir += 1f;
		if (dir != 0f)
			_targetValue[_selectedKnob] = Mathf.Clamp(
				_targetValue[_selectedKnob] + dir * KeyAdjustSpeed * FineFactor() * dt, 0f, 1f);

		_contextTimer += dt;
		if (dir != 0f || _contextTimer >= 0.2f)
		{
			_contextTimer = 0f;
			PublishControllerContext();
		}

		AnimateValues(dt);

		bool matched = Similarity() >= MatchThreshold;
		_hold = matched ? _hold + dt : 0f;

		// La luz late suavemente mientras está en el umbral
		float pulse = matched ? 0.35f + 0.25f * Mathf.Sin(_time * 12f) : 0f;
		_led.Color = matched ? LedOn.Lerp(Colors.White, pulse * 0.35f) : LedOff;

		if (_hold >= HoldSeconds)
			Complete();
	}

	private void Complete()
	{
		_done = true;
		_playerWave.DefaultColor = TargetColor;
		_led.Color = LedOn;
		_wordLabel.Text = RevealWord;
		Sparks(_screen.GetGlobalRect().GetCenter(), TargetColor);
		PublishControllerContext();
		MinigameEffects.CompleteAfter(this, .8, () => EmitSignal(SignalName.Completed));
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

			case ControllerCommands.FrequencySelect:
				if (GameplayBlocked || _done) return true;
				if (command.Index >= 0 && command.Index < 3)
				{
					_selectedKnob = command.Index;
					UpdateKnobHighlight();
					PublishControllerContext();
				}
				return true;

			case ControllerCommands.FrequencySet:
				if (GameplayBlocked || _done) return true;
				if (command.Index >= 0 && command.Index < 3 && command.HasValue)
				{
					_targetValue[command.Index] = Mathf.Clamp(command.Value, 0f, 1f);
					_selectedKnob = command.Index;
					UpdateKnobHighlight();
					PublishControllerContext();
				}
				return true;

			default:
				return false;
		}
	}

	public void PublishControllerContext()
	{
		var values = new Godot.Collections.Array();
		for (int i = 0; i < 3; i++) values.Add(Mathf.Snapped(_targetValue[i], 0.001f));

		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["values"] = values,
			["selected"] = _selectedKnob,
			["similarity"] = Mathf.Snapped(Similarity(), 0.001f),
			["hold"] = Mathf.Snapped(Mathf.Clamp(_hold / HoldSeconds, 0f, 1f), 0.01f),
			["done"] = _done,
			["overlay"] = OverlayState,
		});
	}

	private bool GameplayBlocked => GetNodeOrNull<PracticeReturn>("PracticeReturn")?.BlocksGameplay ?? false;

	private string OverlayState =>
		GetNodeOrNull<PracticeReturn>("PracticeReturn")?.ControllerOverlay ?? "active";

	private void Sparks(Vector2 globalPos, Color color)
	{
		MinigameEffects.Sparks(_effects, _effects.ToLocal(globalPos), color);
	}
}

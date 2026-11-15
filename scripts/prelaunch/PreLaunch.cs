namespace ares.scripts.prelaunch;

using Godot;

/// <summary>Small one-shot orbital transfer game. One scene, one controller, no external state.</summary>
public partial class PreLaunch : Node2D
{
	public enum FlightState { Orbiting, Launching, Won, Lost }

	[Export] public Vector2 EarthPosition { get; set; } = new(576, 700);
	[Export] public Vector2 MarsPosition { get; set; } = new(576, 105);
	[Export] public float EarthRadius { get; set; } = 104f;
	[Export] public float MarsRadius { get; set; } = 82f;
	[Export] public float EarthGravityRadius { get; set; } = 190f;
	[Export] public float MarsGravityRadius { get; set; } = 210f;
	[Export] public float OrbitRadius { get; set; } = 142f;
	[Export] public float OrbitSpeed { get; set; } = 2.8f;
	[Export] public float ChargeTurns { get; set; } = 1.5f;
	[Export] public float MinLaunchSpeed { get; set; } = 185f;
	[Export] public float MaxLaunchSpeed { get; set; } = 430f;
	[Export] public float SpaceDrag { get; set; } = 0.018f;
	[Export] public float MinimumFlightSpeed { get; set; } = 18f;
	[Export] public float GravityStrength { get; set; } = 11000f;
	[Export] public float AstronomicalUnitPixels { get; set; } = 600f;

	public FlightState State { get; private set; } = FlightState.Orbiting;
	public float Charge { get; private set; }
	public float DistanceTravelled { get; private set; }
	public Vector2 FlightVelocity { get; private set; }

	private Sprite2D _rocket;
	private Label _chargeLabel;
	private Label _distanceLabel;
	private Label _message;
	private Label _stateLabel;
	private float _orbitAngle;
	private float _wonAngle;
	private float _slowMotionRemaining;
	private Vector2 _flightPosition;
	private bool _inputReleased = true;

	public override void _Ready()
	{
		_rocket = GetNode<Sprite2D>("Rocket");
		_chargeLabel = GetNode<Label>("HUD/Margin/VBox/Charge");
		_distanceLabel = GetNode<Label>("HUD/Margin/VBox/Distance");
		_stateLabel = GetNode<Label>("HUD/Margin/VBox/State");
		_message = GetNode<Label>("HUD/Message");
		ResetFlight();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		if (Engine.TimeScale < 1f)
		{
			_slowMotionRemaining -= step / Mathf.Max((float)Engine.TimeScale, 0.01f);
			if (_slowMotionRemaining <= 0f) Engine.TimeScale = 1f;
		}

		switch (State)
		{
			case FlightState.Orbiting: ProcessOrbit(step); break;
			case FlightState.Launching: ProcessFlight(step); break;
			case FlightState.Won: ProcessWon(step); break;
		}
		UpdateHud();
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent input)
	{
		bool pressed = input switch
		{
			InputEventKey key => key.Pressed && (key.Keycode == Key.Space || key.Keycode == Key.Enter),
			InputEventMouseButton mouse => mouse.Pressed && mouse.ButtonIndex == MouseButton.Left,
			_ => false
		};
		if (!pressed) { _inputReleased = true; return; }
		if (!_inputReleased) return;
		_inputReleased = false;
		GetViewport().SetInputAsHandled();
		if (State == FlightState.Orbiting) Launch();
		else if (State == FlightState.Lost) ResetFlight();
	}

	private void ProcessOrbit(float delta)
	{
		_orbitAngle = Mathf.Wrap(_orbitAngle + OrbitSpeed * delta, 0f, Mathf.Tau);
		Charge = Mathf.Clamp(Charge + OrbitSpeed * delta / (Mathf.Tau * ChargeTurns), 0f, 1f);
		_rocket.Position = EarthPosition + Vector2.FromAngle(_orbitAngle) * OrbitRadius;
		_rocket.Rotation = _orbitAngle + Mathf.Pi / 2f;
	}

	private void Launch()
	{
		State = FlightState.Launching;
		_flightPosition = _rocket.Position;
		Vector2 radial = (_flightPosition - EarthPosition).Normalized();
		Vector2 tangent = new(-radial.Y, radial.X);
		FlightVelocity = tangent * Mathf.Lerp(MinLaunchSpeed, MaxLaunchSpeed, Charge);
		_rocket.Rotation = tangent.Angle() + Mathf.Pi / 2f;
		Charge = Mathf.Clamp(Charge, 0f, 1f);
	}

	private void ProcessFlight(float delta)
	{
		Vector2 previous = _flightPosition;
		Vector2 acceleration = Vector2.Zero;
		AddGravity(ref acceleration, EarthPosition, EarthGravityRadius);
		AddGravity(ref acceleration, MarsPosition, MarsGravityRadius);
		FlightVelocity += acceleration * delta;
		FlightVelocity *= Mathf.Exp(-SpaceDrag * delta);
		_flightPosition += FlightVelocity * delta;
		DistanceTravelled += previous.DistanceTo(_flightPosition);
		_rocket.Position = _flightPosition;
		if (FlightVelocity.Length() < MinimumFlightSpeed) { SetLost(); return; }

		float marsDistance = _flightPosition.DistanceTo(MarsPosition);
		if (marsDistance <= MarsRadius) { SetWon(); return; }
		if (_flightPosition.DistanceTo(EarthPosition) <= EarthRadius) { SetLost(); return; }

		Rect2 bounds = GetViewportRect().Grow(150f);
		if (!bounds.HasPoint(_flightPosition)) SetLost();
		else if (marsDistance <= MarsGravityRadius && _slowMotionRemaining <= 0f)
		{
			_slowMotionRemaining = 0.4f;
			Engine.TimeScale = 0.35f;
			SetWon();
		}
	}

	private void AddGravity(ref Vector2 acceleration, Vector2 source, float radius)
	{
		Vector2 offset = source - _flightPosition;
		float distance = offset.Length();
		if (distance > radius || distance < 1f) return;
		acceleration += offset / distance * GravityStrength / Mathf.Max(distance * distance, 400f);
	}

	private void SetWon()
	{
		State = FlightState.Won;
		_wonAngle = (_flightPosition - MarsPosition).Angle();
		FlightVelocity = Vector2.Zero;
		_message.Text = "ORBIT REACHED!";
	}

	private void ProcessWon(float delta)
	{
		_wonAngle += OrbitSpeed * 0.25f * delta;
		_rocket.Position = MarsPosition + Vector2.FromAngle(_wonAngle) * (MarsRadius + 32f);
		_rocket.Rotation = _wonAngle + Mathf.Pi / 2f;
	}

	private void SetLost()
	{
		State = FlightState.Lost;
		FlightVelocity = Vector2.Zero;
		_message.Text = "LOST SIGNAL - Espacio para reintentar";
	}

	private void ResetFlight()
	{
		Engine.TimeScale = 1f;
		State = FlightState.Orbiting;
		Charge = 0f;
		DistanceTravelled = 0f;
		_orbitAngle = -Mathf.Pi / 2f;
		_flightPosition = EarthPosition + Vector2.FromAngle(_orbitAngle) * OrbitRadius;
		_message.Text = "";
		_rocket.Position = _flightPosition;
		_rocket.Rotation = 0f;
	}

	private void UpdateHud()
	{
		_chargeLabel.Text = $"CHARGE  {Charge * 100f:000}%";
		_distanceLabel.Text = $"DISTANCE  {DistanceTravelled / AstronomicalUnitPixels:0.00} UA";
		_stateLabel.Text = $"STATE  {State.ToString().ToUpperInvariant()}";
	}

	public override void _Draw()
	{
		DrawCircle(EarthPosition, EarthGravityRadius, new Color(0.25f, 0.72f, 0.95f, 0.12f), false, 2f);
		DrawCircle(MarsPosition, MarsGravityRadius, new Color(0.95f, 0.38f, 0.28f, 0.12f), false, 2f);
		DrawCircle(EarthPosition, EarthRadius, new Color(0.35f, 0.85f, 1f, 0.32f), false, 2f);
		DrawCircle(MarsPosition, MarsRadius, new Color(1f, 0.45f, 0.3f, 0.32f), false, 2f);
		if (State == FlightState.Orbiting)
		{
			Vector2 radial = (_rocket.Position - EarthPosition).Normalized();
			Vector2 tangent = new(-radial.Y, radial.X);
			Vector2 start = _rocket.Position + tangent * 14f;
			Vector2 end = start + tangent * (34f + Charge * 28f);
			for (int i = 0; i < 5; i++)
				DrawLine(start + tangent * i * 12f, start + tangent * (i * 12f + 7f), Colors.White, 3f);
			DrawLine(end, end - tangent * 9f + radial * 7f, Colors.White, 3f);
			DrawLine(end, end - tangent * 9f - radial * 7f, Colors.White, 3f);
		}
	}
}

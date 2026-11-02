using Ares.Mission;
using Godot;

namespace Ares.Stages;

// Rover stage rules (design doc, Scene 3). Rover.cs moves the rover; this script owns the outcome.
public partial class RoverStage : Node2D
{
	private const float PixelsPerMeter = 2f; // display only
	private const string MenuScene = "res://scenes/main_menu/main_menu_take3.tscn";

	[Export] public float BaseLag { get; set; } = 0.5f;

	[Export] public float LagPerImpact { get; set; } = 0.2f;

	[Export] public int MaxImpacts { get; set; } = 5;

	private Rover _rover;
	private Area2D _goal;
	private Label _lagLabel;
	private Label _impactsLabel;
	private Label _distanceLabel;
	private Control _resultPanel;
	private Label _resultLabel;

	public override void _Ready()
	{
		_rover = GetNode<Rover>("Rover");
		_goal = GetNode<Area2D>("OperationsCenter");
		_lagLabel = GetNode<Label>("Hud/Telemetry/LagLabel");
		_impactsLabel = GetNode<Label>("Hud/Telemetry/ImpactsLabel");
		_distanceLabel = GetNode<Label>("Hud/Telemetry/DistanceLabel");
		_resultPanel = GetNode<Control>("Hud/Result");
		_resultLabel = GetNode<Label>("Hud/Result/Box/ResultLabel");

		MissionState.Instance.Stage = MissionStage.Rover;
		MissionState.Instance.RoverCollisions = 0;
		_rover.LagSeconds = BaseLag;

		_rover.Collided += OnRoverCollided;
		_goal.BodyEntered += OnGoalEntered;
		GetNode<Button>("Hud/Result/Box/ContinueButton").Pressed += OnContinuePressed;

		_resultPanel.Visible = false;
		UpdateHud();
	}

	public override void _Process(double delta)
	{
		float meters = _rover.GlobalPosition.DistanceTo(_goal.GlobalPosition) / PixelsPerMeter;
		_distanceLabel.Text = $"CENTRAL {meters:0} M";
	}

	private void OnRoverCollided()
	{
		MissionState state = MissionState.Instance;
		state.RoverCollisions++;
		_rover.LagSeconds = BaseLag + LagPerImpact * state.RoverCollisions;
		GD.Print($"[RoverStage] impact {state.RoverCollisions}/{MaxImpacts}, lag {_rover.LagSeconds:0.00}s");
		UpdateHud();

		if (state.RoverCollisions >= MaxImpacts)
			Finish($"Rover fuera de servicio: {MaxImpacts} impactos.\nConsejo: cada impacto aumenta el retraso de control. Suelta la tecla antes de llegar al obstaculo.");
	}

	private void OnGoalEntered(Node2D body)
	{
		if (body != _rover)
			return;

		Finish($"Central de operaciones localizada.\nImpactos: {MissionState.Instance.RoverCollisions}.");
	}

	private void Finish(string text)
	{
		_rover.Velocity = Vector2.Zero;
		_rover.SetPhysicsProcess(false);
		_resultLabel.Text = text;
		_resultPanel.Visible = true;
	}

	private void UpdateHud()
	{
		_lagLabel.Text = $"LAG {_rover.LagSeconds:0.00} S";
		_impactsLabel.Text = $"IMPACTOS {MissionState.Instance.RoverCollisions}/{MaxImpacts}";
	}

	private void OnContinuePressed()
	{
		GetTree().ChangeSceneToFile(MenuScene);
	}
}

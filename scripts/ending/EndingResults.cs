namespace ares.scripts.ending;

using Ares.Systems.Controllers;
using Godot;
using ares.scripts.mission;

public partial class EndingResults : Control, IControllerCommandSink, IControllerContextSource
{
	private ProgressBar _oxygen;
	private ProgressBar _hull;
	private ProgressBar _energy;
	private ProgressBar _missionBar;
	private Label _oxygenValue;
	private Label _hullValue;
	private Label _energyValue;
	private Label _missionValue;
	private Label _total;
	private Button _return;
	private MissionState _state;

	public override void _Ready()
	{
		_state = MissionState.Instance;
		_oxygen = GetNode<ProgressBar>("%OxygenBar");
		_hull = GetNode<ProgressBar>("%HullBar");
		_energy = GetNode<ProgressBar>("%EnergyBar");
		_missionBar = GetNode<ProgressBar>("%MissionBar");
		_oxygenValue = GetNode<Label>("%OxygenValue");
		_hullValue = GetNode<Label>("%HullValue");
		_energyValue = GetNode<Label>("%EnergyValue");
		_missionValue = GetNode<Label>("%MissionValue");
		_total = GetNode<Label>("%TotalScore");

		if (_state != null)
		{
			var score = _state.Scoring;
			SetScore(_oxygen, _oxygenValue, score.Resources);
			SetScore(_hull, _hullValue, score.Repairs);
			SetScore(_energy, _energyValue, score.Launch);
			SetScore(_missionBar, _missionValue, score.Overall);
			_total.Text = $"{(score.Passed ? "MISSION PASS" : "MISSION FAILED")}  {score.Overall:0}%";
		}
		else
		{
			_total.Text = "NO MISSION RESULT RECORDED";
		}

		_return = new Button { Text = "RETURN TO MISSION CONTROL", CustomMinimumSize = new Vector2(360, 48) };
		_return.SetAnchorsPreset(LayoutPreset.BottomRight);
		_return.OffsetLeft = -390;
		_return.OffsetTop = -74;
		_return.OffsetRight = -24;
		_return.OffsetBottom = -24;
		_return.Pressed += ReturnToMenu;
		AddChild(_return);
		_return.GrabFocus();
		ControllerCommandRouter.Register(this, this);
		ControllerContext.Begin(this, ControllerContextMode.Ending);
		PublishControllerContext();
	}

	private static void SetScore(ProgressBar bar, Label label, double value)
	{
		bar.Value = Mathf.Clamp((float)value, 0, 100);
		label.Text = $"{value:0}%";
	}

	private void ReturnToMenu() =>
		GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu_take3.tscn");

	public bool HandleControllerCommand(ControllerCommand command)
	{
		if (command.Name != ControllerCommands.EndingContinue) return false;
		ReturnToMenu();
		return true;
	}

	public void PublishControllerContext() =>
		ControllerContext.Update(this, new Godot.Collections.Dictionary
		{
			["phase"] = "results",
			["can_continue"] = true,
			["can_skip"] = false,
		});

	public override void _ExitTree()
	{
		ControllerCommandRouter.Unregister(this);
		ControllerContext.End(this);
	}
}

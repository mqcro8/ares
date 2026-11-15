namespace ares.scripts.ending;

using Ares.Systems.Controllers;
using Godot;
using ares.scripts.mission;

public partial class EndingResults : Control, IControllerCommandSink, IControllerContextSource
{
	private Control _board;
	private Label _total;
	private Label _reason;
	private Button _return;
	private MissionState _state;
	public string ScoreReason => _reason?.Text ?? "";

	public override void _Ready()
	{
		_state = MissionState.Instance;
		_board = GetNode<Control>("Center/Board");
		_total = GetNode<Label>("%TotalScore");
		HideLegacyRows();

		if (_state != null)
		{
			var score = _state.Scoring;
			BuildScoreBreakdown(score);
			_total.Text = $"{BuildResultTitle(score)}  {score.Overall:0}%";
		}
		else
		{
			_total.Text = "NO MISSION RESULT RECORDED";
			BuildScoreBreakdown(null);
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

	private void HideLegacyRows()
	{
		foreach (string path in new[]
		{
			"OxygenBar", "OxygenValue", "HullBar", "HullValue",
			"EnergyBar", "EnergyValue", "MissionBar", "MissionValue"
		})
			_board.GetNodeOrNull<CanvasItem>(path)?.Hide();
	}

	private void BuildScoreBreakdown(MissionScoring score)
	{
		var panel = new PanelContainer
		{
			Name = "ScoreBreakdown",
			Position = new Vector2(86, 132),
			Size = new Vector2(980, 330),
			CustomMinimumSize = new Vector2(980, 330),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.04f, 0.06f, 0.98f),
			BorderColor = new Color(0.25f, 0.48f, 0.55f, 1),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
		});
		_board.AddChild(panel);

		var margin = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 14);
		panel.AddChild(margin);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 5);
		margin.AddChild(column);
		var heading = MakeLabel("MISSION BREAKDOWN", new Color(0.55f, 0.9f, 1f), 11);
		column.AddChild(heading);

		double training = score?.Training ?? 0;
		double launch = score?.Launch ?? 0;
		double repairs = score?.Repairs ?? 0;
		double rover = score?.Rover ?? 0;
		double resources = score?.Resources ?? 0;
		AddRow(column, "TRAINING", training);
		AddRow(column, "LAUNCH", launch);
		AddRow(column, "SURFACE REPAIRS", repairs);
		AddRow(column, "ROVER", rover);
		AddRow(column, "RESOURCES", resources);
		_reason = MakeLabel(BuildScoreReason(score), new Color(0.95f, 0.76f, 0.43f), 8);
		_reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_reason.ClipText = false;
		_reason.CustomMinimumSize = new Vector2(0, 30);
		column.AddChild(_reason);
	}

	private static void AddRow(VBoxContainer column, string title, double value)
	{
		var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
		row.AddThemeConstantOverride("separation", 12);
		column.AddChild(row);
		var label = MakeLabel(title, Colors.White, 8);
		label.CustomMinimumSize = new Vector2(176, 22);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);
		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 100,
			Value = Mathf.Clamp((float)value, 0, 100),
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 16),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("18243b") });
		bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("75e4ff") });
		row.AddChild(bar);
		var valueLabel = MakeLabel($"{value:0}%", new Color(0.83f, 0.67f, 0.4f), 8);
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		valueLabel.CustomMinimumSize = new Vector2(66, 22);
		row.AddChild(valueLabel);
	}

	private string BuildScoreReason(MissionScoring score)
	{
		if (_state == null || score == null) return "NO MISSION RESULT RECORDED.";
		if (_state.LaunchFailed) return "LAUNCH FAILED BEFORE DEPARTURE.";
		if (score.FinalOxygenSeconds <= 0) return "SUIT OXYGEN DEPLETED BEFORE SAFE RETURN.";
		if (_state.RoverStageResolved && !_state.RoverStageCompleted) return "OPPORTUNITY LOST — ROVER OBJECTIVE FAILED.";
		if (_state.LaunchRecord == null) return "LAUNCH OBJECTIVE NOT COMPLETED.";
		if (!_state.AllPuzzlesComplete) return "SURFACE REPAIRS REMAIN INCOMPLETE.";
		if (!score.HasReturnReserve) return "RETURN OXYGEN RESERVE NOT SECURED.";
		if (!score.AllMissionsWon) return "ALL REQUIRED MISSION OBJECTIVES MUST BE WON.";
		if (score.Overall < MissionScoring.PassingGrade) return "TOTAL SCORE IS BELOW THE 80% PASS MARK.";
		return "ALL OBJECTIVES COMPLETE — RETURN RESERVE SECURED.";
	}

	private string BuildResultTitle(MissionScoring score)
	{
		if (_state.Ending == MissionEnding.Bad) return "MISSION FAILED";
		if (!score.AllMissionsWon) return "RETURN COMPLETE — OBJECTIVES NOT ALL WON";
		if (!score.HasReturnReserve) return "RETURN RESERVE FAILED";
		if (score.Overall < MissionScoring.PassingGrade) return "MISSION COMPLETE — BELOW PASS MARK";
		return "MISSION PASS";
	}

	private static Label MakeLabel(string text, Color color, int size)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeFontSizeOverride("font_size", size);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) label.AddThemeFontOverride("font", font);
		return label;
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

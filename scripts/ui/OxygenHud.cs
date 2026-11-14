namespace ares.scripts.ui;

using System;
using Godot;
using ares.scripts.mission;

/// <summary>Surface-only oxygen display, return warning and emergency extraction presentation.</summary>
public partial class OxygenHud : CanvasLayer
{
	private static readonly Color Cyan = new(0.46f, 0.89f, 0.96f);
	private static readonly Color Amber = new(1f, 0.72f, 0.24f);
	private static readonly Color Red = new(1f, 0.12f, 0.08f);
	private MissionState _mission;
	private ColorRect _criticalFlash;
	private Label _oxygenText, _criticalText, _returnText, _messageText;
	private ProgressBar _bar;
	private StyleBoxFlat _barFill;
	private Control _failureOverlay;
	private Button _failureButton;
	private bool _failed;
	private double _flashClock;
	private double _messageClock;

	[Export] public bool ActivateOnReady { get; set; } = true;
	[Export] public string ExtractionScene { get; set; } = "res://scenes/main_menu/main_menu_take3.tscn";
	public bool IsCritical => _mission?.Oxygen.IsCritical == true;
	public bool IsFailureVisible => _failureOverlay?.Visible == true;
	public bool IsCriticalWarningVisible => _criticalText?.Visible == true;
	public bool IsReturnWarningVisible => _returnText?.Visible == true;
	public bool IsEmergencyVisible => _failureOverlay?.Visible == true;
	public bool IsMessageVisible => _messageText?.Visible == true;
	public string MessageText => _messageText?.Text ?? "";
	public float CriticalFlashAlpha => _criticalFlash?.Color.A ?? 0;

	public override void _Ready()
	{
		Layer = 25;
		BuildHud();
		_mission = MissionState.Instance;
		if (_mission == null)
		{
			GD.PushError("OxygenHud requires MissionState autoload.");
			return;
		}
		_mission.Oxygen.Changed += Refresh;
		_mission.Oxygen.Depleted += OnDepleted;
		_mission.OxygenMessageChanged += RefreshMessage;
		if (ActivateOnReady) _mission.BeginMarsSurface();
		Refresh();
		RefreshMessage();
	}

	public override void _Process(double delta)
	{
		if (_mission == null) return;
		_mission.Oxygen.Advance(delta);
		RefreshCriticalFlash(delta);
		if (_messageText != null && _messageText.Visible)
		{
			_messageClock -= delta;
			if (_messageClock <= 0)
			{
				_messageText.Visible = false;
				_mission.SetOxygenMessage("");
			}
		}
	}

	private void BuildHud()
	{
		_criticalFlash = new ColorRect
		{
			Name = "CriticalRedFlash",
			Color = new Color(1, 0, 0, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_criticalFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_criticalFlash);

		var panel = new PanelContainer { Name = "OxygenPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
		panel.OffsetLeft = 16;
		panel.OffsetTop = 16;
		panel.OffsetRight = 276;
		panel.OffsetBottom = 118;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.045f, 0.065f, 0.94f),
			BorderColor = new Color(0.24f, 0.47f, 0.54f, 0.95f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
		});
		AddChild(panel);
		var margin = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 8);
		panel.AddChild(margin);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 5);
		margin.AddChild(column);
		_oxygenText = MakeLabel("O2 —", Colors.White, 8);
		column.AddChild(_oxygenText);
		_bar = new ProgressBar
		{
			Name = "OxygenBar",
			CustomMinimumSize = new Vector2(0, 12),
			MinValue = 0,
			MaxValue = 100,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.07f, 0.12f, 0.16f, 1) });
		_barFill = new StyleBoxFlat { BgColor = Cyan };
		_bar.AddThemeStyleboxOverride("fill", _barFill);
		column.AddChild(_bar);
		_criticalText = MakeLabel("", Red, 7);
		_criticalText.Name = "CriticalWarning";
		_criticalText.Visible = false;
		column.AddChild(_criticalText);
		_returnText = MakeLabel("", Amber, 7);
		_returnText.Name = "ReturnWarning";
		_returnText.Visible = false;
		column.AddChild(_returnText);
		_messageText = MakeLabel("", Cyan, 7);
		_messageText.Name = "PickupMessage";
		_messageText.Visible = false;
		column.AddChild(_messageText);

		var dimmer = new ColorRect
		{
			Color = new Color(0.01f, 0.015f, 0.025f, 0.88f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always,
		};
		dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(dimmer);
		_failureOverlay = dimmer;
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		dimmer.AddChild(center);
		var failurePanel = new PanelContainer { CustomMinimumSize = new Vector2(380, 170) };
		failurePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.025f, 0.025f, 1),
			BorderColor = Red,
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
		});
		center.AddChild(failurePanel);
		var failureMargin = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			failureMargin.AddThemeConstantOverride("margin_" + side, 14);
		failurePanel.AddChild(failureMargin);
		var failureColumn = new VBoxContainer();
		failureColumn.Alignment = BoxContainer.AlignmentMode.Center;
		failureColumn.AddThemeConstantOverride("separation", 12);
		failureMargin.AddChild(failureColumn);
		var title = MakeLabel("EMERGENCY RETURN", Red, 11);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		failureColumn.AddChild(title);
		var cause = MakeLabel("OXYGEN DEPLETED — RETURN TO THE LANDER", Colors.White, 8);
		cause.HorizontalAlignment = HorizontalAlignment.Center;
		failureColumn.AddChild(cause);
		_failureButton = new Button { Text = "EMERGENCY EXTRACTION" };
		_failureButton.Pressed += ReturnToMissionControl;
		failureColumn.AddChild(_failureButton);
	}

	private static Label MakeLabel(string text, Color color, int size)
	{
		var label = new Label
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipText = true,
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeFontSizeOverride("font_size", size);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) label.AddThemeFontOverride("font", font);
		return label;
	}

	private void Refresh()
	{
		if (_mission == null || _oxygenText == null) return;
		var oxygen = _mission.Oxygen;
		var remaining = System.TimeSpan.FromSeconds(oxygen.RemainingSeconds);
		var capacity = System.TimeSpan.FromSeconds(oxygen.CapacitySeconds);
		_oxygenText.Text = $"O2  {remaining.Minutes:D2}:{remaining.Seconds:D2} / {capacity.Minutes:D2}:{capacity.Seconds:D2}  ({oxygen.Fraction:P0})";
		_bar.Value = oxygen.Fraction * 100;
		_barFill.BgColor = oxygen.IsCritical ? Red : oxygen.IsReturnReserveLow ? Amber : Cyan;
		_criticalText.Visible = oxygen.IsCritical;
		if (oxygen.IsCritical)
			_criticalText.Text = $"CRITICAL OXYGEN — {Math.Ceiling(oxygen.Fraction * 100):F0}%";
		_returnText.Visible = oxygen.IsReturnReserveLow;
		if (oxygen.IsReturnReserveLow)
			_returnText.Text = "RETURN RESERVE LOW — HEAD BACK";
	}

	private void RefreshMessage()
	{
		if (_messageText == null) return;
		string message = _mission?.OxygenMessage ?? "";
		_messageText.Visible = message != "";
		if (message == "") return;
		_messageText.Text = message;
		_messageClock = 3.0;
	}

	private void RefreshCriticalFlash(double delta)
	{
		if (_mission.Oxygen.IsCritical)
		{
			_flashClock += delta;
			float pulse = (Mathf.Sin((float)_flashClock * 8f) + 1f) * 0.5f;
			_criticalFlash.Color = new Color(1, 0, 0, 0.04f + pulse * 0.16f);
			_criticalText.Modulate = new Color(1, 0.2f + pulse * 0.3f, 0.15f, 0.55f + pulse * 0.45f);
		}
		else
		{
			_criticalFlash.Color = new Color(1, 0, 0, 0);
			_criticalText.Modulate = Colors.White;
		}
	}

	private void OnDepleted()
	{
		if (_failed) return;
		_failed = true;
		_criticalFlash.Color = new Color(1, 0, 0, 0);
		_criticalText.Visible = false;
		_returnText.Visible = false;
		_failureOverlay.Visible = true;
		_failureButton?.GrabFocus();
	}

	private void ReturnToMissionControl()
	{
		if (GetTree().ChangeSceneToFile(ExtractionScene) != Error.Ok)
			GD.PushError("OxygenHud could not start the emergency extraction.");
	}

	public override void _ExitTree()
	{
		if (_mission == null) return;
		_mission.Oxygen.Changed -= Refresh;
		_mission.Oxygen.Depleted -= OnDepleted;
		_mission.OxygenMessageChanged -= RefreshMessage;
		_mission.EndMarsSurface();
	}
}

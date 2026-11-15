namespace ares.scripts.systems;

using System;
using System.Text;
using Godot;
using ares.scripts.mission;
using ares.scripts.rover;

public interface IDevSceneSkipTarget
{
	bool CanSkipWithDevCode { get; }
	bool SkipWithDevCode();
}

/// <summary>Hidden developer shortcuts. Type the code with Shift held so it never fights gameplay keys.</summary>
public partial class DevCheats : Node
{
	/// <summary>Completes every training exercise.</summary>
	public const string SkipTrainingCode = "ARES";
	/// <summary>Clears the training progress again.</summary>
	public const string ResetTrainingCode = "ERASE";

	private const double Timeout = 2.5;
	private readonly StringBuilder _buffer = new();
	private double _lastKeyTime;
	private Label _toast;
	private double _toastClock;

	public bool Enabled { get; set; } = true;
	public string Buffer => _buffer.ToString();
	public bool IsToastVisible => _toast != null && _toast.Visible;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		var layer = new CanvasLayer { Name = "DevCheatLayer", Layer = 120 };
		AddChild(layer);
		_toast = new Label
		{
			Name = "Toast",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_toast.AddThemeFontSizeOverride("font_size", 12);
		_toast.AddThemeColorOverride("font_color", new Color(0.46f, 0.89f, 0.96f));
		_toast.AddThemeColorOverride("font_outline_color", Colors.Black);
		_toast.AddThemeConstantOverride("outline_size", 4);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) _toast.AddThemeFontOverride("font", font);
		_toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		_toast.Position = new Vector2(-260, -40);
		_toast.Size = new Vector2(520, 80);
		layer.AddChild(_toast);
	}

	public override void _Process(double delta)
	{
		if (_toast == null || !_toast.Visible) return;
		_toastClock -= delta;
		if (_toastClock <= 0) _toast.Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Enabled) return;
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
		{
			_buffer.Clear();
			return;
		}
		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastKeyTime > Timeout) _buffer.Clear();
		_lastKeyTime = now;

		if (!key.ShiftPressed || !IsLetter(key.Keycode))
		{
			_buffer.Clear();
			return;
		}
		GetViewport().SetInputAsHandled();

		_buffer.Append(((char)('A' + (key.Keycode - (int)Key.A))).ToString());
		if (_buffer.Length > 16) _buffer.Remove(0, _buffer.Length - 16);
		CheckCodes();
	}

	/// <summary>Evaluates the current buffer against the known codes.</summary>
	public void CheckCodes()
	{
		string buffer = _buffer.ToString();
		if (buffer.EndsWith(SkipTrainingCode, StringComparison.Ordinal))
		{
			_buffer.Clear();
			ApplySkip();
			return;
		}
		if (buffer.EndsWith(ResetTrainingCode, StringComparison.Ordinal))
		{
			_buffer.Clear();
			ApplyReset();
		}
	}

	private static bool IsLetter(Key keycode) => keycode >= Key.A && keycode <= Key.Z;

	private void ApplySkip()
	{
		var mission = MissionState.Instance;
		mission?.CompleteAllTraining();
		long currentRun = mission?.LaunchSequence ?? 0;
		bool skipped = TrySkipActiveScene(GetTree().CurrentScene);
		if (!skipped && GetTree().CurrentScene is IRoverMissionSkipTarget rover &&
			rover.CanSkipRoverMission)
			skipped = rover.SkipRoverMission();
		if (mission != null && mission.LaunchSequence != currentRun)
			mission.CompleteAllTraining();
		ShowToast(skipped ? $"SCENE SKIPPED — {SkipTrainingCode}" : $"TRAINING SKIPPED — {SkipTrainingCode}");
	}

	private static bool TrySkipActiveScene(Node root)
	{
		if (root == null) return false;
		if (root is IDevSceneSkipTarget target && target.CanSkipWithDevCode)
			return target.SkipWithDevCode();
		foreach (Node child in root.GetChildren())
			if (TrySkipActiveScene(child)) return true;
		return false;
	}

	private void ApplyReset()
	{
		MissionState.Instance?.ResetTrainingProgress();
		ShowToast($"TRAINING RESET — {ResetTrainingCode}");
	}

	public void ShowToast(string message)
	{
		if (_toast == null) return;
		_toast.Text = message;
		_toast.Visible = true;
		_toastClock = 2.5;
	}
}

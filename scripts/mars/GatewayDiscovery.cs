namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.training;

/// <summary>Modal archive reveal, shown once after Gateway communications are restored.</summary>
public partial class GatewayDiscovery : CanvasLayer, ares.scripts.systems.IDevSceneSkipTarget
{
	private const string SequencePath = "res://data/dialogue/gateway_discovery.tres";
	private const string CircuitImagePath = "res://art/cinematics/Mars Gateway Circuit Puzzle-2.png";
	private const string CommunicationsImagePath = "res://art/cinematics/Retro Mars base communications room.png";

	private MissionState _mission;
	private DialogueSequence _sequence;
	private Player _player;
	private TrainingPlayerLock _playerLock;
	private Control _overlay;
	private TextureRect _image;
	private DialogueView _dialogue;
	private Button _continue, _skip;
	private int _line;
	private bool _open;
	private bool _hasStaminaSnapshot;
	private float _staminaBefore, _staminaRestBefore;

	public bool IsOpen => _open;
	public bool CanSkipWithDevCode => IsInsideTree() && _open;

	public override void _Ready()
	{
		Layer = 60;
		ProcessMode = ProcessModeEnum.Pausable;
		_mission = MissionState.Instance;
		_playerLock = new TrainingPlayerLock();
		_sequence = GD.Load<DialogueSequence>(SequencePath);
		BuildUi();
		if (_mission == null)
		{
			GD.PushError("GatewayDiscovery requires MissionState.");
			return;
		}
		_mission.SignalTableRepairedChanged += OnSignalTableRepaired;
		_mission.MissionEnded += OnMissionEnded;
		if (_mission.GatewayDiscoveryPending && !_mission.IsRunOver) ShowPendingDiscovery();
	}

	private void BuildUi()
	{
		_overlay = new Control
		{
			Name = "DiscoveryOverlay",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_overlay);

		_image = new TextureRect
		{
			Name = "Image",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_overlay.AddChild(_image);

		var shade = new ColorRect
		{
			Name = "Shade",
			Color = new Color(0.005f, 0.012f, 0.028f, 0.32f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_overlay.AddChild(shade);

		_dialogue = new DialogueView { Name = "Dialogue", Visible = false, ZIndex = 2 };
		_overlay.AddChild(_dialogue);

		var actions = new HBoxContainer
		{
			Name = "Actions",
			Alignment = BoxContainer.AlignmentMode.End,
			ZIndex = 4,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		actions.AddThemeConstantOverride("separation", 10);
		actions.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		actions.OffsetLeft = -360;
		actions.OffsetTop = 16;
		actions.OffsetRight = -20;
		actions.OffsetBottom = 58;
		_overlay.AddChild(actions);

		_skip = new Button { Name = "Skip", Text = "SKIP REVEAL", CustomMinimumSize = new Vector2(170, 42) };
		_skip.Pressed += FinishReveal;
		actions.AddChild(_skip);
		_continue = new Button { Name = "Continue", Text = "CONTINUE", CustomMinimumSize = new Vector2(170, 42) };
		_continue.Pressed += Advance;
		actions.AddChild(_continue);
	}

	private void OnSignalTableRepaired(bool repaired)
	{
		if (repaired && _mission?.GatewayDiscoveryPending == true && _mission.IsRunOver == false)
			ShowPendingDiscovery();
	}

	public void ShowPendingDiscovery()
	{
		if (_open || _mission?.GatewayDiscoveryPending != true || _mission.IsRunOver ||
			_sequence?.Lines.Count == 0) return;
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		_staminaBefore = _mission.Stamina;
		_staminaRestBefore = _mission.StaminaRestSeconds;
		_hasStaminaSnapshot = true;
		if (_player != null) _playerLock.Acquire(_player);
		GetParent()?.GetNodeOrNull<ObjectiveDialogue>("ObjectiveDialogue")?.Dismiss();
		_line = 0;
		_open = true;
		_overlay.Show();
		ShowCurrentLine();
	}

	private void ShowCurrentLine()
	{
		if (_sequence == null || _line < 0 || _line >= _sequence.Lines.Count) return;
		_image.Texture = _line < 2
			? GD.Load<Texture2D>(CircuitImagePath)
			: GD.Load<Texture2D>(CommunicationsImagePath);
		_dialogue.ShowLine(_sequence.Lines[_line]);
		_continue.Text = _line == _sequence.Lines.Count - 1 ? "ACKNOWLEDGE REPORT" : "CONTINUE";
		_continue.GrabFocus();
	}

	private void Advance()
	{
		if (!_open) return;
		_line++;
		if (_line >= (_sequence?.Lines.Count ?? 0))
		{
			FinishReveal();
			return;
		}
		ShowCurrentLine();
	}

	private void FinishReveal()
	{
		if (!_open) return;
		if (_mission?.IsRunOver == true)
		{
			AbortAfterMissionFailure();
			return;
		}
		_open = false;
		_mission?.AcknowledgeGatewayDiscovery();
		_dialogue.Clear();
		_overlay.Hide();
		_playerLock.Release();
		RestoreStamina();
		GetParent()?.GetNodeOrNull<ObjectiveDialogue>("ObjectiveDialogue")?.ResumeAfterGatewayDiscovery();
	}

	private void OnMissionEnded(MissionEnding _)
	{
		if (_mission?.IsRunOver == true) AbortAfterMissionFailure();
	}

	private void AbortAfterMissionFailure()
	{
		if (!_open) return;
		_open = false;
		_dialogue.Clear();
		_overlay.Hide();
		_playerLock.Release();
		RestoreStamina();
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		FinishReveal();
		return true;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!_open || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode is not (Key.Enter or Key.KpEnter or Key.Space or Key.Escape)) return;
		GetViewport().SetInputAsHandled();
		if (key.Keycode == Key.Escape) FinishReveal();
		else Advance();
	}

	public override void _ExitTree()
	{
		if (_mission != null) _mission.SignalTableRepairedChanged -= OnSignalTableRepaired;
		if (_mission != null) _mission.MissionEnded -= OnMissionEnded;
		_playerLock?.Release();
		RestoreStamina();
	}

	private void RestoreStamina()
	{
		if (!_hasStaminaSnapshot || _mission == null) return;
		_hasStaminaSnapshot = false;
		_mission.RestoreStamina(_staminaBefore, _staminaRestBefore);
	}
}

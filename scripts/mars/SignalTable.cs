namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;
using Ares.Systems.Multiplayer;

/// <summary>
/// Large signal table in the third building. Opens the waveform/frequency
/// calibration mini-game once; the completed state is stored in MissionState.
/// </summary>
public partial class SignalTable : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/FrequencyBoxMinigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _calibrated;
	private bool _lockPending;
	private TeamSession _team;
	private MinigameOverlay _overlay;
	private const string TaskId = "mars.signal_table";
	private string _defaultPrompt;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_team = TeamSession.Instance;
		_defaultPrompt = PromptText;
		MissionState.Instance.SignalTableRepairedChanged += OnSignalTableRepairedChanged;
		if (_team != null) { _team.TeamChanged += OnTeamChanged; OnTeamChanged(); }
		_calibrated = MissionState.Instance?.SignalTableRepaired ?? false;
		ApplyState();
	}

	protected override void Interact()
	{
		if (_open || _calibrated || _lockPending)
			return;

		if (_team?.Active == true && _team.MissionStarted)
		{
			_lockPending = true;
			HidePrompt();
			_team.RequestTaskLock(TaskId, (granted, owner) =>
			{
				if (!GodotObject.IsInstanceValid(this)) return;
				_lockPending = false;
				if (granted) OpenCalibration();
				else
				{
					PromptText = owner >= 0
						? $"IN USE — PLAYER {owner + 1} IS CALIBRATING"
						: _defaultPrompt;
					ShowPrompt();
				}
			});
			return;
		}
		OpenCalibration();
	}

	private void OpenCalibration()
	{
		if (_calibrated) { _team?.ReleaseTaskLock(TaskId); return; }
		_overlay = MinigameOverlay.Open(this, NearbyPlayer, MinigameScene, "SIGNAL CALIBRATION",
			"Match the target waveform with the three knobs.  1/2/3 selects, arrows adjust.",
			"Calibrated!");
		if (_overlay == null) { _team?.ReleaseTaskLock(TaskId); return; }

		_open = true;
		HidePrompt();
		_overlay.Finished += solved =>
		{
			_overlay = null;
			_team?.ReleaseTaskLock(TaskId);
			_open = false;
			if (solved)
				Calibrate();
			else
				ShowPrompt();
		};
	}

	private void OnTeamChanged()
	{
		if (_team == null) return;
		int owner = _team.TaskLockOwner(TaskId);
		if (owner >= 0 && owner != _team.LocalSlot)
		{
			PromptText = $"IN USE — PLAYER {owner + 1} IS CALIBRATING";
			ShowPrompt();
		}
		else if (owner < 0)
		{
			PromptText = _defaultPrompt;
			if (!_open && !_calibrated) ShowPrompt();
		}
	}

	private void Calibrate()
	{
		_calibrated = true;
		Enabled = false;
		HidePrompt();
		_sprite.Stop();
		_sprite.Modulate = new Color(0.72f, 1f, 0.8f);
		MissionState.Instance?.MarkSignalTableRepaired();
	}

	private void ApplyState()
	{
		Enabled = !_calibrated;
		if (_calibrated)
		{
			HidePrompt();
			_sprite.Stop();
			_sprite.Modulate = new Color(0.72f, 1f, 0.8f);
			return;
		}

		_sprite.Play("default");
	}

	private void OnSignalTableRepairedChanged(bool repaired)
	{
		if (!repaired || _calibrated) return;
		_calibrated = true;
		_overlay?.CloseFromSharedCompletion();
		ApplyState();
	}

	public override void _ExitTree()
	{
		if (MissionState.Instance != null)
			MissionState.Instance.SignalTableRepairedChanged -= OnSignalTableRepairedChanged;
		if (_team != null)
		{
			_team.TeamChanged -= OnTeamChanged;
			if (_team.TaskLockOwner(TaskId) == _team.LocalSlot) _team.ReleaseTaskLock(TaskId);
		}
	}
}

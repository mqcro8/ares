namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;
using Ares.Systems.Multiplayer;

/// <summary>
/// Generator the player repairs through the fuse-box mini-game.
/// Solving it switches the generator from the idle frame to the looping on frames.
/// </summary>
public partial class Generator : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string RepairMinigame { get; set; } = "res://scenes/minigames/FuseBoxMinigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _on;
	private bool _repairing;
	private bool _lockPending;
	private TeamSession _team;
	private MinigameOverlay _overlay;
	private const string TaskId = "mars.generator";
	private string _defaultPrompt;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_team = TeamSession.Instance;
		_defaultPrompt = PromptText;
		MissionState.Instance.GeneratorRepairedChanged += OnGeneratorRepairedChanged;
		if (_team != null) { _team.TeamChanged += OnTeamChanged; OnTeamChanged(); }
		_on = MissionState.Instance?.GeneratorRepaired ?? false;
		Enabled = !_on;
		if (_on)
			HidePrompt();
		_sprite.Play(_on ? "on" : "off");
	}

	protected override void Interact()
	{
		if (_on || _repairing || _lockPending)
			return;

		if (_team?.Active == true && _team.MissionStarted)
		{
			_lockPending = true;
			HidePrompt();
			_team.RequestTaskLock(TaskId, (granted, owner) =>
			{
				if (!GodotObject.IsInstanceValid(this)) return;
				_lockPending = false;
				if (granted) OpenRepair();
				else
				{
					PromptText = owner >= 0
						? $"IN USE — PLAYER {owner + 1} IS REPAIRING"
						: _defaultPrompt;
					ShowPrompt();
				}
			});
			return;
		}
		OpenRepair();
	}

	private void OpenRepair()
	{
		if (_on) { _team?.ReleaseTaskLock(TaskId); return; }
		_overlay = MinigameOverlay.Open(this, NearbyPlayer, RepairMinigame, "GENERATOR REPAIR",
			"Turn all six switches ON within four presses.  Keys 1-6 or click.");
		if (_overlay == null) { _team?.ReleaseTaskLock(TaskId); return; }

		_repairing = true;
		HidePrompt();
		_overlay.Finished += solved =>
		{
			_overlay = null;
			_team?.ReleaseTaskLock(TaskId);
			_repairing = false;
			if (solved)
				PowerOn();
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
			PromptText = $"IN USE — PLAYER {owner + 1} IS REPAIRING";
			ShowPrompt();
		}
		else if (owner < 0)
		{
			PromptText = _defaultPrompt;
			if (!_repairing && !_on) ShowPrompt();
		}
	}

	private void PowerOn()
	{
		_on = true;
		Enabled = false;
		HidePrompt();
		_sprite.Play("on");
		MissionState.Instance?.MarkGeneratorRepaired();
	}

	private void OnGeneratorRepairedChanged(bool repaired)
	{
		if (!repaired || _on) return;
		_on = true;
		_overlay?.CloseFromSharedCompletion();
		Enabled = false;
		HidePrompt();
		_sprite.Play("on");
	}

	public override void _ExitTree()
	{
		if (MissionState.Instance != null)
			MissionState.Instance.GeneratorRepairedChanged -= OnGeneratorRepairedChanged;
		if (_team != null)
		{
			_team.TeamChanged -= OnTeamChanged;
			if (_team.TaskLockOwner(TaskId) == _team.LocalSlot) _team.ReleaseTaskLock(TaskId);
		}
	}
}

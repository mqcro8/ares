namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;
using Ares.Systems.Multiplayer;

/// <summary>Electrical box that opens the wire minigame in place.</summary>
public partial class ElectricalBox : Interactable
{
	[Export(PropertyHint.File, "*.tscn")]
	public string MinigameScene { get; set; } = "res://scenes/minigames/wireminigame.tscn";

	private AnimatedSprite2D _sprite;
	private bool _open;
	private bool _solved;
	private bool _lockPending;
	private TeamSession _team;
	private MinigameOverlay _overlay;
	private const string TaskId = "mars.wire_box";
	private string _defaultPrompt;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_team = TeamSession.Instance;
		_defaultPrompt = PromptText;
		MissionState.Instance.WireBoxRepairedChanged += OnWireBoxRepairedChanged;
		if (_team != null) { _team.TeamChanged += OnTeamChanged; OnTeamChanged(); }
		_solved = MissionState.Instance?.WireBoxRepaired ?? false;
		_sprite.Play(_solved ? "open" : "closed");
		if (_solved)
		{
			Enabled = false;
			HidePrompt();
		}
	}

	protected override void Interact()
	{
		if (_open || _lockPending)
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
		if (_solved) { _team?.ReleaseTaskLock(TaskId); return; }
		_overlay = MinigameOverlay.Open(this, NearbyPlayer, MinigameScene, "ELECTRICAL BOX",
			"Drag matching colors from left to right.  Keyboard: 1-4, Up/Down, Enter.");
		if (_overlay == null) { _team?.ReleaseTaskLock(TaskId); return; }

		_open = true;
		HidePrompt();
		_overlay.Finished += solved =>
		{
			_overlay = null;
			_team?.ReleaseTaskLock(TaskId);
			_open = false;
			if (solved)
			{
				_solved = true;
				Enabled = false;
				_sprite.Play("open");
				HidePrompt();
				MissionState.Instance?.MarkWireBoxRepaired();
			}
			else
			{
				ShowPrompt();
			}
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
			if (!_open && !_solved) ShowPrompt();
		}
	}

	private void OnWireBoxRepairedChanged(bool repaired)
	{
		if (!repaired || _solved) return;
		_solved = true;
		_overlay?.CloseFromSharedCompletion();
		_open = false;
		Enabled = false;
		_sprite.Play("open");
		HidePrompt();
	}

	public override void _ExitTree()
	{
		if (MissionState.Instance != null)
			MissionState.Instance.WireBoxRepairedChanged -= OnWireBoxRepairedChanged;
		if (_team != null)
		{
			_team.TeamChanged -= OnTeamChanged;
			if (_team.TaskLockOwner(TaskId) == _team.LocalSlot) _team.ReleaseTaskLock(TaskId);
		}
	}
}

namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;

/// <summary>
/// The return-to-start trigger in MarsBase. Finishes the run when every base task is
/// restored; otherwise it tells the player why they cannot leave yet.
/// </summary>
public partial class EndingTrigger : Area2D
{
	[Export(PropertyHint.File, "*.tscn")]
	public string EndingScene { get; set; } = "res://scenes/ending/ending.tscn";

	[Export] public DialogueSequence CommunicationsLocked { get; set; }
	[Export] public DialogueSequence RepairsPending { get; set; }
	[Export] public float MessageSeconds { get; set; } = 4f;

	private DialogueView _dialogue;
	private Node2D _player;
	private bool _armed;
	private bool _showing;
	private bool _changing;
	private double _remaining;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		CommunicationsLocked ??= GD.Load<DialogueSequence>("res://data/dialogue/ending_locked_comms.tres");
		RepairsPending ??= GD.Load<DialogueSequence>("res://data/dialogue/ending_locked_repairs.tres");
		_dialogue = GetNodeOrNull<DialogueView>("DialogueLayer/Dialogue");
		Callable.From(ArmFromInitialOverlap).CallDeferred();
	}

	private async void ArmFromInitialOverlap()
	{
		// Let SceneManager finish placing a player who just returned from a building.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// Ignore only the initial spawn overlap. If the player arrives outside the
		// marker, their first real entry should trigger immediately.
		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_player = player;
		_armed = player == null || !AreaContains(player.GlobalPosition);
	}

	private bool AreaContains(Vector2 point)
	{
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape?.Shape is not RectangleShape2D rect)
			return false;

		Vector2 size = rect.Size * shape.GlobalScale;
		return new Rect2(shape.GlobalPosition - size / 2f, size).HasPoint(point);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_player = body;
		if (!_armed || _showing || _changing)
			return;

		if (MissionState.Instance?.AllPuzzlesComplete == true)
			BeginEnding();
		else
			ShowLockedMessage();
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_armed = true;
		if (!_showing)
			_player = null;
	}

	private void ShowLockedMessage()
	{
		if (_dialogue == null)
			return;

		GetParent()?.GetNodeOrNull<ObjectiveDialogue>("ObjectiveDialogue")?.Dismiss();
		bool communicationsDown = MissionState.Instance?.SignalTableRepaired != true;
		DialogueSequence sequence = communicationsDown ? CommunicationsLocked : RepairsPending;
		DialogueLine line = sequence != null && sequence.Lines.Count > 0
			? sequence.Lines[0]
			: new DialogueLine
			{
				Speaker = "MISSION CONTROL",
				Text = communicationsDown
					? "You can't go back yet. Communications haven't been restored."
					: "You can't go back yet. Finish the remaining repairs first."
			};

		_showing = true;
		_remaining = Mathf.Max(2f, MessageSeconds);
		_dialogue.ShowLine(line);
		_player?.SetPhysicsProcess(false);
	}

	private void HideMessage()
	{
		if (!_showing)
			return;

		_showing = false;
		_dialogue.Clear();
		if (IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
	}

	private void BeginEnding()
	{
		var mission = MissionState.Instance;
		if (mission?.Oxygen.CapacitySeconds <= 0 ||
			mission.Oxygen.RemainingSeconds < mission.Oxygen.ReturnReserveSeconds)
		{
			ShowReturnReserveMessage();
			return;
		}
		if (mission?.TryCompleteMission() != true)
		{
			if (IsInstanceValid(_player))
				_player.SetPhysicsProcess(true);
			return;
		}
		_changing = true;
		GetParent()?.GetNodeOrNull<ObjectiveDialogue>("ObjectiveDialogue")?.Dismiss();
		_player?.SetPhysicsProcess(false);
		mission.Stage = MissionStage.Ending;
		Callable.From(ChangeToEnding).CallDeferred();
	}

	private void ShowReturnReserveMessage()
	{
		if (_dialogue == null) return;
		_showing = true;
		_remaining = Mathf.Max(2f, MessageSeconds);
		_dialogue.ShowLine(new DialogueLine
		{
			Speaker = "MISSION CONTROL",
			Text = "OXYGEN RESERVE TOO LOW — refill before attempting Earth return."
		});
		_player?.SetPhysicsProcess(false);
	}

	private void ChangeToEnding()
	{
		Error error = GetTree().ChangeSceneToFile(EndingScene);
		if (error == Error.Ok)
			return;

		_changing = false;
		if (IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
		GD.PushError($"EndingTrigger: could not open {EndingScene}: {error}");
	}

	public override void _Process(double delta)
	{
		if (!_showing)
			return;

		_remaining -= delta;
		if (_remaining <= 0)
			HideMessage();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_showing)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } key &&
			key.Keycode is Key.E or Key.Space or Key.Enter or Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			HideMessage();
		}
	}

	public override void _ExitTree()
	{
		if (_showing && IsInstanceValid(_player))
			_player.SetPhysicsProcess(true);
	}
}

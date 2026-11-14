namespace ares.scripts.mars;

using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;

/// <summary>
/// Shows the current Mars objective when a scene opens and whenever it advances.
/// Place one instance in each building/MarsBase scene.
/// </summary>
public partial class ObjectiveDialogue : CanvasLayer
{
	[Export] public float MessageSeconds { get; set; } = 6f;
	[Export] public bool ShowSecondBuildingBriefing { get; set; }

	private DialogueView _dialogue;
	private MissionState _state;
	private MissionObjective _lastObjective;
	private bool _showing;
	private double _remaining;

	public override void _Ready()
	{
		_dialogue = GetNodeOrNull<DialogueView>("Dialogue");
		_state = MissionState.Instance;
		if (_state == null || _dialogue == null)
			return;

		_lastObjective = _state.CurrentObjective;

		if (ShowSecondBuildingBriefing && !_state.SecondBuildingBriefingShown)
		{
			_state.MarkSecondBuildingBriefingShown();
			_state.MarkObjectiveAnnounced(_lastObjective);
			ShowSequence("res://data/dialogue/second_building_briefing.tres");
		}
		else if (_state.LastAnnouncedObjective != _lastObjective)
		{
			_state.MarkObjectiveAnnounced(_lastObjective);
			ShowObjective(_lastObjective);
		}
	}

	public override void _Process(double delta)
	{
		if (_state != null)
		{
			MissionObjective current = _state.CurrentObjective;
			if (current != _lastObjective)
			{
				_lastObjective = current;
				_state.MarkObjectiveAnnounced(current);
				ShowObjective(current);
			}
		}

		if (!_showing)
			return;

		_remaining -= delta;
		if (_remaining <= 0)
			HideMessage();
	}

	/// <summary>Lets higher-priority scene messages (such as the ending gate) take the screen.</summary>
	public void Dismiss() => HideMessage();

	/// <summary>Shows a one-off gate message without changing objective progress.</summary>
	public void ShowBlocked(DialogueSequence sequence) => ShowSequence(sequence);

	private void ShowObjective(MissionObjective objective)
	{
		ShowSequence(objective switch
		{
			MissionObjective.RestorePower => "res://data/dialogue/objective_power.tres",
			MissionObjective.OpenGreenhouseDoors => "res://data/dialogue/objective_greenhouse.tres",
			MissionObjective.RestoreCommunications => "res://data/dialogue/objective_communications.tres",
			_ => "res://data/dialogue/objective_return.tres",
		});
	}

	private void ShowSequence(string path)
	{
		ShowSequence(GD.Load<DialogueSequence>(path));
	}

	private void ShowSequence(DialogueSequence sequence)
	{
		if (sequence == null || sequence.Lines.Count == 0)
			return;

		_showing = true;
		_remaining = Mathf.Max(2f, MessageSeconds);
		_dialogue.ShowLine(sequence.Lines[0]);
	}

	private void HideMessage()
	{
		_showing = false;
		_dialogue?.Clear();
	}
}

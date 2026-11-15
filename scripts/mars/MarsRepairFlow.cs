namespace ares.scripts.mars;

using System;
using Godot;
using ares.scripts.dialogue;
using ares.scripts.mission;
using ares.scripts.systems.items;

/// <summary>
/// Shared Mars repair attempt rules. A tool holder completes the task in one
/// successful run; without the matching tool the first success only sets up one
/// mandatory retry and shows the astronaut's regret line.
/// </summary>
public static class MarsRepairFlow
{
	public static MinigameOverlay Begin(
		Node owner,
		Node2D player,
		MarsRepairTask task,
		string requiredToolId,
		string minigameScene,
		string title,
		string hint,
		string completedTitle,
		DialogueSequence retryDialogue,
		Action onSolved,
		Action onClosed)
	{
		var mission = MissionState.Instance;
		if (mission?.IsRunOver == true)
		{
			onClosed?.Invoke();
			return null;
		}
		var overlay = MinigameOverlay.Open(owner, player, minigameScene, title, hint, completedTitle);
		if (overlay == null)
		{
			onClosed?.Invoke();
			return null;
		}

		overlay.RetryAfterSolve = () =>
		{
			if (retryDialogue == null)
				return null;
			if (mission == null || mission.IsMarsRepairComplete(task))
				return null;
			if (mission.HasEquipped(requiredToolId))
				return null;
			if (mission.MarsRepairNeedsRetry(task))
				return null;

			mission.RecordMarsRepairAttempt(task);
			return new MinigameOverlay.RetryPlan(retryDialogue);
		};

		overlay.Finished += solved =>
		{
			if (mission?.IsRunOver == true)
			{
				onClosed?.Invoke();
				return;
			}
			if (solved)
				onSolved?.Invoke();
			else
				onClosed?.Invoke();
		};

		return overlay;
	}
}

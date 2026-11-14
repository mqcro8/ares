namespace ares.scripts.training;

using Godot;
using ares.scripts.player;

/// <summary>Restores exactly the physics state owned by one dialogue.</summary>
public sealed class TrainingPlayerLock
{
	private CharacterBody2D _player;
	private bool _previousProcessing;
	private bool _owned;

	public void Acquire(CharacterBody2D player)
	{
		if (_owned) return;
		_player = player;
		_previousProcessing = player.IsPhysicsProcessing();
		_owned = true;
		player.Velocity = Vector2.Zero;
		player.SetPhysicsProcess(false);
	}

	public void Release()
	{
		if (!_owned) return;
		if (GodotObject.IsInstanceValid(_player))
		{
			if (_player is Player player) player.TrainingMovement?.SuppressDialogueJump();
			_player.SetPhysicsProcess(_previousProcessing);
		}
		_owned = false;
	}
}

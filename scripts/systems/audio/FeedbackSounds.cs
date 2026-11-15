namespace Ares.Systems.Audio;

using System.Collections.Generic;
using Godot;

/// <summary>Owner-local sound throttling. Never throttles the underlying action.</summary>
public sealed class FeedbackSounds
{
	private readonly Dictionary<string, ulong> _last = new();

	public bool Play(string id, uint intervalMs = 100, bool duringPause = false)
	{
		ulong now = Time.GetTicksMsec();
		if (_last.TryGetValue(id, out ulong last) && now - last < intervalMs) return false;
		_last[id] = now;
		if (duringPause) AudioManager.PlayUi(id);
		else AudioManager.PlaySfx(id);
		return true;
	}

	public void Transfer(bool before, bool after)
	{
		Play(before == after ? SoundIds.UiDenied : after ? SoundIds.EquipmentTake : SoundIds.EquipmentStore,
			before == after ? 300u : 100u);
	}
}

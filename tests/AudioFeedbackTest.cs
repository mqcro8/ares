using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ares.Systems.Audio;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.items;
using ares.scripts.training;

/// <summary>Headless playback/state contracts; not a substitute for human audition.</summary>
public partial class AudioFeedbackTest : Node
{
	private int _assertions;
	private static readonly string[] Ids = {
		SoundIds.UiNavigate, SoundIds.UiConfirm, SoundIds.UiBack, SoundIds.UiDenied,
		SoundIds.PanelOpen, SoundIds.PanelClose, SoundIds.EquipmentTake, SoundIds.EquipmentStore,
		SoundIds.ConnectorSnap, SoundIds.ElectricalFault, SoundIds.RelayEngage, SoundIds.ObjectiveComplete
	};
	private static void Invoke(object target, string method, params object[] args) =>
		target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
	private static T Field<T>(object target, string name) =>
		(T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
	private void Check(bool valid, string message)
	{
		_assertions++;
		if (!valid) throw new InvalidOperationException(message);
	}
	private AudioStreamPlayer[] Voices => AudioManager.Instance.GetChildren().OfType<AudioStreamPlayer>()
		.Where(p => p.Bus == AudioManager.SfxBusName).ToArray();
	private bool Heard(string id)
	{
		AudioManager.TryGetSfx(id, out var def);
		return Voices.Any(p => p.Playing && p.Stream == def.Stream && Mathf.IsEqualApprox(p.VolumeDb, def.VolumeDb));
	}
	private async Task Wait(double seconds = .35)
	{
		// Match the monotonic audio debounce clock, not a potentially large first-frame delta.
		ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
		do { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
		while (Time.GetTicksMsec() < until);
	}
	private void Remove(Node node) { RemoveChild(node); node.Free(); }

	public override async void _Ready()
	{
		try
		{
			CatalogAndPool();
			await Navigation();
			await Equipment();
			await Boards();
			AudioManager.StopAllSfx();
			Check(Voices.Length == 16 && Voices.All(p => !p.Playing), "Pool remains bounded and stops cleanly");
			GD.Print($"AUDIO FEEDBACK PASS ({_assertions} assertions)");
			GetTree().Quit();
		}
		catch (Exception error)
		{
			GetTree().Paused = false;
			GD.PushError(error.ToString());
			GetTree().Quit(1);
		}
	}

	private void CatalogAndPool()
	{
		var bank = GD.Load<SoundBank>("res://data/audio/sound_bank.tres");
		Check(bank.Sfx.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == bank.Sfx.Count,
			"SFX IDs unique");
		foreach (string id in Ids)
		{
			Check(AudioManager.TryGetSfx(id, out var def) && !def.Loop && def.Stream.GetLength() > 0 &&
				def.Stream.GetLength() <= 1.21, "Short registered non-looping cue: " + id);
		}
		var gate = new FeedbackSounds();
		Check(gate.Play(SoundIds.UiDenied, 300), "First rejection plays");
		Check(!gate.Play(SoundIds.UiDenied, 300), "Repeated rejection throttled");
		AudioManager.StopAllSfx();
		GetTree().Paused = true;
		AudioManager.PlayUi(SoundIds.UiConfirm);
		Check(Heard(SoundIds.UiConfirm) && Voices.Any(p => p.Playing && p.CanProcess()), "UI plays during pause");
		GetTree().Paused = false;
		for (int i = 0; i < 17; i++) AudioManager.PlaySfx(SoundIds.ConnectorSnap);
		Check(Voices.All(p => p.ProcessMode == ProcessModeEnum.Inherit), "Reused UI voice restores gameplay pause behavior");
		AudioManager.StopAllSfx();
	}

	private async Task Navigation()
	{
		var root = new Control();
		var button = new Button();
		root.AddChild(button);
		AddChild(root);
		UiNavigationSounds.Attach(root);
		UiNavigationSounds.Attach(root);
		Check(root.GetChildren().OfType<UiNavigationSounds>().Count() == 1, "Navigation attachment idempotent");
		button.EmitSignal(Control.SignalName.FocusEntered);
		button.EmitSignal(Control.SignalName.MouseEntered);
		Check(Heard(SoundIds.UiNavigate) && Voices.Count(p => p.Playing) == 1, "Hover/focus emits once");
		AudioManager.StopAllSfx();
		await Wait(.12);
		button.EmitSignal(Control.SignalName.MouseExited);
		button.EmitSignal(Control.SignalName.MouseEntered);
		Check(Heard(SoundIds.UiNavigate), "Re-entering the same button remains audible");
		AudioManager.StopAllSfx();
		button.Disabled = true;
		button.EmitSignal(Control.SignalName.MouseEntered);
		Check(Voices.All(p => !p.Playing), "Disabled button silent");
		Remove(root);
	}

	private async Task Equipment()
	{
		MissionState.Instance.ResetEquipment();
		var player = new Player();
		AddChild(player);
		player.SetPhysicsProcess(false);
		var kit = GD.Load<ItemData>("res://data/equipment/items/maintenance_kit.tres");
		var locker = new EquipmentLocker { StarterKit = kit };
		AddChild(locker);
		var panel = new EquipmentPanel { Player = player, Locker = locker };
		AddChild(panel);
		Check(Heard(SoundIds.PanelOpen), "Equipment panel opens audibly");
		AudioManager.StopAllSfx();
		Invoke(panel, "ToggleKit");
		Check(player.Inventory.Contains(kit) && Heard(SoundIds.EquipmentTake), "Take cue after real transfer");
		AudioManager.StopAllSfx();
		Invoke(panel, "ToggleKit");
		Check(!player.Inventory.Contains(kit) && Heard(SoundIds.EquipmentStore), "Store cue after real transfer");
		await Wait();
		AudioManager.StopAllSfx();
		Invoke(panel, "AdjustTanks", 1);
		Check(MissionState.Instance.OxygenTankCount == 1 && Heard(SoundIds.EquipmentTake),
			$"Tank acceptance cue: tanks={MissionState.Instance.OxygenTankCount}, feedback={Field<Label>(panel, "_feedback").Text}, voices={string.Join(",", Voices.Where(p => p.Playing).Select(p => p.Stream.ResourcePath + ":" + p.VolumeDb))}");
		AudioManager.StopAllSfx();
		Invoke(panel, "AdjustTanks", 99);
		Check(MissionState.Instance.OxygenTankCount == 1 && Heard(SoundIds.UiDenied), "Rejected tank count never confirms");
		AudioManager.StopAllSfx();
		panel.Close();
		panel.Close();
		Check(Heard(SoundIds.PanelClose) && Voices.Count(p => p.Playing) == 1, "Close idempotent");
		await Wait();
		Remove(locker);
		Remove(player);
		MissionState.Instance.ResetEquipment();
	}

	private async Task Boards()
	{
		var wire = GD.Load<PackedScene>("res://scenes/minigames/wireminigame.tscn").Instantiate<WireMinigame>();
		AddChild(wire);
		Invoke(wire.GetNode<PracticeReturn>("PracticeReturn"), "StartGame");
		var left = Field<List<ColorRect>>(wire, "_leftTerminals");
		var right = Field<List<ColorRect>>(wire, "_rightTerminals");
		AudioManager.StopAllSfx();
		Invoke(wire, "StartDrag", left[0]);
		Check(Heard(SoundIds.EquipmentTake), "Wire pickup cue");
		AudioManager.StopAllSfx();
		Invoke(wire, "EndDragAt", right.First(t => t.Color != left[0].Color));
		Check(wire.ConnectedCount == 0 && Heard(SoundIds.ElectricalFault), "Wrong terminal rejects without progress");
		Invoke(wire, "StartDrag", left[0]);
		AudioManager.StopAllSfx();
		Invoke(wire, "EndDragAt", new object[] { null });
		Check(Voices.All(p => !p.Playing), "Cancelled wire has no electrical fault");
		int completed = 0;
		wire.Completed += () => completed++;
		foreach (var terminal in left)
		{
			await Wait(.12);
			Invoke(wire, "StartDrag", terminal);
			AudioManager.StopAllSfx();
			Invoke(wire, "EndDragAt", right.First(t => t.Color == terminal.Color));
			Check(Heard(SoundIds.ConnectorSnap), "Accepted wire snaps");
		}
		Check(completed == 1 && Heard(SoundIds.ObjectiveComplete), "Wire completion once");
		Remove(wire);
		var fuse = GD.Load<PackedScene>("res://scenes/minigames/FuseBoxMinigame.tscn").Instantiate<FuseBoxMinigame>();
		AddChild(fuse);
		Invoke(fuse.GetNode<PracticeReturn>("PracticeReturn"), "StartGame");
		AudioManager.StopAllSfx();
		Invoke(fuse, "Press", -1);
		Check(Voices.All(p => !p.Playing), "Invalid fuse input silent");
		Invoke(fuse, "Press", 0);
		Check(Heard(SoundIds.UiConfirm), "Accepted fuse input clicks");
		fuse.StopPending();
		AudioManager.StopAllSfx();
		Invoke(fuse, "ResetBox");
		Check(Heard(SoundIds.ElectricalFault), "Fuse reset interruption");
		await Wait();
		var state = Field<bool[]>(fuse, "_state");
		Array.Fill(state, true);
		int index = Enumerable.Range(0, state.Length).First(i => i != Field<int>(fuse, "_double"));
		state[index] = false;
		AudioManager.StopAllSfx();
		Invoke(fuse, "Press", index);
		Check(Heard(SoundIds.RelayEngage), "Solved fuse engages relay");
		await Wait(.65);
		Check(Heard(SoundIds.ObjectiveComplete), "Fuse completion after original delay");
		Remove(fuse);
	}
}

namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;

/// <summary>
/// Temporary Mars Base testing aid. Remove this script once the outdoor scene is
/// always entered through the normal loadout flow.
/// </summary>
public partial class MarsBaseDebug : Node2D
{
	[Export] public bool StartWithFullStats { get; set; } = true;

	public override void _EnterTree()
	{
		if (!StartWithFullStats) return;

		var mission = MissionState.Instance;
		if (mission == null)
		{
			GD.PushWarning("MarsBaseDebug: MissionState autoload is unavailable; stats were not filled.");
			return;
		}

		// Parent _EnterTree runs before the OxygenHud child _Ready, so the reserve
		// is already full when the surface drain starts.
		mission.DebugFillStats();
		GD.Print("MarsBaseDebug: oxygen and stamina filled for testing.");
	}
}

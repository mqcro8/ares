namespace ares.scripts.mission;

using Godot;

// Single source of truth for the run. Registered as an autoload.
// Every scene reads and writes here; nothing is duplicated locally.
public partial class MissionState : Node
{
    public static MissionState Instance { get; private set; }

    public MissionStage Stage { get; set; } = MissionStage.MainMenu;
    public double MissionTime { get; set; }

    // Resource axes from the design doc. Exact axes and starting values are an M0 decision.
    public int Budget { get; set; }
    public int Mass { get; set; }
    public int Energy { get; set; }
    public int Communications { get; set; }
    public int SpareParts { get; set; }

    public override void _EnterTree() => Instance = this;
}

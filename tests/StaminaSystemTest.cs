using System;
using Godot;
using ares.scripts.mission;

public partial class StaminaSystemTest : Node
{
    public override void _Ready()
    {
        var mission = MissionState.Instance;
        try
        {
            mission.RestoreStamina(60);
            mission.AdvanceStamina(1, true);
            Check(Mathf.IsEqualApprox(mission.Stamina, 60), "Rest delay prevents immediate regeneration");

            mission.AdvanceStamina(1, true);
            Check(Mathf.IsEqualApprox(mission.Stamina, 85), "Stamina regenerates after resting anywhere");

            mission.AdvanceStamina(0.5, false);
            mission.AdvanceStamina(0.5, true);
            Check(Mathf.IsEqualApprox(mission.Stamina, 85), "Movement resets the rest delay");

            Check(mission.TrySpendStamina(20) && Mathf.IsEqualApprox(mission.Stamina, 65),
                "Jump stamina cost updates the shared stat");
            mission.AdvanceStamina(100, true);
            Check(Mathf.IsEqualApprox(mission.Stamina, MissionState.MaxStamina), "Stamina caps at 100");

            GD.Print("STAMINA SYSTEM PASS (5 assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            mission.ResetStamina();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

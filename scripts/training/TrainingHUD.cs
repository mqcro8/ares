namespace ares.scripts.training;

using Godot;

/// <summary>Passive stamina display; no input handling or dialogue overlay.</summary>
public partial class TrainingHUD : CanvasLayer
{
	[Export] public Label Stats { get; set; }
	public void UpdateState(float stamina)
	{
		if (Stats != null) Stats.Text = $"STAMINA  {stamina:0} / 100";
	}
}

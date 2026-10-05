using Godot;

public partial class MainMenu : Control
{
	private VBoxContainer mainButtons;
	private Panel options;

	public override void _Ready()
	{
		mainButtons = GetNode<VBoxContainer>("MainButtons");
		options = GetNode<Panel>("Options");

		mainButtons.Visible = true;
		options.Visible = false;
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/intro/");
	}

	private void OnOptionsPressed()
	{
		GD.Print("options");

		mainButtons.Visible = false;
		options.Visible = true;
	}

	private void OnCreditsPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/credits/");
	}

	private void OnQuitGamePressed()
	{
		GetTree().Quit();
	}

	private void OnBackOptionsPressed()
	{
		mainButtons.Visible = true;
		options.Visible = false;
	}
}

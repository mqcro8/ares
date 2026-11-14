namespace ares.scripts.mars;

using Godot;

/// <summary>
/// Area2D that shows a "Press E to interact" prompt while the player is inside it.
/// Subclasses only need to implement <see cref="Interact"/>.
/// </summary>
public abstract partial class Interactable : Area2D
{
	[Export] public string PromptText { get; set; } = "Press E to interact";

	/// <summary>Set to false to make this object ignore further interactions.</summary>
	[Export] public bool Enabled { get; set; } = true;

	protected Node2D NearbyPlayer { get; private set; }
	protected bool PlayerInRange { get; private set; }

	private Label _prompt;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		CreatePrompt();
	}

	private void CreatePrompt()
	{
		var layer = new CanvasLayer { Layer = 20 };
		AddChild(layer);

		_prompt = new Label
		{
			Text = PromptText,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_prompt.AddThemeFontSizeOverride("font_size", 18);
		_prompt.AddThemeColorOverride("font_color", Colors.White);
		_prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
		_prompt.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_prompt);

		_prompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_prompt.OffsetLeft = -220;
		_prompt.OffsetRight = 220;
		_prompt.OffsetTop = -140;
		_prompt.OffsetBottom = -104;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		NearbyPlayer = body;
		PlayerInRange = true;
		if (Enabled)
			ShowPrompt();
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		NearbyPlayer = null;
		PlayerInRange = false;
		HidePrompt();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!PlayerInRange || !Enabled)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } && @event.IsActionPressed("interact"))
		{
			GetViewport().SetInputAsHandled();
			Interact();
		}
	}

	protected abstract void Interact();

	protected void ShowPrompt()
	{
		if (_prompt == null || !PlayerInRange)
			return;

		_prompt.Text = PromptText;
		_prompt.Visible = true;
	}

	protected void HidePrompt()
	{
		if (_prompt != null)
			_prompt.Visible = false;
	}
}

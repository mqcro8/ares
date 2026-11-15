namespace ares.scripts.mars;

using System.Collections.Generic;
using Godot;
using Ares.Systems.Audio;

/// <summary>World interaction with one nearest, camera-aware E prompt at a time.</summary>
public abstract partial class Interactable : Area2D
{
	private static readonly HashSet<Interactable> Nearby = new();
	private static Interactable _activePrompt;

	[Export] public string PromptText { get; set; } = "INTERACT";
	[Export] public bool Enabled { get; set; } = true;

	protected Node2D NearbyPlayer { get; private set; }
	protected bool PlayerInRange { get; private set; }
	private CanvasLayer _promptLayer;
	private PanelContainer _prompt;
	private Label _message;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		CreatePrompt();
	}

	private void CreatePrompt()
	{
		_promptLayer = new CanvasLayer { Name = "InteractionPromptLayer", Layer = 9 };
		AddChild(_promptLayer);
		_prompt = new PanelContainer
		{
			Name = "InteractionPrompt",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		_prompt.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color("101c2a"),
			BorderColor = new Color("75e4ff"),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		});
		_promptLayer.AddChild(_prompt);

		var margin = new MarginContainer { Name = "Margin", MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 6);
		_prompt.AddChild(margin);
		var row = new HBoxContainer { Name = "Row", MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 7);
		margin.AddChild(row);
		var key = new PanelContainer { Name = "Keycap", CustomMinimumSize = new Vector2(34, 19), MouseFilter = Control.MouseFilterEnum.Ignore };
		key.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color("172b3a"), BorderColor = new Color("f7c30c"),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
		});
		var keyLabel = new Label
		{
			Name = "Key",
			Text = "E",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		keyLabel.AddThemeColorOverride("font_color", new Color("f7c30c"));
		keyLabel.AddThemeFontSizeOverride("font_size", 7);
		ApplyFont(keyLabel);
		key.AddChild(keyLabel);
		row.AddChild(key);

		_message = new Label
		{
			Name = "Message",
			Text = PromptText,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
		};
		_message.AddThemeColorOverride("font_color", Colors.White);
		_message.AddThemeFontSizeOverride("font_size", 8);
		ApplyFont(_message);
		row.AddChild(_message);
	}

	private static void ApplyFont(Control control)
	{
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) control.AddThemeFontOverride("font", font);
	}

	public override void _Process(double delta)
	{
		if (!PlayerInRange) return;
		RefreshActivePrompt();
		if (_activePrompt == this) PositionPrompt();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		NearbyPlayer = body;
		PlayerInRange = true;
		Nearby.Add(this);
		RefreshActivePrompt();
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		NearbyPlayer = null;
		PlayerInRange = false;
		Nearby.Remove(this);
		RefreshActivePrompt();
	}

	private static void RefreshActivePrompt()
	{
		Nearby.RemoveWhere(item => !GodotObject.IsInstanceValid(item) || !item.IsInsideTree() ||
			!item.PlayerInRange || !item.Enabled);
		Interactable selected = null;
		float nearest = float.MaxValue;
		foreach (var item in Nearby)
		{
			if (!GodotObject.IsInstanceValid(item.NearbyPlayer)) continue;
			float distance = item.NearbyPlayer.GlobalPosition.DistanceSquaredTo(item.GlobalPosition);
			if (distance >= nearest) continue;
			nearest = distance;
			selected = item;
		}
		_activePrompt = selected;
		foreach (var item in Nearby)
			if (GodotObject.IsInstanceValid(item._prompt)) item._prompt.Visible = item == selected;
		selected?.PositionPrompt();
	}

	private void PositionPrompt()
	{
		if (!GodotObject.IsInstanceValid(_prompt) || !GodotObject.IsInstanceValid(_message)) return;
		_message.Text = PromptText;
		float maxWidth = Mathf.Max(120, GetViewport().GetVisibleRect().Size.X - 24);
		_message.CustomMaximumSize = new Vector2(Mathf.Max(80, maxWidth - 70), -1);
		_prompt.CustomMaximumSize = new Vector2(maxWidth, -1);
		_prompt.Size = _prompt.GetCombinedMinimumSize();
		Vector2 screen = GetViewport().GetCanvasTransform() * (GlobalPosition + Vector2.Up * 24);
		Vector2 view = GetViewport().GetVisibleRect().Size;
		Vector2 position = screen - new Vector2(_prompt.Size.X / 2, _prompt.Size.Y + 8);
		position.X = Mathf.Clamp(position.X, 8, Mathf.Max(8, view.X - _prompt.Size.X - 8));
		position.Y = Mathf.Clamp(position.Y, 8, Mathf.Max(8, view.Y - _prompt.Size.Y - 8));
		_prompt.Position = position;
	}

	protected void ShowPrompt()
	{
		if (!PlayerInRange || !Enabled) return;
		Nearby.Add(this);
		RefreshActivePrompt();
	}

	protected void HidePrompt()
	{
		Nearby.Remove(this);
		if (_activePrompt == this) RefreshActivePrompt();
		else if (GodotObject.IsInstanceValid(_prompt)) _prompt.Hide();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_activePrompt != this || !PlayerInRange || !Enabled) return;
		if (@event is not InputEventKey { Pressed: true, Echo: false } || !@event.IsActionPressed("interact")) return;
		GetViewport().SetInputAsHandled();
		AudioManager.PlaySfx(SoundIds.Enter);
		Interact();
	}

	protected abstract void Interact();

	public override void _ExitTree()
	{
		BodyEntered -= OnBodyEntered;
		BodyExited -= OnBodyExited;
		Nearby.Remove(this);
		if (_activePrompt == this) _activePrompt = null;
		RefreshActivePrompt();
	}
}

namespace ares.scripts.training;

using System;
using Godot;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

/// <summary>Loadout view opened from the supply locker. It holds the kit and the oxygen tanks.</summary>
public partial class EquipmentPanel : CanvasLayer
{
	[Export] public Texture2D TankIcon { get; set; }
	[Export] public Texture2D KitIcon { get; set; }

	public Player Player { get; set; }
	public EquipmentLocker Locker { get; set; }
	public Action Closed { get; set; }
	public bool IsOpen => IsInsideTree();
	/// <summary>The panel currently on screen, if any. World interactions stay locked while it is open.</summary>
	public static EquipmentPanel Active { get; private set; }

	private readonly TrainingPlayerLock _physicsLock = new();
	private Label _kitStatus, _tankCount, _tankCapacity, _cargoStatus, _feedback;
	private Button _kitButton, _minus, _plus, _close;

	public override void _Ready()
	{
		Layer = 23;
		Active = this;
		BuildUi();
		_physicsLock.Acquire(Player);
		Refresh();
		_close?.GrabFocus();
	}

	private void BuildUi()
	{
		var shade = new ColorRect
		{
			Color = new Color(0.01f, 0.025f, 0.045f, 0.62f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(shade);

		var panel = new PanelContainer { Name = "Panel" };
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		float halfWidth = Mathf.Min(190, (GetViewport().GetVisibleRect().Size.X - 32) / 2);
		panel.OffsetLeft = -halfWidth;
		panel.OffsetRight = halfWidth;
		panel.OffsetTop = -150;
		panel.OffsetBottom = 150;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color("152535"),
			BorderColor = new Color("75e4ff"),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
		});
		AddChild(panel);

		var margin = new MarginContainer { Name = "Margin" };
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 14);
		panel.AddChild(margin);
		var column = new VBoxContainer { Name = "VBox", Alignment = BoxContainer.AlignmentMode.Center };
		column.AddThemeConstantOverride("separation", 8);
		margin.AddChild(column);

		var title = MakeLabel("SUPPLY LOCKER", new Color("75e4ff"), 12);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(title);

		column.AddChild(MakeLabel("EQUIPMENT", new Color(0.75f, 0.82f, 0.86f), 8));
		var kitRow = new HBoxContainer { Name = "KitRow" };
		kitRow.AddThemeConstantOverride("separation", 8);
		column.AddChild(kitRow);
		kitRow.AddChild(MakeIcon(KitIcon, "KitIcon"));
		_kitStatus = MakeLabel("", Colors.White, 8);
		_kitStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_kitStatus.VerticalAlignment = VerticalAlignment.Center;
		kitRow.AddChild(_kitStatus);		_kitButton = new Button { Name = "KitButton", Text = "TAKE", CustomMinimumSize = new Vector2(84, 26) };
		_kitButton.Pressed += ToggleKit;
		kitRow.AddChild(_kitButton);

		column.AddChild(MakeLabel("OXYGEN TANKS", new Color(0.75f, 0.82f, 0.86f), 8));
		var tankRow = new HBoxContainer { Name = "TankRow" };
		tankRow.AddThemeConstantOverride("separation", 8);
		column.AddChild(tankRow);
		tankRow.AddChild(MakeIcon(TankIcon, "TankIcon"));		_minus = new Button { Name = "RemoveTank", Text = "-", CustomMinimumSize = new Vector2(28, 24) };
		_minus.Pressed += RemoveTank;
		tankRow.AddChild(_minus);
		_tankCount = MakeLabel("0 / 4 TANKS", Colors.White, 8);
		_tankCount.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_tankCount.HorizontalAlignment = HorizontalAlignment.Center;
		_tankCount.VerticalAlignment = VerticalAlignment.Center;
		tankRow.AddChild(_tankCount);
		_plus = new Button { Name = "AddTank", Text = "+", CustomMinimumSize = new Vector2(28, 24) };
		_plus.Pressed += AddTank;
		tankRow.AddChild(_plus);

		_tankCapacity = MakeLabel("", new Color(0.65f, 0.77f, 0.82f), 7);
		column.AddChild(_tankCapacity);
		_cargoStatus = MakeLabel("", new Color(0.65f, 0.77f, 0.82f), 7);
		column.AddChild(_cargoStatus);
		_feedback = MakeLabel("", new Color(1f, 0.45f, 0.38f), 7);
		_feedback.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(_feedback);

		_close = new Button { Name = "Close", Text = "CLOSE", CustomMinimumSize = new Vector2(0, 30) };
		_close.Pressed += Close;
		column.AddChild(_close);
	}

	private static Control MakeIcon(Texture2D texture, string name)
	{
		var slot = new PanelContainer
		{
			Name = name,
			CustomMinimumSize = new Vector2(28, 28),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.12f, 0.16f, 1),
			BorderColor = new Color(0.278f, 0.482f, 0.545f, 0.9f),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
		});
		var icon = new TextureRect
		{
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		slot.AddChild(icon);
		return slot;
	}

	private static Label MakeLabel(string text, Color color, int size)
	{
		var label = new Label
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipText = true,
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeFontSizeOverride("font_size", size);
		var font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) label.AddThemeFontOverride("font", font);
		return label;
	}

	private void ToggleKit()
	{
		if (Locker == null || Player == null) return;
		_feedback.Text = Locker.Toggle(Player);
		Refresh();
	}

	private void AddTank() => AdjustTanks(1);

	private void RemoveTank() => AdjustTanks(-1);

	private void AdjustTanks(int amount)
	{
		var mission = MissionState.Instance;
		if (mission == null) return;
		_feedback.Text = mission.TrySetOxygenTankCount(mission.OxygenTankCount + amount, out string error)
			? "" : error;
		Refresh();
	}

	/// <summary>Refreshes the loadout values shown in the panel.</summary>
	public void Refresh()
	{
		var mission = MissionState.Instance;
		if (mission == null) return;
		bool locked = mission.LoadoutConfirmed || mission.Oxygen.HasStartedSurface;

		if (Locker != null && Player != null && Locker.StarterKit != null)
		{
			bool carried = Player.Inventory != null && Player.Inventory.Contains(Locker.StarterKit);
			bool stored = Locker.Contents != null && Locker.Contents.Contains(Locker.StarterKit);
			_kitStatus.Text = carried ? "MAINTENANCE KIT" : stored ? "MAINTENANCE KIT" : "NO KIT";
			_kitButton.Text = carried ? "STORE" : stored ? "TAKE" : "EMPTY";
			_kitButton.Disabled = locked || (!carried && !stored);
		}

		_tankCount.Text = $"{mission.OxygenTankCount} / {OxygenSupply.MaxTanks} TANKS";
		var capacity = TimeSpan.FromSeconds(mission.Oxygen.CapacitySeconds);
		_tankCapacity.Text = $"CAPACITY {capacity.Minutes:D2}:{capacity.Seconds:D2}  ({mission.OxygenCargoUsed} CARGO)";
		_cargoStatus.Text = $"CARGO {mission.CargoUsed}/{mission.CargoCapacity}";
		_minus.Disabled = locked || mission.OxygenTankCount <= 0;
		_plus.Disabled = locked || mission.OxygenTankCount >= OxygenSupply.MaxTanks ||
			mission.CargoUsed + MissionState.OxygenTankCargoCost > mission.CargoCapacity;
		if (locked) _feedback.Text = "LOADOUT LOCKED — the launch is confirmed.";
	}

	private bool _closed;

	public void Close()
	{
		if (_closed) return;
		_closed = true;
		_physicsLock.Release();
		Closed?.Invoke();
		Callable.From(QueueFree).CallDeferred();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode != Key.Escape && key.Keycode != Key.E) return;
		GetViewport().SetInputAsHandled();
		Callable.From(Close).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (Active == this) Active = null;
		if (GodotObject.IsInstanceValid(_kitButton)) _kitButton.Pressed -= ToggleKit;
		if (GodotObject.IsInstanceValid(_minus)) _minus.Pressed -= RemoveTank;
		if (GodotObject.IsInstanceValid(_plus)) _plus.Pressed -= AddTank;
		if (GodotObject.IsInstanceValid(_close)) _close.Pressed -= Close;
		_physicsLock.Release();
	}
}

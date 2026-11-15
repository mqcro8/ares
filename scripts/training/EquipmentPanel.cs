namespace ares.scripts.training;

using System;
using System.Collections.Generic;
using Godot;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

/// <summary>Loadout view opened from the supply locker for the kit, oxygen and repair tools.</summary>
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
	private readonly List<(ItemData Item, Button Button, Action Handler)> _tools = new();
	private Label _kitStatus, _tankCount, _tankCapacity, _cargoStatus, _oxygenWarning, _coverageStatus, _feedback;
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
		float halfWidth = Mathf.Min(240, (GetViewport().GetVisibleRect().Size.X - 32) / 2);
		float halfHeight = Mathf.Min(250, (GetViewport().GetVisibleRect().Size.Y - 32) / 2);
		panel.OffsetLeft = -halfWidth;
		panel.OffsetRight = halfWidth;
		panel.OffsetTop = -halfHeight;
		panel.OffsetBottom = halfHeight;
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
		column.AddThemeConstantOverride("separation", 6);
		margin.AddChild(column);

		var title = MakeLabel("SUPPLY LOCKER", new Color("75e4ff"), 12);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(title);

		column.AddChild(MakeLabel("EQUIPO", new Color(0.75f, 0.82f, 0.86f), 8));
		var kitRow = new HBoxContainer { Name = "KitRow" };
		kitRow.AddThemeConstantOverride("separation", 8);
		column.AddChild(kitRow);
		kitRow.AddChild(MakeIcon(KitIcon, "KitIcon"));
		_kitStatus = MakeLabel("", Colors.White, 8);
		_kitStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_kitStatus.VerticalAlignment = VerticalAlignment.Center;
		kitRow.AddChild(_kitStatus);
		_kitButton = new Button { Name = "KitButton", Text = "TOMAR", CustomMinimumSize = new Vector2(84, 26) };
		_kitButton.Pressed += ToggleKit;
		kitRow.AddChild(_kitButton);
		column.AddChild(MakeLabel($"KIT: {Locker?.StarterKit?.CargoCost ?? 0} CARGA", new Color(0.65f, 0.77f, 0.82f), 7));

		column.AddChild(MakeLabel($"TANQUES DE OXÍGENO ({MissionState.OxygenTankCargoCost} CARGA C/U)", new Color(0.75f, 0.82f, 0.86f), 8));
		var tankRow = new HBoxContainer { Name = "TankRow" };
		tankRow.AddThemeConstantOverride("separation", 8);
		column.AddChild(tankRow);
		tankRow.AddChild(MakeIcon(TankIcon, "TankIcon"));
		_minus = new Button { Name = "RemoveTank", Text = "-", CustomMinimumSize = new Vector2(28, 24) };
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

		column.AddChild(MakeLabel("HERRAMIENTAS (COSTO INDIVIDUAL)", new Color(0.75f, 0.82f, 0.86f), 8));
		var toolScroll = new ScrollContainer
		{
			Name = "ToolScroll",
			CustomMinimumSize = new Vector2(0, 150),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			FollowFocus = true,
		};
		column.AddChild(toolScroll);
		var toolColumn = new VBoxContainer { Name = "ToolRows" };
		toolColumn.AddThemeConstantOverride("separation", 3);
		toolScroll.AddChild(toolColumn);
		foreach (ItemData tool in Locker?.Tools ?? Array.Empty<ItemData>())
		{
			if (tool == null) continue;
			var row = new HBoxContainer { Name = tool.Id + "Row" };
			row.AddThemeConstantOverride("separation", 5);
			toolColumn.AddChild(row);
			row.AddChild(MakeIcon(tool.Icon, tool.Id + "Icon"));
			var identity = new VBoxContainer { Name = "Identity", CustomMinimumSize = new Vector2(112, 0) };
			identity.AddThemeConstantOverride("separation", 2);
			var name = MakeLabel(tool.DisplayName.ToUpperInvariant(), Colors.White, 7);
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			name.ClipText = false;
			identity.AddChild(name);
			identity.AddChild(MakeLabel($"{tool.CargoCost} CARGA", new Color(0.65f, 0.77f, 0.82f), 6));
			row.AddChild(identity);
			var button = new Button { Name = tool.Id + "Button", Text = "TOMAR", CustomMinimumSize = new Vector2(60, 24) };
			Action handler = () => ToggleTool(tool);
			button.Pressed += handler;
			row.AddChild(button);
			_tools.Add((tool, button, handler));
			var indicators = new VBoxContainer { Name = tool.Id + "Tradeoff", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			indicators.AddThemeConstantOverride("separation", 2);
			indicators.TooltipText = ToolEffects.Describe(tool.Id);
			row.AddChild(indicators);
			var benefitRow = new HBoxContainer { Name = "Benefit" };
			benefitRow.AddThemeConstantOverride("separation", 4);
			benefitRow.AddChild(MakeLabel("VENTAJA REL.", new Color("70ffb0"), 5));
			benefitRow.AddChild(MakeMeter(ToolEffects.BenefitFraction(tool.Id), new Color("42d98b")));
			var benefit = MakeLabel(ToolEffects.BenefitText(tool.Id), Colors.White, 6);
			benefit.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			benefit.ClipText = false;
			benefit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			benefitRow.AddChild(benefit);
			indicators.AddChild(benefitRow);
			var drawbackRow = new HBoxContainer { Name = "Drawback" };
			drawbackRow.AddThemeConstantOverride("separation", 4);
			drawbackRow.AddChild(MakeLabel("COSTE", new Color("ffb24a"), 5));
			drawbackRow.AddChild(MakeMeter(tool.CargoCost / (float)ToolEffects.MaximumToolCargoCost,
				new Color("e79a35")));
			var drawback = MakeLabel($"{tool.CargoCost}/{ToolEffects.MaximumToolCargoCost} CARGA · {ToolEffects.DrawbackText(tool.Id)}",
				new Color(1f, 0.78f, 0.52f), 6);
			drawback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			drawback.ClipText = false;
			drawback.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			drawbackRow.AddChild(drawback);
			indicators.AddChild(drawbackRow);
		}

		_oxygenWarning = MakeLabel("0 TANQUES: MARTE EMPIEZA SIN OXÍGENO.", new Color(1f, 0.72f, 0.24f), 7);
		_oxygenWarning.Name = "OxygenWarning";
		column.AddChild(_oxygenWarning);
		_coverageStatus = MakeLabel("", new Color(0.75f, 0.82f, 0.86f), 6);
		_coverageStatus.Name = "CoverageSummary";
		_coverageStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_coverageStatus.ClipText = false;
		_coverageStatus.CustomMinimumSize = new Vector2(0, 42);
		column.AddChild(_coverageStatus);
		_feedback = MakeLabel("", new Color(1f, 0.45f, 0.38f), 7);
		_feedback.HorizontalAlignment = HorizontalAlignment.Center;
		_feedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_feedback.ClipText = false;
		column.AddChild(_feedback);

		_close = new Button { Name = "Close", Text = "CERRAR", CustomMinimumSize = new Vector2(0, 30) };
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

	/// <summary>Visual-only relative benefit or cargo bar; exact values stay beside it.</summary>
	private static ProgressBar MakeMeter(float value, Color fill)
	{
		var meter = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 1,
			Value = Mathf.Clamp(value, 0, 1),
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(42, 7),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			FocusMode = Control.FocusModeEnum.None,
		};
		meter.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("263746") });
		meter.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
		return meter;
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

	private void ToggleTool(ItemData tool)
	{
		if (Locker == null || Player == null) return;
		_feedback.Text = Locker.ToggleTool(Player, tool);
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
			_kitStatus.Text = carried ? "KIT EN INVENTARIO" : stored ? "KIT EN CASILLERO" : "SIN KIT";
			_kitButton.Text = carried ? "GUARDAR" : stored ? "TOMAR" : "VACÍO";
			_kitButton.Disabled = locked || (!carried && !stored);
		}

		_tankCount.Text = $"{mission.OxygenTankCount} / {OxygenSupply.MaxTanks} TANQUES";
		var capacity = TimeSpan.FromSeconds(mission.Oxygen.CapacitySeconds);
		_tankCapacity.Text = $"RESERVA {capacity.Minutes:D2}:{capacity.Seconds:D2}  ({mission.OxygenCargoUsed} CARGA)";
		_cargoStatus.Text = $"CARGA {mission.CargoUsed}/{mission.CargoCapacity}";
		_oxygenWarning.Visible = mission.OxygenTankCount == 0;
		foreach (var (tool, button, _) in _tools)
		{
			bool carried = Player?.Inventory?.Contains(tool) == true;
			button.Text = carried ? "GUARDAR" : "TOMAR";
			button.Disabled = locked || (!carried && Locker?.Contents?.Contains(tool) != true);
		}
		var covered = new List<string>();
		foreach (var tool in _tools)
			if (mission.HasEquipped(tool.Item.Id))
			{
				string failure = ToolEffects.CoveredFailure(tool.Item.Id);
				if (!string.IsNullOrEmpty(failure) && !covered.Contains(failure)) covered.Add(failure);
			}
		if (mission.HasEquipped(ToolEffects.OxygenRegulator) && mission.OxygenTankCount >= ToolEffects.OxygenRegulatorMinimumTanks)
			covered.Add(ToolEffects.CoveredFailure(ToolEffects.OxygenRegulator));
		covered.RemoveAll(string.IsNullOrEmpty);
		var uncovered = new List<string>(ToolEffects.FailureCategories);
		foreach (string failure in covered) uncovered.Remove(failure);
		string kitAccess = mission.HasEquipped(ToolEffects.MaintenanceKit)
			? "Kit permite las cuatro prácticas." : "Kit necesario para iniciar prácticas.";
		_coverageStatus.Text = $"{kitAccess}\nFALLAS CON BONO: {(covered.Count > 0 ? string.Join(", ", covered) : "ninguna")}\nSIN BONO: {(uncovered.Count > 0 ? string.Join(", ", uncovered) : "ninguna")}";
		_minus.Disabled = locked || mission.OxygenTankCount <= 0;
		_plus.Disabled = locked || mission.OxygenTankCount >= OxygenSupply.MaxTanks ||
			mission.CargoUsed + MissionState.OxygenTankCargoCost > mission.CargoCapacity;
		if (locked) _feedback.Text = "EQUIPO BLOQUEADO — lanzamiento confirmado.";
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
		foreach (var (_, button, handler) in _tools)
			if (GodotObject.IsInstanceValid(button)) button.Pressed -= handler;
		_physicsLock.Release();
	}
}

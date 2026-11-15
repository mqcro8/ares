namespace ares.scripts.training;

using System;
using Godot;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

/// <summary>Supply locker shared by the training rooms; contents survive scene changes.</summary>
public partial class EquipmentLocker : Node2D
{
    [Export] public Sprite2D Sprite { get; set; }
    [Export] public Texture2D ClosedTexture { get; set; }
    [Export] public Texture2D OpenTexture { get; set; }
    [Export] public ItemData StarterKit { get; set; }
    [Export(PropertyHint.Range, "1,8,1")] public int Slots { get; set; } = 2;
    [Export] public Control FrontZone { get; set; }
    [Export] public Texture2D TankIcon { get; set; }
    [Export] public Texture2D KitIcon { get; set; }

    public Inventory Contents { get; private set; }
    public EquipmentPanel Panel { get; private set; }
    public ItemData[] Tools { get; private set; } = Array.Empty<ItemData>();
    public ItemData DormantOxygenRegulator { get; private set; }

    public override void _Ready()
    {
        if (Sprite == null) Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (FrontZone == null) FrontZone = GetNodeOrNull<Control>("FrontZone");
        Tools = new[]
        {
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.SpareFuses}.tres"),
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.FrequencyAnalyzer}.tres"),
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.PrecisionWelder}.tres"),
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.WirePliers}.tres"),
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.RoverBumper}.tres"),
            GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.RoverServos}.tres"),
        };
        DormantOxygenRegulator = GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.OxygenRegulator}.tres");
        Contents = MissionState.Instance?.GetOrCreateLocker(Math.Max(Slots, Tools.Length + 1), StarterKit, Tools);
        Refresh();
    }

    public string InteractionPrompt(Player player)
    {
        if (StarterKit == null || player?.Inventory == null) return "SUPPLY LOCKER UNAVAILABLE";
        return Panel != null ? "E — Close supply locker" : "E — Open supply locker";
    }

    /// <summary>Opens the loadout view. It is the single place to take the kit and pick tanks.</summary>
    public string Open(Player player)
    {
        if (StarterKit == null || player?.Inventory == null)
            return "SUPPLY LOCKER UNAVAILABLE";
        if (Panel != null && GodotObject.IsInstanceValid(Panel))
        {
            Panel.Close();
            return "SUPPLY LOCKER CLOSED.";
        }

        Panel = new EquipmentPanel
        {
            Name = "EquipmentPanel",
            Player = player,
            Locker = this,
            TankIcon = TankIcon,
            KitIcon = KitIcon,
        };
        Panel.Closed += OnPanelClosed;
        GetTree().CurrentScene?.AddChild(Panel);
        Refresh();
        return "SUPPLY LOCKER OPEN — choose your equipment.";
    }

    private void OnPanelClosed()
    {
        if (Panel != null) Panel.Closed -= OnPanelClosed;
        Panel = null;
    }

    public string Toggle(Player player)
    {
        if (StarterKit == null || player?.Inventory == null || Contents == null)
            return "SUPPLY LOCKER UNAVAILABLE";

        var mission = MissionState.Instance;
        if (player.Inventory.Contains(StarterKit))
        {
            if (!InventoryTransferService.TryTransfer(player.Inventory, Contents, StarterKit, 1,
                    int.MaxValue, out string storeError))
                return "CANNOT STORE KIT — " + storeError;

            Refresh();
            return "MAINTENANCE KIT STORED.";
        }

        if (!Contents.Contains(StarterKit)) return "SUPPLY LOCKER EMPTY.";

        if (!InventoryTransferService.TryTransfer(Contents, player.Inventory, StarterKit, 1,
                mission?.CargoCapacity ?? ToolEffects.CargoCapacity, out string takeError,
                mission?.OxygenCargoUsed ?? 0))
            return "CANNOT TAKE KIT — " + takeError;

        Refresh();
        return $"MAINTENANCE KIT ACQUIRED — SELECT IT WITH 1  (CARGO {mission?.CargoUsed ?? 0}/{mission?.CargoCapacity ?? ToolEffects.CargoCapacity})";
    }

    public string ToggleTool(Player player, ItemData tool)
    {
        var mission = MissionState.Instance;
        if (player?.Inventory == null || Contents == null || tool == null || mission == null)
            return "TOOL UNAVAILABLE.";
        if (mission.LoadoutConfirmed || mission.Oxygen.HasStartedSurface)
            return "LOADOUT LOCKED — the launch is confirmed.";

        if (player.Inventory.Contains(tool))
        {
            if (!InventoryTransferService.TryTransfer(player.Inventory, Contents, tool, 1,
                    int.MaxValue, out string storeError))
                return "CANNOT STORE TOOL — " + storeError;
            mission.Oxygen.RefreshLoadout();
            return $"{tool.DisplayName.ToUpperInvariant()} STORED.";
        }

        if (!Contents.Contains(tool)) return "TOOL UNAVAILABLE.";
        if (!InventoryTransferService.TryTransfer(Contents, player.Inventory, tool, 1,
                mission.CargoCapacity, out string takeError, mission.OxygenCargoUsed))
            return "CANNOT TAKE TOOL — " + takeError;
        mission.Oxygen.RefreshLoadout();
        return $"{tool.DisplayName.ToUpperInvariant()} ACQUIRED.";
    }

    public void Refresh()
    {
        if (Sprite == null || StarterKit == null) return;
        bool available = Contents != null && Contents.Contains(StarterKit);
        Sprite.Texture = available ? OpenTexture : ClosedTexture;
    }
}

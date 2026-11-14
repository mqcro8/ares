namespace ares.scripts.training;

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

    public override void _Ready()
    {
        if (Sprite == null) Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (FrontZone == null) FrontZone = GetNodeOrNull<Control>("FrontZone");
        Contents = MissionState.Instance?.GetOrCreateLocker(Slots, StarterKit);
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
                mission?.CargoCapacity ?? 6, out string takeError,
                mission?.OxygenCargoUsed ?? 0))
            return "CANNOT TAKE KIT — " + takeError;

        Refresh();
        return $"MAINTENANCE KIT ACQUIRED — SELECT IT WITH 1  (CARGO {mission?.CargoUsed ?? 0}/{mission?.CargoCapacity ?? 6})";
    }

    public void Refresh()
    {
        if (Sprite == null || StarterKit == null) return;
        bool available = Contents != null && Contents.Contains(StarterKit);
        Sprite.Texture = available ? OpenTexture : ClosedTexture;
    }
}

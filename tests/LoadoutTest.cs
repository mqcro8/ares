using Godot;
using System;
using ares.scripts.mission;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;

public partial class LoadoutTest : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"LOADOUT PASS ({_assertions} assertions)");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Run()
    {
        var mission = MissionState.Instance;
        var kit = GD.Load<ItemData>("res://data/equipment/items/maintenance_kit.tres");
        var filler = GD.Load<ItemData>("res://data/equipment/items/test_item.tres");

        Check(kit != null && kit.HasCapability(ItemCapability.Repair), "Maintenance kit exposes the repair capability");
        Check(kit.CargoCost > 0 && kit.MaxStackSize == 1, "Kit has cargo cost and is a single grouped item");
        Check(!new ItemData().HasCapability(ItemCapability.Repair), "Plain items do not repair");

        mission.ResetEquipment();
        var locker = mission.GetOrCreateLocker(2, kit);
        var player = mission.GetOrCreatePlayerInventory(5, out bool created);

        Check(created && player.SlotCount == 5 && locker.SlotCount == 2, "Shared containers are created once with explicit sizes");
        Check(locker.Contains(kit) && !player.Contains(kit) && mission.CargoUsed == 0, "Locker starts with the kit and empty player cargo");
        Check(mission.GetOrCreateLocker(2, kit).CountOf(kit) == 1, "Locker seeding is idempotent");
        Check(mission.GetOrCreatePlayerInventory(8, out bool recreated) == player && !recreated,
            "Player inventory keeps identity and slot count across lookups");

        Check(InventoryTransferService.TryTransfer(locker, player, kit, 1, mission.CargoCapacity, out string takeError),
            "Locker transfers the kit into the hotbar inventory: " + takeError);
        Check(player.Contains(kit) && !locker.Contains(kit) && mission.CargoUsed == kit.CargoCost,
            "Transferred kit consumes cargo, not an extra inventory slot");
        player.Size = 8;
        Check(mission.CargoUsed == kit.CargoCost, "Hotbar capacity does not change cargo usage");

        Check(InventoryTransferService.TryTransfer(player, locker, kit, 1, int.MaxValue, out string storeError),
            "Kit can be stored back: " + storeError);
        Check(locker.Contains(kit) && !player.Contains(kit) && mission.CargoUsed == 0,
            "Storing the kit clears cargo and both sides update");

        mission.CargoCapacity = kit.CargoCost - 1;
        Check(!InventoryTransferService.TryTransfer(locker, player, kit, 1, mission.CargoCapacity, out string cargoError)
            && cargoError.Contains("Cargo limit"), "Cargo limit blocks the transfer with a clear reason");
        Check(locker.Contains(kit) && !player.Contains(kit), "Failed cargo transfer leaves both containers unchanged");

        mission.CargoCapacity = 6;
        var full = new Inventory { Size = 1 };
        full.AddItem(filler, filler.MaxStackSize);
        Check(!InventoryTransferService.TryTransfer(locker, full, kit, 1, mission.CargoCapacity, out string spaceError)
            && spaceError.Contains("space"), "Full destination blocks the transfer with a clear reason");
        Check(locker.Contains(kit) && full.CountOf(kit) == 0, "Failed space transfer leaves both containers unchanged");

        Check(!InventoryTransferService.TryTransfer(locker, player, kit, 0, mission.CargoCapacity, out _),
            "Zero-quantity transfer rejected");
        Check(!InventoryTransferService.TryTransfer(locker, player, kit, 2, mission.CargoCapacity, out _),
            "Insufficient source quantity rejected");
        Check(!InventoryTransferService.TryTransfer(locker, player, null, 1, mission.CargoCapacity, out _),
            "Null item rejected");

        var resize = new Inventory { Size = 2 };
        resize.AddItem(kit, 2);
        int notifications = 0;
        resize.InventoryChanged += () => notifications++;
        Check(!resize.TryResize(1) && resize.CountOf(kit) == 2 && resize.SlotCount == 2 && notifications == 0,
            "Shrinking occupied slots is atomic and emits no change");
        var hotbar = new Hotbar { SlotScene = GD.Load<PackedScene>("res://scripts/hotbar_scene.tscn") };
        AddChild(hotbar);
        hotbar.Bind(resize);
        Check(resize.TryResize(4) && hotbar.GetChildCount() == 4 && notifications == 1,
            "Growing inventory updates the bound hotbar once");
        hotbar.Select(3);
        int selectionChanges = 0;
        hotbar.SelectionChanged += _ => selectionChanges++;
        Check(resize.TryResize(2) && hotbar.GetChildCount() == 2 && hotbar.SelectedIndex == 1 && selectionChanges == 1,
            "Shrinking empty slots reconciles hotbar selection");
        int previousNotifications = notifications;
        Check(resize.TryResize(2) && notifications == previousNotifications,
            "Unchanged size does not emit another notification");
        hotbar.QueueFree();

        mission.ResetEquipment();
        Check(mission.PlayerInventory == null && mission.LockerInventory == null && mission.CargoUsed == 0,
            "New mission reset clears equipment references");
    }
}

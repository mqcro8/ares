using Godot;
using System;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.systems.inventory;
using ares.scripts.systems.items;
using ares.scripts.training;

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

    private void CheckRepairEffects(bool equipped)
    {
        var fuse = GD.Load<PackedScene>("res://scenes/minigames/FuseBoxMinigame.tscn").Instantiate<FuseBoxMinigame>();
        AddChild(fuse);
        Check(fuse.GetNode<HBoxContainer>("Box/Lights").GetChildCount() == (equipped ? 5 : 4),
            "Fuse box shows one extra press/light only when spare fuses are carried");
        RemoveChild(fuse);
        fuse.Free();

        var frequency = GD.Load<PackedScene>("res://scenes/minigames/FrequencyBoxMinigame.tscn").Instantiate<FrequencyBoxMinigame>();
        AddChild(frequency);
        Check(Mathf.IsEqualApprox(frequency.MatchThreshold, equipped ? 0.85f : 0.9f) &&
            Mathf.IsEqualApprox(frequency.HoldSeconds, 0.35f),
            "Frequency analyzer changes only the match threshold");
        RemoveChild(frequency);
        frequency.Free();

        var hull = GD.Load<PackedScene>("res://scenes/minigames/HullRepairMinigame.tscn").Instantiate<HullRepairMinigame>();
        AddChild(hull);
        Check(Mathf.IsEqualApprox(hull.WeldTolerance, equipped ? 18f : 14f) &&
            Mathf.IsEqualApprox(hull.PlaceTolerance, 14f),
            "Precision welder changes only weld tolerance");
        RemoveChild(hull);
        hull.Free();
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

        Check(mission.TrySetOxygenTankCount(4, out string tankError),
            "Four oxygen tanks fit the available cargo: " + tankError);
        Check(mission.CargoUsed == 4 && mission.OxygenTankCount == 4,
            "Each oxygen tank reserves one cargo unit");
        Check(!InventoryTransferService.TryTransfer(locker, player, kit, 1, mission.CargoCapacity,
                out string reservedError, mission.OxygenCargoUsed) && reservedError.Contains("Cargo limit"),
            "Reserved oxygen cargo prevents an over-capacity kit transfer");
        string kitError = "";
        bool selectedThreeTanks = mission.TrySetOxygenTankCount(3, out tankError);
        bool transferredKit = selectedThreeTanks && InventoryTransferService.TryTransfer(locker, player, kit, 1,
            mission.CargoCapacity, out kitError, mission.OxygenCargoUsed);
        Check(transferredKit,
            "Three tanks leave enough cargo for the maintenance kit: " + tankError + kitError);
        Check(mission.CargoUsed == 6 && !mission.TrySetOxygenTankCount(4, out _),
            "Tank selection cannot exceed cargo after acquiring the maintenance kit");

        mission.ResetEquipment();
        Check(mission.PlayerInventory == null && mission.LockerInventory == null && mission.CargoUsed == 0 &&
            mission.OxygenTankCount == 0,
            "New mission reset clears equipment references");

        var astronaut = new Player();
        AddChild(astronaut);
        var supply = new EquipmentLocker { StarterKit = kit };
        AddChild(supply);
        Check(supply.Contents.SlotCount == 4 && supply.Tools.Length == 3 &&
            supply.Tools[0].Id == "spare_fuses" && supply.Tools[1].Id == "frequency_analyzer" &&
            supply.Tools[2].Id == "precision_welder", "Locker stocks the three distinct tools once");
        foreach (var tool in supply.Tools)
            Check(tool.Icon == null && tool.CargoCost == 1 && tool.MaxStackSize == 1,
                "New tools are one-cargo, single items with blank placeholder icons");

        Check(supply.Toggle(astronaut).Contains("ACQUIRED") && mission.CargoUsed == 3,
            "Taking the existing kit still costs three cargo");
        Check(mission.TrySetOxygenTankCount(2, out tankError) && mission.CargoUsed == 5 &&
            mission.Oxygen.CapacitySeconds == 360, "Two tanks leave one cargo for a tool");
        Check(supply.ToggleTool(astronaut, supply.Tools[0]).Contains("ACQUIRED") &&
            mission.HasEquipped("spare_fuses") && mission.CargoUsed == 6,
            "Tool possession is stored in MissionState's inventory");
        Check(supply.ToggleTool(astronaut, supply.Tools[1]).Contains("Cargo limit") &&
            !mission.HasEquipped("frequency_analyzer") && supply.Contents.Contains(supply.Tools[1]),
            "Over-capacity selection leaves both inventories unchanged");
        Check(mission.GetOrCreateLocker(4, kit, supply.Tools).CountOf(supply.Tools[0]) == 0,
            "Revisiting the locker does not duplicate a tool already taken");
        Check(mission.TrySetOxygenTankCount(1, out tankError) &&
            supply.ToggleTool(astronaut, supply.Tools[1]).Contains("ACQUIRED") &&
            mission.CargoUsed == 6 && mission.Oxygen.CapacitySeconds == 180,
            "Choosing two tools trades six oxygen minutes for repair advantages");

        var equipmentPanel = new EquipmentPanel { Player = astronaut, Locker = supply };
        AddChild(equipmentPanel);
        var panelSize = equipmentPanel.GetNode<PanelContainer>("Panel");
        Check(panelSize.GetCombinedMinimumSize().Y <= panelSize.Size.Y + 1f,
            $"Equipment rows fit the panel: {panelSize.GetCombinedMinimumSize().Y}/{panelSize.Size.Y}");
        Check(!equipmentPanel.GetNode<Label>("Panel/Margin/VBox/OxygenWarning").Visible,
            "Zero-tank warning stays hidden while carrying oxygen");
        Check(equipmentPanel.GetNode<Button>("Panel/Margin/VBox/spare_fusesRow/spare_fusesButton").Text == "STORE",
            "Equipment screen shows carried tool and provides a store action");
        Check(mission.TrySetOxygenTankCount(0, out tankError), "Zero tanks remain an allowed choice");
        equipmentPanel.Refresh();
        Check(equipmentPanel.GetNode<Label>("Panel/Margin/VBox/OxygenWarning").Visible,
            "Zero tanks produce an explicit warning without blocking the choice");
        mission.LoadoutConfirmed = true;
        equipmentPanel.Refresh();
        Check(equipmentPanel.GetNode<Button>("Panel/Margin/VBox/spare_fusesRow/spare_fusesButton").Disabled,
            "Confirmed loadout locks the new tool actions");
        equipmentPanel.Close();
        mission.ResetEquipment();
        CheckRepairEffects(false);
        var chosen = mission.GetOrCreatePlayerInventory(5, out _);
        foreach (var tool in supply.Tools) chosen.AddItem(tool);
        CheckRepairEffects(true);
        mission.ResetEquipment();
        CheckRepairEffects(false);
    }
}

using Godot;
using System;
using System.Linq;
using ares.scripts.mission;
using ares.scripts.player;
using ares.scripts.rover;
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
        var fuseStatus = fuse.GetChildren().OfType<Label>().First(label => label.Text.StartsWith("Presses:"));
        int pressBudget = ToolEffects.FusePressesFor(equipped);
        Check(fuseStatus.Text.Contains($"{pressBudget}/{pressBudget}"),
            "Fuse status text uses effective press budget");
        RemoveChild(fuse);
        fuse.Free();

        var frequency = GD.Load<PackedScene>("res://scenes/minigames/FrequencyBoxMinigame.tscn").Instantiate<FrequencyBoxMinigame>();
        AddChild(frequency);
        Check(Mathf.IsEqualApprox(frequency.MatchThreshold, equipped
                ? ToolEffects.FrequencyAnalyzerThreshold : ToolEffects.FrequencyBaseThreshold) &&
            Mathf.IsEqualApprox(frequency.HoldSeconds, ToolEffects.FrequencyBaseHoldSeconds),
            "Frequency analyzer changes only the match threshold");
        RemoveChild(frequency);
        frequency.Free();

        var hull = GD.Load<PackedScene>("res://scenes/minigames/HullRepairMinigame.tscn").Instantiate<HullRepairMinigame>();
        AddChild(hull);
        Check(Mathf.IsEqualApprox(hull.WeldTolerance, equipped
                ? ToolEffects.HullPrecisionWeldTolerance : ToolEffects.HullBaseWeldTolerance) &&
            Mathf.IsEqualApprox(hull.HeatOffSeam, equipped
                ? ToolEffects.HullPrecisionHeatOffSeam : ToolEffects.HullBaseHeatOffSeam) &&
            Mathf.IsEqualApprox(hull.PlaceTolerance, ToolEffects.HullBasePlaceTolerance),
            "Precision welder changes weld tolerance and off-seam heat only");
        RemoveChild(hull);
        hull.Free();

        var wires = GD.Load<PackedScene>("res://scenes/minigames/wireminigame.tscn").Instantiate<WireMinigame>();
        AddChild(wires);
        Check(wires.ConnectedCount == (equipped ? ToolEffects.WirePreconnectedCount : 0),
            "Wire pliers preconnect exactly one cable");
        RemoveChild(wires);
        wires.Free();
    }

    private void CheckRoverLoadout(bool bumper, bool servos)
    {
        var mission = MissionState.Instance;
        mission.ResetEquipment();
        var inventory = mission.GetOrCreatePlayerInventory(7, out _);
        if (bumper) inventory.AddItem(GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.RoverBumper}.tres"));
        if (servos) inventory.AddItem(GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.RoverServos}.tres"));

        var root = new Node2D();
        var rover = new Rover();
        rover.AddChild(new RoverAnimation { Name = "AnimatedSprite2D" });
        root.AddChild(rover);
        var hud = new Hud();
        var hearts = new HBoxContainer { Name = "HBoxContainer" };
        for (int i = 0; i < ToolEffects.RoverBaseDamageLimit; i++)
            hearts.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://art/rover/heart_full.png") });
        hud.AddChild(hearts);
        hud.AddChild(new TextureRect { Name = "lagometer" });
        root.AddChild(hud);
        AddChild(root);
        hud.ConfigureMaxImpactPoints(rover.MaxImpactPoints);

        int expectedLimit = ToolEffects.RoverBaseDamageLimit +
            (bumper ? ToolEffects.RoverBumperExtraDamageLimit : 0);
        int expectedDamage = servos ? ToolEffects.RoverServoDamagePerCollision :
            ToolEffects.RoverBaseDamagePerCollision;
        float expectedSpeed = ToolEffects.RoverBaseLaneChangeSpeed *
            (bumper ? ToolEffects.RoverBumperSpeedFactor : 1f) /
            (servos ? ToolEffects.RoverServoControlTimeFactor : 1f);
        Check(rover.MaxImpactPoints == expectedLimit && rover.DamagePerCollision == expectedDamage &&
            Mathf.IsEqualApprox(rover.EffectiveLaneChangeSpeed, expectedSpeed),
            $"Rover loadout bumper={bumper}, servos={servos} applies approved trade-offs");
        Check(hud.HeartCount == expectedLimit, "Rover HUD heart count matches impact capacity");
        hud.UpdateCollisions(expectedLimit);
        Check(hearts.GetChildren().OfType<TextureRect>().All(heart => heart.Texture ==
            GD.Load<Texture2D>("res://art/rover/heart_lost.png")), "Rover HUD clears hearts to impact damage count");
        RemoveChild(root);
        root.Free();
        mission.ResetEquipment();
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

        mission.CargoCapacity = ToolEffects.CargoCapacity;
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

        mission.CargoCapacity = 6;
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
        Check(mission.CargoCapacity == ToolEffects.CargoCapacity,
            "New mission uses the approved seven-cargo loadout capacity");
        Check(mission.PlayerInventory == null && mission.LockerInventory == null && mission.CargoUsed == 0 &&
            mission.OxygenTankCount == 0,
            "New mission reset clears equipment references");

        var astronaut = new Player();
        AddChild(astronaut);
        var supply = new EquipmentLocker { StarterKit = kit };
        AddChild(supply);
        Check(supply.Contents.SlotCount == 7 && supply.Tools.Length == 6 &&
            supply.Tools[0].Id == ToolEffects.SpareFuses && supply.Tools[1].Id == ToolEffects.FrequencyAnalyzer &&
            supply.Tools[2].Id == ToolEffects.PrecisionWelder && supply.Tools[3].Id == ToolEffects.WirePliers &&
            supply.Tools[4].Id == ToolEffects.RoverBumper && supply.Tools[5].Id == ToolEffects.RoverServos &&
            supply.DormantOxygenRegulator.Id == ToolEffects.OxygenRegulator,
            "Locker stocks six tools and keeps regulator dormant until oxygen gameplay is connected");
        Check(supply.DormantOxygenRegulator.CargoCost == 1 && supply.DormantOxygenRegulator.Icon != null,
            "Dormant regulator resource has configured cargo and icon");
        foreach (var tool in supply.Tools)
            Check(tool.Icon != null && tool.HotbarLabel.Length == 2 && tool.MaxStackSize == 1,
                "Stocked tools have an icon, hotbar abbreviation and single-item stack");
        Check(supply.Tools[0].CargoCost == 1 && supply.Tools[1].CargoCost == 1 &&
            supply.Tools[2].CargoCost == 2 && supply.Tools[3].CargoCost == 1 &&
            supply.Tools[4].CargoCost == 2 && supply.Tools[5].CargoCost == 1,
            "Cargo costs match the approved trade-off catalog");
        int allToolCargo = supply.Tools.Sum(tool => tool.CargoCost) + supply.DormantOxygenRegulator.CargoCost;
        int minimumLoadoutFreeCargo = mission.CargoCapacity - kit.CargoCost - MissionState.OxygenTankCargoCost;
        Check(allToolCargo == 9 && allToolCargo > minimumLoadoutFreeCargo && minimumLoadoutFreeCargo == 3,
            "Nine cargo of tools exceed three free cargo after kit plus one tank");
        Check(!supply.Contents.Contains(supply.DormantOxygenRegulator),
            "Regulator resource exists but is not offered before oxygen gameplay is connected");

        Check(supply.Toggle(astronaut).Contains("ACQUIRED") && mission.CargoUsed == 3,
            "Taking the existing kit still costs three cargo");
        Check(mission.TrySetOxygenTankCount(1, out tankError) && mission.CargoUsed == 4 &&
            mission.Oxygen.CapacitySeconds == 180 && mission.CargoCapacity - mission.CargoUsed == 3,
            "One-tank survival kit leaves three cargo for tools");
        Check(supply.ToggleTool(astronaut, supply.Tools[0]).Contains("ACQUIRED") &&
            mission.HasEquipped(ToolEffects.SpareFuses) && mission.CargoUsed == 5,
            "Tool possession is stored in MissionState's inventory");
        Check(supply.ToggleTool(astronaut, supply.Tools[1]).Contains("ACQUIRED") &&
            mission.HasEquipped(ToolEffects.FrequencyAnalyzer) && mission.CargoUsed == 6,
            "Analyser trades one cargo unit for a measurable frequency benefit");
        Check(supply.ToggleTool(astronaut, supply.Tools[2]).Contains("Cargo limit") &&
            !mission.HasEquipped(ToolEffects.PrecisionWelder) && supply.Contents.Contains(supply.Tools[2]),
            "Over-capacity selection leaves both inventories unchanged");
        Check(mission.GetOrCreateLocker(4, kit, supply.Tools).CountOf(supply.Tools[0]) == 0,
            "Revisiting the locker does not duplicate a tool already taken");
        Check(mission.CargoUsed == 6 && mission.Oxygen.CapacitySeconds == 180,
            "Two specialized tools trade two cargo units while preserving one tank");

        var testSlot = GD.Load<PackedScene>("res://scripts/hotbar_scene.tscn").Instantiate<HotbarSlot>();
        AddChild(testSlot);
        testSlot.SetStack(astronaut.Inventory.GetStack(1));
        Check(testSlot.GetNode<Label>("Abbreviation").Text == "FU" &&
            testSlot.GetNode<Label>("Abbreviation").Visible,
            "Hotbar displays abbreviation over tool icon");
        RemoveChild(testSlot);
        testSlot.Free();

        var equipmentPanel = new EquipmentPanel { Player = astronaut, Locker = supply };
        AddChild(equipmentPanel);
        var panelSize = equipmentPanel.GetNode<PanelContainer>("Panel");
        Check(panelSize.GetCombinedMinimumSize().Y <= panelSize.Size.Y + 1f,
            $"Equipment rows fit the panel: {panelSize.GetCombinedMinimumSize().Y}/{panelSize.Size.Y}");
        Check(!equipmentPanel.GetNode<Label>("Panel/Margin/VBox/OxygenWarning").Visible,
            "Zero-tank warning stays hidden while carrying oxygen");
        Check(equipmentPanel.GetNode<Button>("Panel/Margin/VBox/ToolScroll/ToolRows/spare_fusesRow/spare_fusesButton").Text == "GUARDAR",
            "Equipment screen shows carried tool and provides a store action");
        Check(mission.TrySetOxygenTankCount(0, out tankError), "Zero tanks remain an allowed choice");
        equipmentPanel.Refresh();
        Check(equipmentPanel.GetNode<Label>("Panel/Margin/VBox/OxygenWarning").Visible,
            "Zero tanks produce an explicit warning without blocking the choice");
        mission.LoadoutConfirmed = true;
        equipmentPanel.Refresh();
        Check(equipmentPanel.GetNode<Button>("Panel/Margin/VBox/ToolScroll/ToolRows/spare_fusesRow/spare_fusesButton").Disabled,
            "Confirmed loadout locks the new tool actions");
        equipmentPanel.Close();
        mission.ResetEquipment();
        CheckRepairEffects(false);
        var chosen = mission.GetOrCreatePlayerInventory(5, out _);
        foreach (var tool in supply.Tools) chosen.AddItem(tool);
        CheckRepairEffects(true);
        mission.ResetEquipment();
        CheckRepairEffects(false);

        var regulator = GD.Load<ItemData>($"res://data/equipment/items/{ToolEffects.OxygenRegulator}.tres");
        var oxygenInventory = mission.GetOrCreatePlayerInventory(7, out _);
        oxygenInventory.AddItem(regulator);
        mission.Oxygen.RefreshLoadout();
        Check(mission.TrySetOxygenTankCount(ToolEffects.OxygenRegulatorMinimumTanks, out tankError) &&
            mission.Oxygen.CapacitySeconds == ToolEffects.OxygenBaseSecondsPerTank *
            ToolEffects.OxygenRegulatorMinimumTanks * ToolEffects.OxygenRegulatorTankFactor,
            "Regulator adds thirty percent capacity with two tanks");
        Check(mission.TrySetOxygenTankCount(1, out tankError) &&
            mission.Oxygen.CapacitySeconds == ToolEffects.OxygenBaseSecondsPerTank,
            "Regulator provides no bonus with fewer than two tanks");
        mission.ResetEquipment();
        Check(mission.TrySetOxygenTankCount(ToolEffects.OxygenRegulatorMinimumTanks, out tankError) &&
            mission.Oxygen.CapacitySeconds == ToolEffects.OxygenBaseSecondsPerTank *
            ToolEffects.OxygenRegulatorMinimumTanks,
            "Two tanks without regulator keep original duration");
        CheckRoverLoadout(false, false);
        CheckRoverLoadout(true, false);
        CheckRoverLoadout(false, true);
        CheckRoverLoadout(true, true);
    }
}

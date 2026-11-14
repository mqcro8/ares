namespace ares.scripts.systems.inventory;

using System;
using items;

public static class CargoCapacity
{
    public static int Used(Inventory inventory) => inventory?.CargoUsed ?? 0;

    public static bool TryReserve(Inventory inventory, ItemData item, int amount, int capacity, out string error,
        int reservedCargo = 0)
    {
        error = "";
        if (inventory == null || item == null || amount <= 0 || reservedCargo < 0)
        {
            error = "Invalid cargo transfer.";
            return false;
        }

        int added = amount * item.CargoCost;
        int used = Used(inventory) + reservedCargo;
        if (used + added > Math.Max(0, capacity))
        {
            error = $"Cargo limit exceeded ({used + added}/{capacity}).";
            return false;
        }

        return true;
    }
}

namespace ares.scripts.systems.inventory;

using items;

/// <summary>Atomic container-to-container transfer; a failed destination leaves both sides unchanged.</summary>
public static class InventoryTransferService
{
    public static bool TryTransfer(Inventory from, Inventory to, ItemData item, int amount,
        int cargoCapacity, out string error)
    {
        error = "";
        if (from == null || to == null || item == null || amount <= 0)
        {
            error = "Invalid transfer.";
            return false;
        }

        if (!from.Contains(item, amount))
        {
            error = "Not enough items in the source container.";
            return false;
        }

        if (!to.CanFit(item, amount))
        {
            error = "No inventory space.";
            return false;
        }

        if (!CargoCapacity.TryReserve(to, item, amount, cargoCapacity, out error))
            return false;

        int removed = from.RemoveItem(item, amount);
        if (removed != amount)
        {
            if (removed > 0) from.AddItem(item, removed);
            error = "Could not remove the full amount.";
            return false;
        }

        int remaining = to.AddItem(item, amount);
        if (remaining == 0) return true;

        to.RemoveItem(item, amount - remaining);
        from.AddItem(item, amount);
        error = "Could not add the full amount.";
        return false;
    }
}

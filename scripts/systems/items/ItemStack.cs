using System;

public class ItemStack
{
	public ItemData Item { get; }
    public int Count { get; private set; }

    public int MaxSize => Item.MaxStackSize;
    public bool IsFull => Count >= MaxSize;
    public int FreeSpace => MaxSize - Count;

    public ItemStack(ItemData item, int count = 1)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        Item = item;
        Count = Math.Clamp(count, 1, item.MaxStackSize);
    }

    public bool CanStackWith(ItemData other) => other != null && other.Id == Item.Id;

    public int Add(int amount)
    {
        int added = Math.Min(amount, FreeSpace);
        Count += added;
        return amount - added;
    }

    // Removes up to `amount`; returns how many were actually removed.
    public int Remove(int amount)
    {
        int removed = Math.Min(amount, Count);
        Count -= removed;
        return removed;
    }
}
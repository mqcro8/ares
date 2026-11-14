namespace ares.scripts.systems.items;

using System;

[Flags]
public enum ItemCapability
{
    None = 0,
    Repair = 1 << 0,
}

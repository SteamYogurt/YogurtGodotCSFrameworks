using System;

[Flags]
public enum UnitControlFlag
{
    None = 0,
    DisableMove = 1 << 0,
    DisableAttack = 1 << 1,
    DisableThink = 1 << 2,
}

public class UnitControlController
{
    UnitControlFlag _flags;

    public UnitControlFlag Flags => _flags;

    public void AddFlags(UnitControlFlag flags) => _flags |= flags;

    public void RemoveFlags(UnitControlFlag flags) => _flags &= ~flags;

    public bool HasFlag(UnitControlFlag flag) => (_flags & flag) != 0;
}

public interface IUnitControlHost
{
    UnitControlController UnitControlController { get; }
}

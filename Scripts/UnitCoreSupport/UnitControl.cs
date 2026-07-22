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
    readonly int[] _flagCounts = new int[32];
    UnitControlFlag _flags;

    public UnitControlFlag Flags => _flags;

    public void AddFlags(UnitControlFlag flags)
    {
        if (flags == UnitControlFlag.None)
        {
            return;
        }

        uint bits = (uint)flags;
        for (int i = 0; i < 32; i++)
        {
            uint bit = 1u << i;
            if ((bits & bit) == 0)
            {
                continue;
            }

            if (_flagCounts[i] == 0)
            {
                _flags |= (UnitControlFlag)bit;
            }

            _flagCounts[i]++;
        }
    }

    public void RemoveFlags(UnitControlFlag flags)
    {
        if (flags == UnitControlFlag.None)
        {
            return;
        }

        uint bits = (uint)flags;
        for (int i = 0; i < 32; i++)
        {
            uint bit = 1u << i;
            if ((bits & bit) == 0 || _flagCounts[i] <= 0)
            {
                continue;
            }

            _flagCounts[i]--;
            if (_flagCounts[i] == 0)
            {
                _flags &= ~(UnitControlFlag)bit;
            }
        }
    }

    public bool HasFlag(UnitControlFlag flag) => (_flags & flag) != 0;
}

public interface IUnitControlHost
{
    UnitControlController UnitControlController { get; }
}

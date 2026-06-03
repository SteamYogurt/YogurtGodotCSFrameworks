using System;
using System.Collections.Generic;
using Godot;

public interface IUnit
{
    [Export]
    public UnitInfo UnitInfo { get; set; }
    public StatCollection UnitStatCollection { get; set; }
    public event Action<UnitStatType, float> StatChanged;
    public void NotifyStatChanged(UnitStatType type, float amount);
    public float MaxHealth { get; set; }
    public float Health { get; set; }
    public float MaxShield { get; set; }
    public float Shield { get; set; }
    public float PhysicalDefense { get; set; }
    public float MagicalDefense { get; set; }
    public float MoveSpeed { get; set; }
    public float PhysicalAttackDamage { get; set; }
    public float MagicalAttackDamage { get; set; }
    public float RealDamage { get; set; }
    public float AttackRange { get; set; }
    public float AttackSpeed { get; set; }
    public float CritRate { get; set; }
    public bool IsAlive { get; set; }

    public event Action<DamageContext> OnDealingDamage;    // 造成伤害前（计算增伤、暴击）
    public event Action<DamageContext> OnDealtDamage;     // 造成伤害后（触发吸血）
    public event Action<DamageContext> OnReceivingDamage; // 收到伤害前（计算减伤、护盾）
    public event Action<DamageContext> OnReceivedDamage;  // 收到伤害后（触发反击、受击特效）
    public void NotifyDealingDamage(DamageContext context);
    public void NotifyDealtDamage(DamageContext context);
    public void NotifyReceivingDamage(DamageContext context);
    public void NotifyReceivedDamage(DamageContext context);
    public BuffController BuffController { get; set; }
    public bool CanReceiveBuff(Buff buff)
    {
        return true;
    }
    public List<MeshInstance3D> GetVisualMeshes()
    {
        return null;
    }
    public Vector3 GetTargetPositionLocal()
    {
        return Vector3.Zero;
    }

    public List<UnitStatType> GetUnitVisualStatTypes()
    {
        return new List<UnitStatType>
        {
            UnitStatType.PhysicalAttackDamage,
            UnitStatType.MagicAttackDamage,
            UnitStatType.RealDamage,
            UnitStatType.AttackRange,
            UnitStatType.AttackSpeed,
            UnitStatType.CritRate
        };
    }
}

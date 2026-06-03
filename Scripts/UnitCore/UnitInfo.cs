using System;
using Godot;

[GlobalClass]
public partial class UnitInfo : Resource
{
    [Export]
    public float maxHealth = 50f;
    [Export]
    public float maxShield = 0f;
    [Export]
    public float physicalAttackDamage = 10f;
    [Export]
    public float magicalAttackDamage = 0f;
    [Export]
    public float realDamage = 0f;
    [Export]
    public float attackRange = 1f;
    [Export]
    public float attackSpeed = 1f;
    [Export]
    public float physicalDefense = 5f;
    [Export]
    public float magicalDefense = 5f;
    [Export]
    public float moveSpeed = 4f;
    [Export]
    public float critRate = 0f;
}
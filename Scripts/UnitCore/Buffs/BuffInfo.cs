using Godot;
using System;

[GlobalClass]
public partial class BuffInfo : Resource
{
    [Export]
    public float duration = 5f; // -1 代表永久
    [Export]
    public float tickInterval = 1f; // 间隔多久触发一次 OnTick
    [Export]
    public bool infiniteStacks = false;
    [Export]
    public int maxStacks = 1;
    [Export]
    public int priority = 0; // 影响 StatModifier 的优先级
    [Export]
    public bool isDebuff = false;
    [Export]
    public bool changeColor = false;
    [Export]
    public Color color = new Color(1, 1, 1, 1);
    [Export]
    public int visualPriority = 0;
    [Export]
    public string buffName;
    [Export]
    public string buffDes;
    [Export]
    public bool overrideBuffName;
    [Export]
    public bool overrideBuffDes;
    [Export]
    public BuffTag tag;
}
public enum BuffTag
{
    None,
    Cold,
    Poison,
    Flame,
    Stun,
    Vulnerable,
}
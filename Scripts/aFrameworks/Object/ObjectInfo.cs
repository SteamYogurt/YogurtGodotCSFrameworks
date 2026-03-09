using Godot;
using System;

[GlobalClass]
public partial class ObjectInfo : Resource
{
    public string ObjectName { get; set; }
    public string DirPath { get; set; }
    public ObjectPool Pool { get; set; }
}

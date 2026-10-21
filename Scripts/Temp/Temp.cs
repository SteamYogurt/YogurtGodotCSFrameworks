using Godot;
using System;

public partial class Temp : Node3D,IGameObject
{
    [Export]
    public ObjectInfo Info { get; set; }

    [Export]
    public float keepTime = 0;
    protected float keepAcc = 0;

    [Export]
    public bool useAsEffectOneShot = false;

    public bool freeAtEnd = false;
    public override void _EnterTree()
    {
        keepAcc = 0;
        if (useAsEffectOneShot)
        {
            foreach (var child in GetChildren())
            {
                if(child is GpuParticles3D gpu)
                {
                    gpu.OneShot = true;
                    gpu.Restart();
                }
            }
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (keepTime < 0) return;
        keepAcc += (float)delta;
        if(keepAcc > keepTime)
        {
            if(freeAtEnd)QueueFree();
            else
            {
                GetParent().RemoveChild(this);
                Info.Pool.ReturnObjectToPool(this);
            }
          
        }
    }
}

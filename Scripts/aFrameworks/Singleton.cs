using Godot;
using System;
using System.Collections.Generic;

public partial class Singleton<T> : Node where T : Singleton<T>
{
    public Singleton()
    {
        
    }
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                GD.PrintErr(typeof(T) + ":没有被实例化过！");
                return instance;
            }
            return instance;
        }
        private set
        {
            instance = value;
        }
    }
    static T instance;

    T Convert(Object obj)
    {
        return (T)obj;
    }
    public virtual void Reload()
    {

    }
    public override void _EnterTree()
    {
        instance = Convert(this);
        GD.Print(this.GetType() + ":入树，instance改变");
    }
    public override void _ExitTree()
    {
        if (instance == this) instance = null;
    }
}

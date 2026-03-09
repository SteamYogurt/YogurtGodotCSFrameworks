using Godot;
using System;

public partial interface IGameObject
{
    [Export]
    public ObjectInfo Info { get; set; }

    public void OnLoad()
    {

    }
    public void OnInstantiate()
    {

    }
}

using Godot;
using System;

public partial class Game : Node
{
    public static Game instance;
    public CanvasLayer canvasLayer;
    public GameUI gameUI;
    public override void _EnterTree()
    {
        instance = this;
        //gameUI = Global.GetObj<GameUI>("res://zUI/GameUI/GameUI.tscn");
        //canvasLayer.AddChild(gameUI);
    }
    public override void _ExitTree()
    {
        if(instance == this)instance = null;

    }
}

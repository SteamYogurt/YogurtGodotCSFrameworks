using Godot;
using System;

public partial class WaitingPanel : Control
{
    [Export] public Button cancelBtn;
    public override void _Ready()
    {
        VisibilityChanged += ()=>
        {
            if (Visible)
            {
                cancelBtn.GrabFocus();
            }
        };
        cancelBtn.Pressed += () =>
        {
            Main.Instance.ResetAndReturnToMenu();
        };
    }

}

using Godot;
using Godot.Collections;

public partial class InputConfigResource : Resource
{
    // 键鼠绑定：Key = ActionName, Value = InputEvent (Key/Mouse)
    [Export] public Dictionary<string, InputEvent> KbmBindings = new();

    // 手柄绑定：Key = ActionName, Value = InputEvent (Joypad)
    [Export] public Dictionary<string, InputEvent> JoypadBindings = new();
    
    [Export] public bool VibrationEnabled = true;
}
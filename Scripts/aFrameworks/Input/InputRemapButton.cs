using Godot;

public partial class InputRemapButton : Button
{
    [Export] public string ActionName;
    [Export] public InputDeviceType DeviceType;

    private bool _isListening = false;

    public override void _Ready()
    {
        ToggleMode = true;
        ClipText = true;

        FocusMode = FocusModeEnum.All;
    }
    public override void _EnterTree()
    {
        InputManager.Instance.BindingsReset += UpdateText;
        UpdateText();
    }
    public override void _ExitTree()
    {
        InputManager.Instance.BindingsReset -= UpdateText;
    }
    public override void _Toggled(bool toggledOn)
    {
        _isListening = toggledOn;
        if (_isListening)
        {
            Text = "..."; // 等待输入时的占位符
            // 此时按钮持有焦点，所有的输入都会先经过 _Input
        }
        else
        {
            UpdateText();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 如果不在监听状态，完全不理会输入，交给系统处理（比如方向键导航）
        if (!_isListening) return;

        // --- 核心修复 2：拦截逻辑 ---

        // 1. 排除弹起事件和重复事件（防止长按 Enter 导致反复触发）
        if (!@event.IsPressed() || @event.IsEcho()) return;

        // 2. 检查 Esc 键：通常作为“取消改绑”的快捷键
        if (@event is InputEventKey k && k.Keycode == Key.Escape)
        {
            AcceptEvent(); // 消耗掉 Esc，防止弹出菜单
            ButtonPressed = false; // 退出监听状态
            return;
        }

        // 3. 过滤设备类型（比如在手柄列不能按键盘）
        if (!InputManager.Instance.IsEventTypeMatch(@event, DeviceType)) return;

        // 4. 摇杆死区过滤
        if (@event is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) < 0.5f) return;

        // 5. 捕获到有效输入（包括 Enter, 方向键等）
        if (@event is InputEventKey || @event is InputEventMouseButton ||
            @event is InputEventJoypadButton || @event is InputEventJoypadMotion)
        {
            // 关键：在这里消耗掉事件！
            // 这样这个 Enter 或方向键就不会触发 UI 的导航或点击逻辑
            AcceptEvent();
            InputManager.Instance.UpdateBinding(ActionName, @event, DeviceType);

            // 逻辑完成后，通过代码关闭按下状态，自动触发 _Toggled(false)
            ButtonPressed = false;
        }
    }

    public void UpdateText()
    {
        Text = InputManager.Instance.GetActionText(ActionName, DeviceType);
    }
}
using System.Linq;
using Godot;
using Godot.Collections;

public enum InputDeviceType { Kbm, Joypad }

public partial class InputManager : Singleton<InputManager>
{
    private const string SavePath = "user://input_config.tres";
    private InputConfigResource _config;

    // --- 核心修复：使用 Array 存储多个默认绑定，防止“吞”掉备选键位 ---
    private Dictionary<string, Array<InputEvent>> _defaultKbmBindings = new();
    private Dictionary<string, Array<InputEvent>> _defaultJoypadBindings = new();
    readonly string[] _localCoopActionNames =
{
        "move_left",
        "move_right",
        "move_down",
        "move_up",
        "aim_left",
        "aim_right",
        "aim_down",
        "aim_up",
        "firing",
        "roll",
        "knock",
        "reload"
    };

    int _localCoopJoypadDevice = -1;
    public InputDeviceType LastUsedDevice { get; private set; } = InputDeviceType.Kbm;

    [Signal] public delegate void DeviceTypeChangedEventHandler(InputDeviceType newType);
    [Signal] public delegate void BindingsResetEventHandler();

    public override void _Ready()
    {
        // 1. 先记录项目原始的所有默认按键
        CaptureProjectDefaults();
        // 2. 加载玩家自定义的存档并应用
        LoadAndApplyConfig();
    }

    /// <summary>
    /// 记录项目设置中的原始输入映射（支持多键位）
    /// </summary>
    private void CaptureProjectDefaults()
    {
        _defaultKbmBindings.Clear();
        _defaultJoypadBindings.Clear();

        var actions = InputMap.GetActions();
        foreach (var action in actions)
        {
            string actionName = action.ToString();
            if (actionName.StartsWith("ui_")) continue;

            var events = InputMap.ActionGetEvents(action);
            var kbmList = new Array<InputEvent>();
            var joyList = new Array<InputEvent>();

            foreach (var e in events)
            {
                if (IsEventTypeMatch(e, InputDeviceType.Kbm)) kbmList.Add(e);
                else if (IsEventTypeMatch(e, InputDeviceType.Joypad)) joyList.Add(e);
            }

            _defaultKbmBindings[actionName] = kbmList;
            _defaultJoypadBindings[actionName] = joyList;
        }
        GD.Print("InputManager: 已记录完整的默认输入配置（含多键位）");
    }

    /// <summary>
    /// 重置所有按键到项目初始状态
    /// </summary>
    public void ResetToDefault()
    {
        // 1. 清空存档数据
        _config.KbmBindings.Clear();
        _config.JoypadBindings.Clear();

        // 2. 遍历并恢复所有默认事件
        var allActions = InputMap.GetActions();
        foreach (var action in allActions)
        {
            string actionName = action.ToString();
            if (actionName.StartsWith("ui_")) continue;

            InputMap.ActionEraseEvents(actionName);

            // 恢复所有记录的 KBM 默认键位
            if (_defaultKbmBindings.TryGetValue(actionName, out var kbmEvents))
            {
                foreach (var e in kbmEvents) InputMap.ActionAddEvent(actionName, e);
            }

            // 恢复所有记录的 Joypad 默认键位
            if (_defaultJoypadBindings.TryGetValue(actionName, out var joyEvents))
            {
                foreach (var e in joyEvents) InputMap.ActionAddEvent(actionName, e);
            }
        }

        SaveConfig();
        EmitSignal(SignalName.BindingsReset);
        EmitSignal(SignalName.DeviceTypeChanged, (int)LastUsedDevice);
        GD.Print("InputManager: 配置已重置回项目默认状态");
    }

    public override void _Input(InputEvent @event)
    {
        InputDeviceType detectedType = LastUsedDevice;

        if (@event is InputEventKey || @event is InputEventMouseButton)
        {
            detectedType = InputDeviceType.Kbm;
        }
        else if (@event is InputEventJoypadButton || (@event is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > 0.3f))
        {
            detectedType = InputDeviceType.Joypad;
        }

        if (detectedType != LastUsedDevice)
        {
            LastUsedDevice = detectedType;
            EmitSignal(SignalName.DeviceTypeChanged, (int)LastUsedDevice);
            GD.Print($"InputManager: 设备切换至 {LastUsedDevice}");
        }
    }

    // --- 逻辑处理方法 ---

    public void UpdateBinding(string actionName, InputEvent newEvent, InputDeviceType type)
    {
        // 1. 移除可能存在的冲突项
        //RemoveConflict(newEvent, type);
        // 取消移除冲突

        // 2. 移除当前动作中属于该类型的旧绑定（保留另一类型的绑定）
        var allEvents = InputMap.ActionGetEvents(actionName);
        foreach (var e in allEvents)
        {
            if (IsEventTypeMatch(e, type))
                InputMap.ActionEraseEvent(actionName, e);
        }

        // 3. 应用新绑定并更新存档
        InputMap.ActionAddEvent(actionName, newEvent);
        if (type == InputDeviceType.Kbm) _config.KbmBindings[actionName] = newEvent;
        else _config.JoypadBindings[actionName] = newEvent;

        SaveConfig();
        EmitSignal(SignalName.BindingsReset);
    }

    private void ApplyBindingMap(Dictionary<string, InputEvent> map, InputDeviceType type)
    {
        foreach (var (action, @event) in map)
        {
            if (!InputMap.HasAction(action)) continue;

            // 仅清理存档中指定类型的事件，然后替换
            foreach (var e in InputMap.ActionGetEvents(action))
            {
                if (IsEventTypeMatch(e, type))
                    InputMap.ActionEraseEvent(action, e);
            }
            InputMap.ActionAddEvent(action, @event);
        }
    }

    private void RemoveConflict(InputEvent newEvent, InputDeviceType type)
    {
        var actions = InputMap.GetActions();
        foreach (var action in actions)
        {
            var events = InputMap.ActionGetEvents(action);
            foreach (var e in events)
            {
                if (IsEventTypeMatch(e, type) && IsEventEqual(e, newEvent))
                {
                    InputMap.ActionEraseEvent(action, e);
                    // 同步清理存档字典
                    if (type == InputDeviceType.Kbm) _config.KbmBindings.Remove(action);
                    else _config.JoypadBindings.Remove(action);
                }
            }
        }
    }

    // --- 工具方法 ---

    public bool IsEventTypeMatch(InputEvent @event, InputDeviceType type)
    {
        if (type == InputDeviceType.Kbm)
            return @event is InputEventKey || @event is InputEventMouseButton;
        return @event is InputEventJoypadButton || @event is InputEventJoypadMotion;
    }

    private bool IsEventEqual(InputEvent a, InputEvent b)
    {
        if (a.GetType() != b.GetType()) return false;
        if (a is InputEventKey ak && b is InputEventKey bk) return ak.Keycode == bk.Keycode;
        if (a is InputEventMouseButton am && b is InputEventMouseButton bm) return am.ButtonIndex == bm.ButtonIndex;
        if (a is InputEventJoypadButton aj && b is InputEventJoypadButton bj) return aj.ButtonIndex == bj.ButtonIndex;
        if (a is InputEventJoypadMotion ao && b is InputEventJoypadMotion bo) return ao.Axis == bo.Axis;
        return false;
    }

    public string GetActionText(string actionName, InputDeviceType type)
    {
        var events = InputMap.ActionGetEvents(actionName);
        foreach (var e in events)
        {
            if (IsEventTypeMatch(e, type))
            {
                return GetReadableName(e); // 使用我们的清理函数
            }
        }
        return "null";
    }

    // 核心：将混乱的 AsText() 转换为简洁文字
    private string GetReadableName(InputEvent @event)
    {
        // 1. 键盘
        if (@event is InputEventKey k)
        {
            // 优先物理按键（适合自定义输入映射）
            Key code = k.PhysicalKeycode != Key.None
                ? k.PhysicalKeycode
                : k.Keycode;

            string keyName = OS.GetKeycodeString(code);

            // 处理组合键
            var parts = new System.Collections.Generic.List<string>();

            if (k.CtrlPressed) parts.Add("Ctrl");
            if (k.ShiftPressed) parts.Add("Shift");
            if (k.AltPressed) parts.Add("Alt");
            if (k.MetaPressed) parts.Add("Meta");

            parts.Add(keyName);

            return string.Join("+", parts);
        }

        // 2. 鼠标
        if (@event is InputEventMouseButton m)
        {
            return m.ButtonIndex switch
            {
                MouseButton.Left => "LMB",
                MouseButton.Right => "RMB",
                MouseButton.Middle => "MMB",
                MouseButton.WheelUp => "Wheel Up",
                MouseButton.WheelDown => "Wheel Down",
                MouseButton.Xbutton1 => "Mouse 4",
                MouseButton.Xbutton2 => "Mouse 5",
                _ => $"MB {m.ButtonIndex}"
            };
        }

        // 3. 手柄按钮
        if (@event is InputEventJoypadButton j)
        {
            return j.ButtonIndex switch
            {
                JoyButton.A => "A",
                JoyButton.B => "B",
                JoyButton.X => "X",
                JoyButton.Y => "Y",
                JoyButton.Back => "Back",
                JoyButton.Start => "Start",
                JoyButton.LeftShoulder => "L1",
                JoyButton.RightShoulder => "R1",
                JoyButton.LeftStick => "L3",
                JoyButton.RightStick => "R3",
                JoyButton.DpadUp => "DPad Up",
                JoyButton.DpadDown => "DPad Down",
                JoyButton.DpadLeft => "DPad Left",
                JoyButton.DpadRight => "DPad Right",
                _ => $"Joy {j.ButtonIndex}"
            };
        }

        // 4. 手柄轴
        if (@event is InputEventJoypadMotion o)
        {
            return o.Axis switch
            {
                JoyAxis.LeftX => o.AxisValue > 0 ? "LS Right" : "LS Left",
                JoyAxis.LeftY => o.AxisValue > 0 ? "LS Down" : "LS Up",
                JoyAxis.RightX => o.AxisValue > 0 ? "RS Right" : "RS Left",
                JoyAxis.RightY => o.AxisValue > 0 ? "RS Down" : "RS Up",
                JoyAxis.TriggerLeft => "L2",
                JoyAxis.TriggerRight => "R2",
                _ => $"Axis {o.Axis}"
            };
        }

        return @event.AsText();
    }
    private void LoadAndApplyConfig()
    {
        _config = FileAccess.FileExists(SavePath)
            ? ResourceLoader.Load<InputConfigResource>(SavePath)
            : new InputConfigResource();

        ApplyBindingMap(_config.KbmBindings, InputDeviceType.Kbm);
        ApplyBindingMap(_config.JoypadBindings, InputDeviceType.Joypad);
    }

    private void SaveConfig() => ResourceSaver.Save(_config, SavePath);

    public void Vibrate(float weak = 0.5f, float strong = 0.0f, float duration = 0.1f, int device = 0)
    {
        if (!_config.VibrationEnabled || LastUsedDevice != InputDeviceType.Joypad) return;
        Input.StartJoyVibration(device, weak, strong, duration);
    }

    public void StopVibrate(int device = 0) => Input.StopJoyVibration(device);

    public InputDeviceType GetDeviceTypeForLocalSlot(int localSlotIndex)
    {
        return TryGetAssignedJoypadDevice(localSlotIndex, out _)
                ? InputDeviceType.Joypad
                : InputDeviceType.Kbm;
        // return LastUsedDevice;
    }

    bool TryGetAssignedJoypadDevice(int localSlotIndex, out int device)
    {
        device = -1;

        var joypads = Input.GetConnectedJoypads();
        if (joypads.Count <= 0)
            return false;

        if (Game.instance != null && Game.instance.IsLocalCoop)
        {
            if (localSlotIndex < 0)
                return false;

            int localPlayerCount = Mathf.Max(Game.instance.LocalPlayerCount, 1);

            // 手柄足够：所有本地玩家都走手柄
            if (joypads.Count >= localPlayerCount)
            {
                if (localSlotIndex >= 0 && localSlotIndex < localPlayerCount)
                {
                    device = joypads[localSlotIndex];
                    return true;
                }

                return false;
            }

            // 手柄不够：slot0 使用键鼠，其余槽位顺延占用手柄
            if (localSlotIndex == 0)
                return false;

            int joypadIndex = localSlotIndex - 1;
            if (joypadIndex >= 0 && joypadIndex < joypads.Count)
            {
                device = joypads[joypadIndex];
                return true;
            }

            return false;
        }

        if (LastUsedDevice == InputDeviceType.Joypad)
        {
            device = joypads[0];
            return true;
        }

        return false;
    }
    public int GetJoypadDeviceForLocalSlot(int localSlotIndex)
    {
        return TryGetAssignedJoypadDevice(localSlotIndex, out int device)
            ? device
            : -1;
    }


    public float GetActionStrengthForLocalSlot(string actionName, int localSlotIndex)
    {
        if (GetDeviceTypeForLocalSlot(localSlotIndex) == InputDeviceType.Kbm)
            return GetKbmActionStrength(actionName);

        int device = GetJoypadDeviceForLocalSlot(localSlotIndex);
        return GetJoypadActionStrength(actionName, device);
    }
    float GetKbmActionStrength(string actionName)
    {
        if (!InputMap.HasAction(actionName))
            return 0f;

        float strength = 0f;
        float deadzone = InputMap.ActionGetDeadzone(actionName);
        var events = InputMap.ActionGetEvents(actionName);

        foreach (var @event in events)
        {
            if (@event is InputEventKey keyEvent)
            {
                Key keycode = keyEvent.PhysicalKeycode != Key.None
                    ? keyEvent.PhysicalKeycode
                    : keyEvent.Keycode;

                if (keycode != Key.None && Input.IsPhysicalKeyPressed(keycode))
                {
                    strength = Mathf.Max(strength, 1f);
                }
            }
            else if (@event is InputEventMouseButton mouseEvent)
            {
                if (Input.IsMouseButtonPressed(mouseEvent.ButtonIndex))
                {
                    strength = Mathf.Max(strength, 1f);
                }
            }
        }

        return strength > deadzone ? Mathf.Clamp(strength, 0f, 1f) : 0f;
    }
    float GetJoypadActionStrength(string actionName, int device)
    {
        if (device < 0 || !InputMap.HasAction(actionName))
            return 0f;

        float strength = 0f;
        float deadzone = InputMap.ActionGetDeadzone(actionName);

        var events = InputMap.ActionGetEvents(actionName);
        foreach (var @event in events)
        {
            if (@event is InputEventJoypadButton button)
            {
                if (Input.IsJoyButtonPressed(device, button.ButtonIndex))
                {
                    strength = Mathf.Max(strength, 1f);
                }
            }
            else if (@event is InputEventJoypadMotion motion)
            {
                float axisValue = Input.GetJoyAxis(device, motion.Axis);
                float mappedSign = Mathf.Sign(motion.AxisValue);
                if (Mathf.IsZeroApprox(mappedSign))
                    mappedSign = 1f;

                float projected = axisValue * mappedSign;
                if (projected > deadzone)
                {
                    float normalized =
                        (projected - deadzone) /
                        Mathf.Max(1f - deadzone, 0.0001f);

                    strength = Mathf.Max(
                        strength,
                        Mathf.Clamp(normalized, 0f, 1f));
                }
            }
        }

        return Mathf.Clamp(strength, 0f, 1f);
    }
    public bool IsActionPressedForLocalSlot(string actionName, int localSlotIndex)
    {
        float deadzone = InputMap.HasAction(actionName)
            ? InputMap.ActionGetDeadzone(actionName)
            : 0.5f;

        return GetActionStrengthForLocalSlot(actionName, localSlotIndex) > deadzone;
    }

    public float GetAxisForLocalSlot(
        string negativeAction,
        string positiveAction,
        int localSlotIndex)
    {
        float negative = GetActionStrengthForLocalSlot(negativeAction, localSlotIndex);
        float positive = GetActionStrengthForLocalSlot(positiveAction, localSlotIndex);
        return positive - negative;
    }

    public Vector2 GetVectorForLocalSlot(
        string negativeX,
        string positiveX,
        string negativeY,
        string positiveY,
        int localSlotIndex)
    {
        Vector2 result = new Vector2(
            GetAxisForLocalSlot(negativeX, positiveX, localSlotIndex),
            GetAxisForLocalSlot(negativeY, positiveY, localSlotIndex));

        if (result.LengthSquared() > 1f)
            result = result.Normalized();

        return result;
    }

    public bool IsInputEventForLocalSlotAction(
    InputEvent @event,
    string actionName,
    int localSlotIndex)
    {
        if (!InputMap.HasAction(actionName))
            return false;

        if (GetDeviceTypeForLocalSlot(localSlotIndex) == InputDeviceType.Kbm)
        {
            if (@event is not InputEventKey && @event is not InputEventMouseButton)
                return false;

            var mappedEvents = InputMap.ActionGetEvents(actionName);
            foreach (var mappedEvent in mappedEvents)
            {
                if (mappedEvent is InputEventKey mappedKey &&
                    @event is InputEventKey inputKey &&
                    inputKey.Pressed &&
                    !inputKey.IsEcho())
                {
                    Key mappedCode = mappedKey.PhysicalKeycode != Key.None
                        ? mappedKey.PhysicalKeycode
                        : mappedKey.Keycode;
                    Key inputCode = inputKey.PhysicalKeycode != Key.None
                        ? inputKey.PhysicalKeycode
                        : inputKey.Keycode;

                    if (mappedCode != Key.None && mappedCode == inputCode)
                        return true;
                }

                if (mappedEvent is InputEventMouseButton mappedMouse &&
                    @event is InputEventMouseButton inputMouse &&
                    inputMouse.Pressed &&
                    inputMouse.ButtonIndex == mappedMouse.ButtonIndex)
                {
                    return true;
                }
            }

            return false;
        }

        int device = GetJoypadDeviceForLocalSlot(localSlotIndex);
        if (device < 0)
            return false;

        var joyMappedEvents = InputMap.ActionGetEvents(actionName);
        foreach (var mappedEvent in joyMappedEvents)
        {
            if (mappedEvent is InputEventJoypadButton mappedButton &&
                @event is InputEventJoypadButton inputButton &&
                inputButton.Pressed &&
                inputButton.Device == device &&
                inputButton.ButtonIndex == mappedButton.ButtonIndex)
            {
                return true;
            }

            if (mappedEvent is InputEventJoypadMotion mappedMotion &&
                @event is InputEventJoypadMotion inputMotion &&
                inputMotion.Device == device &&
                inputMotion.Axis == mappedMotion.Axis)
            {
                float mappedSign = Mathf.Sign(mappedMotion.AxisValue);
                if (Mathf.IsZeroApprox(mappedSign))
                    mappedSign = 1f;

                float deadzone = InputMap.ActionGetDeadzone(actionName);
                if (inputMotion.AxisValue * mappedSign > deadzone)
                {
                    return true;
                }
            }
        }

        return false;
    }
    public void VibrateForLocalSlot(
        int localSlotIndex,
        float weak = 0.5f,
        float strong = 0.0f,
        float duration = 0.1f)
    {
        if (!_config.VibrationEnabled)
            return;

        int device = GetJoypadDeviceForLocalSlot(localSlotIndex);
        if (device < 0)
            return;

        Input.StartJoyVibration(device, weak, strong, duration);
    }
    public void RefreshDeviceStatus()
    {
        if (Input.GetConnectedJoypads().Count == 0) LastUsedDevice = InputDeviceType.Kbm;
        EmitSignal(SignalName.DeviceTypeChanged, (int)LastUsedDevice);
    }
}
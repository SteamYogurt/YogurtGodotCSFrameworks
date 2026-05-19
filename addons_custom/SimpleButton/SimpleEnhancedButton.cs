using Godot;
using System;

[GlobalClass]
public partial class SimpleEnhancedButton : Button
{
    // ===== 缩放功能 =====
    [ExportGroup("Scale Settings")]
    [Export] public bool scalePress = true;
    [Export] public bool scaleHover = true;
    [Export] public float hoverScale = 1.08f;
    [Export] public float pressScale = 0.95f;
    [Export] public float scaleSmoothSpeed = 24f;
    private Vector2 _targetScale = Vector2.One;

    // ===== 颤动功能 =====
    [ExportGroup("Shake Settings")]
    [Export] public bool shakeOnHover = true;      // 是否启用颤动
    [Export] public float shakeStrength = 0.08f;   // 旋转弧度
    [Export] public float shakeFrequency = 25.0f;  // 颤动频率
    [Export] public float shakeDuration = 0.4f;    // 持续时间
    private float _shakeTimer = 0f;

    public override void _EnterTree()
    {
        _targetScale = Vector2.One;
        Scale = Vector2.One;
    }

    public override void _Ready()
    {
        MouseEntered += OnMouseEntered;
        FocusEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        FocusExited += OnMouseExited;
        ButtonDown += OnButtonDown;
        ButtonUp += OnButtonUp;
        VisibilityChanged += OnResetState;

        Resized += UpdatePivot;
        UpdatePivot();
    }

    private void SimpleEnhancedButton_FocusEntered()
    {
        throw new NotImplementedException();
    }

    private void UpdatePivot()
    {
        PivotOffset = Size / 2;
    }

    private void OnMouseEntered()
    {
        if (Disabled) return;

        if (scaleHover && !IsPressed())
            _targetScale = new Vector2(hoverScale, hoverScale);

        if (shakeOnHover) _shakeTimer = shakeDuration;
    }

    private void OnMouseExited()
    {
        if (!IsPressed()) _targetScale = Vector2.One;

        _shakeTimer = 0f;
        Rotation = 0f;
    }

    private void OnButtonDown()
    {
        //UIStatic.PlayClickEffect();
        if (scalePress)
            _targetScale = new Vector2(pressScale, pressScale);
    }

    private void OnButtonUp()
    {
        if (IsHovered() && scaleHover)
            _targetScale = new Vector2(hoverScale, hoverScale);
        else
            _targetScale = Vector2.One;
    }

    private void OnResetState()
    {
        _targetScale = Vector2.One;
        Scale = Vector2.One; // 显式重置缩放防止状态残留
        Rotation = 0f;
        _shakeTimer = 0f;
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;

        // --- 修复 1: 缩放逻辑 (增加 Clamp 权重) ---
        if (Scale.DistanceSquaredTo(_targetScale) > 0.00001f)
        {
            // 使用 Mathf.Min 确保权重永远不会超过 1.0，防止低帧率下的过冲弹动
            float weight = Mathf.Clamp(scaleSmoothSpeed * fDelta, 0f, 1f);
            Scale = Scale.Lerp(_targetScale, weight);
        }
        else
        {
            Scale = _targetScale;
        }

        // --- 修复 2: 颤动逻辑 ---
        if (_shakeTimer > 0)
        {
            // 先计算衰减，再减去时间，防止 decay 出现负数导致的瞬间反向旋转
            float decay = Mathf.Max(_shakeTimer / shakeDuration, 0f);
            _shakeTimer -= fDelta;

            Rotation = Mathf.Sin((float)Time.GetTicksMsec() * 0.001f * shakeFrequency) * shakeStrength * decay;

            if (_shakeTimer <= 0)
            {
                Rotation = 0f;
            }
        }
    }
}
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
    [Export] public float shakeStrength = 0.15f;   // 旋转弧度
    [Export] public float shakeFrequency = 40.0f;  // 颤动频率
    [Export] public float shakeDuration = 0.4f;    // 持续时间
    private float _shakeTimer = 0f;

    public override void _EnterTree()
    {
        // 初始状态清理
        _targetScale = Vector2.One;
        Scale = Vector2.One;
    }

    public override void _Ready()
    {
        // 信号连接
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        ButtonDown += OnButtonDown;
        ButtonUp += OnButtonUp;
        VisibilityChanged += OnResetState;

        // 确保 Pivot 在中心，旋转和缩放才会围绕中心
        Resized += UpdatePivot;
        UpdatePivot();
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

        // 触发颤动计时
        if (shakeOnHover) _shakeTimer = shakeDuration;
    }

    private void OnMouseExited()
    {
        // 如果松开状态离开，重置缩放
        if (!IsPressed()) _targetScale = Vector2.One;

        _shakeTimer = 0f;
        Rotation = 0f;
    }

    private void OnButtonDown()
    {
        if (scalePress)
            _targetScale = new Vector2(pressScale, pressScale);
    }

    private void OnButtonUp()
    {
        // 弹起时判断是回到悬停缩放还是原始缩放
        if (IsHovered() && scaleHover)
            _targetScale = new Vector2(hoverScale, hoverScale);
        else
            _targetScale = Vector2.One;
    }

    private void OnResetState()
    {
        _targetScale = Vector2.One;
        Rotation = 0f;
        _shakeTimer = 0f;
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;

        // 1. 平滑缩放
        if (Scale.DistanceSquaredTo(_targetScale) > 0.0001f)
        {
            Scale = Scale.Lerp(_targetScale, scaleSmoothSpeed * fDelta);
        }

        // 2. 颤动逻辑 (旋转)
        if (_shakeTimer > 0)
        {
            _shakeTimer -= fDelta;
            float decay = _shakeTimer / shakeDuration;
            // 使用 Sine 函数制造快速往复
            Rotation = Mathf.Sin((float)Time.GetTicksMsec() * 0.001f * shakeFrequency) * shakeStrength * decay;

            if (_shakeTimer <= 0) Rotation = 0f;
        }
    }
}
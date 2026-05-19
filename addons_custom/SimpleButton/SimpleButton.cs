using Godot;
using System;

[GlobalClass]
public partial class SimpleButton : TextureRect
{
    // ===== 基础状态 =====
    public bool hovering = false;
    public bool pressed = false;
    private bool _disabled = false;

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            if (_disabled && pressed) Cancel();
            UpdateColor();
        }
    }

    // ===== 缩放功能 =====
    [ExportGroup("Scale Settings")]
    [Export] public bool scalePress = true;
    [Export] public bool scaleHover = true;
    [Export] public float hoverScale = 1.08f;
    [Export] public float scaleSmoothSpeed = 24f;
    private Vector2 _targetScale = Vector2.One;

    // ===== 颤动功能 (New!) =====
    [ExportGroup("Shake Settings")]
    [Export] public bool shakeOnHover = true;      // 是否启用颤动
    [Export] public float shakeStrength = 0.08f;   // 旋转弧度（约 8.5度）
    [Export] public float shakeFrequency = 25.0f;  // 颤动频率
    [Export] public float shakeDuration = 0.4f;    // 持续时间（秒）
    private float _shakeTimer = 0f;

    public Action PressedDlg = () => { };

    public override void _EnterTree()
    {
        // 初始状态清理
        hovering = false;
        pressed = false;
        _disabled = false;
        _targetScale = Vector2.One;
        Scale = Vector2.One;

        // 材质初始化
        if (Material == null)
            Material = GD.Load<Material>("res://addons_custom/SimpleButton/simp_button_mat.res").Duplicate() as ShaderMaterial;

        UpdateColor();
    }

    public override void _Ready()
    {
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        VisibilityChanged += OnResetState;
        // 确保 Pivot 在中心，旋转和缩放才会围绕中心
        Resized += () => PivotOffset = Size / 2;
        PivotOffset = Size / 2;
    }

    public virtual void OnMouseEntered()
    {
        if (_disabled) return;
        hovering = true;

        if (scaleHover && !pressed)
            _targetScale = new Vector2(hoverScale, hoverScale);

        // 触发颤动计时
        if (shakeOnHover) _shakeTimer = shakeDuration;

        UpdateColor();
    }

    public virtual void OnMouseExited()
    {
        hovering = false;
        if (!pressed) _targetScale = Vector2.One;
        _shakeTimer = 0f; // 离开即停止颤动
        Rotation = 0f;    // 重置旋转
        UpdateColor();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_disabled) return;

        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed) Pressed();
                else Unpressed();
            }
            else if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed)
            {
                Cancel();
            }
        }
    }

    public virtual void Pressed()
    {
        pressed = true;
        if (scalePress) _targetScale = new Vector2(0.95f, 0.95f); // 建议按下时缩小一点，更有打击感
        UpdateColor();
        PressedDlg?.Invoke();
    }

    public virtual void Unpressed()
    {
        if (!pressed) return;
        pressed = false;
        _targetScale = hovering && scaleHover ? new Vector2(hoverScale, hoverScale) : Vector2.One;
        UpdateColor();
    }

    public virtual void Cancel()
    {
        pressed = false;
        _targetScale = Vector2.One;
        Rotation = 0f;
        UpdateColor();
    }

    private void OnResetState()
    {
        hovering = false;
        pressed = false;
        _targetScale = Vector2.One;
        Rotation = 0f;
        _shakeTimer = 0f;
        UpdateColor();
    }

    public void UpdateColor()
    {
        if (Material is not ShaderMaterial mat) return;

        float brightness = 1.0f;
        if (_disabled) brightness = 0.6f;
        else if (pressed) brightness = 0.85f;
        else if (hovering) brightness = 1.15f;

        mat.SetShaderParameter("brightness", brightness);
        Modulate = new Color(brightness, brightness, brightness, 1.0f);
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;

        // 1. 平滑缩放
        Scale = Scale.Lerp(_targetScale, scaleSmoothSpeed * fDelta);

        // 2. 颤动逻辑 (旋转)
        if (_shakeTimer > 0)
        {
            _shakeTimer -= fDelta;
            // 使用 Sine 函数制造快速往复。指数衰减让颤动有收尾感。
            float decay = _shakeTimer / shakeDuration;
            Rotation = Mathf.Sin(Time.GetTicksMsec() * 0.001f * shakeFrequency) * shakeStrength * decay;

            if (_shakeTimer <= 0) Rotation = 0f;
        }
    }
}
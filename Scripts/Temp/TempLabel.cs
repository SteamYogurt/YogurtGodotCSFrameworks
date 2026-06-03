using Godot;
using System;

public partial class TempLabel : Control, IGameObject
{
    public enum DamageColorType { None, Gold, Physical, Magical, Critical, Real }
    public enum DecorationType { None, Gold, Critical, Explosion }
    public enum FloatType { Static, Up, RandomUp }

    HBoxContainer Container;
    TextureRect Icon;
    Label ValueLabel;

    [Export] public ObjectInfo Info { get; set; }
    public float keepTime = 0.5f;

    private float keepAcc = 0;
    private FloatType _currentFloatType = FloatType.Up;
    float _scaler = 1.0f;
    Tween _spawnTween;
    Tween _fadeTween;

    public override void _EnterTree()
    {
        keepAcc = 0;
        Modulate = new Color(1, 1, 1, 0);
        Scale = Vector2.Zero;

        _spawnTween?.Kill();
        _fadeTween?.Kill();
        _spawnTween = null;
        _fadeTween = null;
    }

    public override void _Ready()
    {
        Container = GetNode<HBoxContainer>("H");
        Icon = GetNode<TextureRect>("H/Icon");
        ValueLabel = GetNode<Label>("H/Label");
    }

    public void Setup(string text, DamageColorType colorType,
        DecorationType deco = DecorationType.None,
        FloatType floatType = FloatType.Up,
        float keepTime = 0.75f)
    {
        _currentFloatType = floatType;
        _scaler = colorType == DamageColorType.Critical ? 1.5f : 1f;
        this.keepTime = keepTime;

        ValueLabel.Text = text;
        ValueLabel.SelfModulate = GetColor(colorType);

        Texture2D iconTex = GetDecorationTexture(deco);
        if (iconTex != null)
        {
            Icon.Texture = iconTex;
            Icon.Visible = true;
        }
        else
        {
            Icon.Visible = false;
        }

        if (_currentFloatType == FloatType.RandomUp)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            Position += new Vector2(rng.RandfRange(-5, 5), rng.RandfRange(-5, 5));
        }

        Container.ResetSize();
        Container.Position = -Container.Size / 2;

        PlaySpawnAnimation();
    }

    private void PlaySpawnAnimation()
    {
        Vector2 startPosition = Position;
        _spawnTween?.Kill();
        _fadeTween?.Kill();

        _spawnTween = CreateTween().SetParallel(true);
        _spawnTween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _spawnTween.TweenProperty(this, "scale", _scaler * Vector2.One, 0.3f).From(_scaler * new Vector2(0.4f, 0.4f));
        _spawnTween.TweenProperty(this, "modulate:a", 1.0f, 0.15f);

        switch (_currentFloatType)
        {
            case FloatType.Up:
                _spawnTween.TweenProperty(this, "position", startPosition + new Vector2(0, -50), _scaler * keepTime)
                    .SetTrans(Tween.TransitionType.Linear);
                break;
            case FloatType.RandomUp:
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                Vector2 targetPos = startPosition + new Vector2(rng.RandfRange(-20, 20), rng.RandfRange(-35, -50));
                _spawnTween.TweenProperty(this, "position", targetPos, keepTime)
                    .SetTrans(Tween.TransitionType.Quad);
                break;
        }

        _fadeTween = CreateTween();
        _fadeTween.TweenInterval(_scaler * keepTime * 0.6f);
        _fadeTween.TweenProperty(this, "modulate:a", 0.0f, _scaler * keepTime * 0.4f);
    }

    public override void _PhysicsProcess(double delta)
    {
        keepAcc += (float)delta;
        if (keepAcc > keepTime + 0.25f)
        {
            GetParent().RemoveChild(this);
            Info.Pool.ReturnObjectToPool(this);
        }
    }

    private Color GetColor(DamageColorType type) => type switch
    {
        DamageColorType.Gold => new Color("#F6C019"),
        DamageColorType.Physical => new Color("#ff6b6b"),
        DamageColorType.Magical => new Color("#5a99f2"),
        DamageColorType.Real => new Color("#ffffff"),
        DamageColorType.Critical => new Color("#F6C019"),
        _ => Colors.White
    };

    private Texture2D GetDecorationTexture(DecorationType deco)
    {
        if (deco == DecorationType.Gold)
            return GD.Load<Texture2D>("res://Assets/Art/Icon/GameRes/Gold.png");
        string path = $"res://Assets/Art/Icon/Label/{deco}.png";
        if (!ResourceLoader.Exists(path))
            return null;
        return GD.Load<Texture2D>(path);
    }
}
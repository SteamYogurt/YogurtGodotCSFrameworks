using Godot;

public partial class Main
{
    private Tween tween;

    public void PlayTransition(bool forward)
    {
        tween?.Kill();
        float target = forward ? 1f : 0f;
        SetProgress(1 - target);
        tween = CreateTween();
        tween.TweenMethod(
            Callable.From<float>(SetProgress),
            1 - target,
            target,
            0.7f
        );
    }

    private void SetProgress(float value)
    {
        var mat = transition.Material as ShaderMaterial;
        mat.SetShaderParameter("progress", value);
    }
}

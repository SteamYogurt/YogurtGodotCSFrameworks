using Godot;
using Godot.Collections;
using System.Collections.Generic;

public class PromotionInstance
{
    public Promotion sourcePromotion;
    public Array<PromotionEffect> copiedPromotionEffects;
    readonly List<PromotionEffectHandle> handles = new();

    public bool IsActive { get; private set; }

    public PromotionInstance(Promotion sourcePromotion)
    {
        this.sourcePromotion = sourcePromotion;
        copiedPromotionEffects = new Array<PromotionEffect>();

        if (sourcePromotion?.promotionEffects == null)
        {
            return;
        }

        foreach (PromotionEffect effect in sourcePromotion.promotionEffects)
        {
            if (effect == null)
            {
                continue;
            }

            copiedPromotionEffects.Add(effect.DuplicateDeep() as PromotionEffect);
        }
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        PromotionEffectContext context = new PromotionEffectContext(this);
        for (int i = 0; i < copiedPromotionEffects.Count; i++)
        {
            PromotionEffect effect = copiedPromotionEffects[i];
            if (effect == null)
            {
                continue;
            }

            PromotionEffectHandle handle = effect.Activate(context);
            if (handle != null && handle != PromotionEffectHandle.Empty)
            {
                handles.Add(handle);
            }
        }

        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        for (int i = handles.Count - 1; i >= 0; i--)
        {
            handles[i]?.Dispose();
        }

        handles.Clear();
        IsActive = false;
    }
}

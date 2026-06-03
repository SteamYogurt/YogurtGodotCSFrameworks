using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public enum RarityLevel
{
    Common,
    Uncommon,
    Rare,
    Epic,
}

[GlobalClass]
public partial class Promotion : Resource
{
    static int builtInPromotionId;

    public string promotionName;
    [Export] public string displayName;
    [Export] public Array<PromotionEffect> promotionEffects;
    [Export] public bool overrideDescription = true;
    [Export] public float goldCost = 100;
    [Export] public float rarity = 100;
    [Export] public RarityLevel rarityLevel;
    [Export] public Array<Promotion> prerequisitePromotions = new();

    public bool canAppearInPromotionStore = true;
    public bool showInPromotionUI = true;

    [Export] public Texture2D promotionIcon;

    public string EnsurePromotionName()
    {
        if (!string.IsNullOrEmpty(promotionName))
        {
            return promotionName;
        }

        promotionName = $"builtinPromotion_{Interlocked.Increment(ref builtInPromotionId)}";
        return promotionName;
    }

    public bool CanBeGenerated(HashSet<Promotion> fetchedPromotions)
    {
        if (prerequisitePromotions == null || prerequisitePromotions.Count == 0)
        {
            return true;
        }

        if (fetchedPromotions == null || fetchedPromotions.Count == 0)
        {
            return false;
        }

        foreach (Promotion prerequisitePromotion in prerequisitePromotions)
        {
            if (prerequisitePromotion == null)
            {
                continue;
            }

            if (!PromotionExt.ContainsPromotion(fetchedPromotions, prerequisitePromotion))
            {
                return false;
            }
        }

        return true;
    }

    public virtual string GetDescription()
    {
        if (overrideDescription)
        {
            return Tr(displayName + "Des");
        }

        if (promotionEffects == null || promotionEffects.Count == 0)
        {
            return "No effect.";
        }

        string description = string.Empty;
        foreach (PromotionEffect effect in promotionEffects)
        {
            string des = effect?.GetDescription();
            if (!string.IsNullOrEmpty(des))
            {
                description += des + "\n";
            }
        }

        return description;
    }

    public Texture2D GetPromotionIcon()
    {
        if (promotionIcon != null)
        {
            return promotionIcon;
        }

        string path = $"res://Assets/Art/Icon/Promotion/Icon/{displayName}.png";
        if (ResourceLoader.Exists(path))
        {
            promotionIcon = ResourceLoader.Load<Texture2D>(path);
            return promotionIcon;
        }

        return GD.Load<Texture2D>("res://icon.svg");
    }

    public bool HasIconFromDirectory()
    {
        string path = $"res://Assets/Art/Icon/Promotion/Icon/{displayName}.png";
        return ResourceLoader.Exists(path);
    }
}

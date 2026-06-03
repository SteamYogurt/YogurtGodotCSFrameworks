using System.Collections.Generic;
using System.IO;
using Godot;

public static class PromotionExt
{
    public const string PromotionRootPath = "res://Data/Promotion/";

    public static List<Promotion> allPromotions = new List<Promotion>();
    public static Dictionary<string, Promotion> allPromotionsDict = new Dictionary<string, Promotion>();
    static Dictionary<string, string> promotionPathDict = new Dictionary<string, string>();
    static bool hasScannedPromotionPath;

    public static void PromotionInit()
    {
        EnsurePromotionPathCache();
    }

    public static Promotion GetPromotion(string promotionName)
    {
        if (string.IsNullOrEmpty(promotionName))
        {
            return null;
        }

        if (allPromotionsDict.TryGetValue(promotionName, out Promotion cachedPromotion))
        {
            return cachedPromotion;
        }

        EnsurePromotionPathCache();
        if (!promotionPathDict.TryGetValue(promotionName, out string promotionPath))
        {
            GD.PrintErr("未找到Promotion资源: " + promotionName);
            return null;
        }

        Promotion promotion = GD.Load<Promotion>(promotionPath);
        if (promotion == null)
        {
            GD.PrintErr("Promotion资源加载失败: " + promotionPath);
            return null;
        }

        promotion.promotionName = promotionName;
        allPromotions.Add(promotion);
        allPromotionsDict[promotionName] = promotion;

        GD.Print("加载了promotion资源: " + promotionName);
        return promotion;
    }

    public static List<Promotion> GetAllPromotions()
    {
        EnsurePromotionPathCache();
        foreach (string promotionName in promotionPathDict.Keys)
        {
            GetPromotion(promotionName);
        }

        return allPromotions;
    }

    public static bool HasPromotion(string promotionName)
    {
        if (string.IsNullOrEmpty(promotionName))
        {
            return false;
        }

        EnsurePromotionPathCache();
        return promotionPathDict.ContainsKey(promotionName);
    }

    public static string GetPromotionName(Promotion promotion)
    {
        if (promotion == null)
        {
            return null;
        }

        return promotion.EnsurePromotionName();
    }

    public static bool ContainsPromotion(HashSet<Promotion> promotions, Promotion targetPromotion)
    {
        if (promotions == null || promotions.Count == 0 || targetPromotion == null)
        {
            return false;
        }

        if (promotions.Contains(targetPromotion))
        {
            return true;
        }

        string targetPromotionName = GetPromotionName(targetPromotion);
        if (string.IsNullOrEmpty(targetPromotionName))
        {
            return false;
        }

        foreach (Promotion promotion in promotions)
        {
            if (GetPromotionName(promotion) == targetPromotionName)
            {
                return true;
            }
        }

        return false;
    }

    static void EnsurePromotionPathCache()
    {
        if (hasScannedPromotionPath)
        {
            return;
        }

        hasScannedPromotionPath = true;
        promotionPathDict.Clear();
        LoadPathFrom(PromotionRootPath);
    }

    static void LoadPathFrom(string openPath)
    {
        if (string.IsNullOrEmpty(openPath))
        {
            return;
        }

        if (openPath.Length >= 2 && openPath[openPath.Length - 2] == '_')
        {
            return;
        }

        string[] psArr = ResourceLoader.ListDirectory(openPath);
        foreach (string ps in psArr)
        {
            if (ps[^1] != '/')
            {
                string ext = Path.GetExtension(ps);
                if (ext != ".res" && ext != ".tres")
                {
                    continue;
                }

                if (ps.Length >= 5 && ps[^5] == '_')
                {
                    continue;
                }

                string promotionName = Path.GetFileNameWithoutExtension(ps);
                string promotionPath = openPath + ps;

                if (promotionPathDict.ContainsKey(promotionName))
                {
                    GD.PrintErr("发现重名Promotion资源: " + promotionName + "，路径: " + promotionPath);
                }

                promotionPathDict[promotionName] = promotionPath;
            }
            else
            {
                LoadPathFrom(openPath + ps);
            }
        }
    }

    static bool IsPromotionAvailable(
        Promotion promotion,
        HashSet<Promotion> excludedPromotions,
        HashSet<Promotion> fetchedPromotions)
    {
        if (promotion == null)
        {
            return false;
        }

        if (!promotion.canAppearInPromotionStore)
        {
            return false;
        }

        if (excludedPromotions != null && excludedPromotions.Contains(promotion))
        {
            return false;
        }

        if (!promotion.CanBeGenerated(fetchedPromotions))
        {
            return false;
        }

        if (promotion.rarity <= 0)
        {
            return false;
        }

        return true;
    }

    public static Promotion GetRandomPromotionByWeight(
        HashSet<Promotion> excludedPromotions,
        HashSet<Promotion> fetchedPromotions = null)
    {
        List<Promotion> promotions = GetAllPromotions();
        float totalWeight = 0;
        Promotion lastValidPromotion = null;

        foreach (Promotion promotion in promotions)
        {
            if (!IsPromotionAvailable(promotion, excludedPromotions, fetchedPromotions))
            {
                continue;
            }

            totalWeight += promotion.rarity;
            lastValidPromotion = promotion;
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        float randomValue = GD.Randf() * totalWeight;
        float currentWeight = 0;

        foreach (Promotion promotion in promotions)
        {
            if (!IsPromotionAvailable(promotion, excludedPromotions, fetchedPromotions))
            {
                continue;
            }

            currentWeight += promotion.rarity;
            if (randomValue <= currentWeight)
            {
                return promotion;
            }
        }

        return lastValidPromotion;
    }
}

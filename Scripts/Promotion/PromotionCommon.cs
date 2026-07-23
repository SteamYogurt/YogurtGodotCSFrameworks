using System.Collections.Generic;
using Godot;

/// <summary>
/// Promotion definition catalog (lazy). ID = filename under <see cref="PromotionRootPath"/>.
/// Domain helpers (store weight pick, name equality) stay here.
/// </summary>
public static class PromotionExt
{
	public const string PromotionRootPath = "res://Data/Promotion/";

	public static readonly ResourceCatalog<Promotion> Catalog = CreateCatalog();

	static ResourceCatalog<Promotion> CreateCatalog()
	{
		ResourceCatalog<Promotion> catalog = new(PromotionRootPath, "Promotion", OnLoaded);
		ResourceCatalogRegistry.Register(catalog);
		return catalog;
	}

	static bool OnLoaded(Promotion promotion, string id)
	{
		promotion.promotionName = id;
		return true;
	}

	public static void Init(string rootPath = null) => Catalog.Init(rootPath);

	public static Promotion GetPromotion(string promotionName) => Catalog.Get(promotionName);

	public static bool TryGetPromotion(string promotionName, out Promotion promotion)
		=> Catalog.TryGet(promotionName, out promotion);

	public static bool HasPromotion(string promotionName) => Catalog.Has(promotionName);

	public static IReadOnlyList<Promotion> GetAllPromotions() => Catalog.GetAll();

	public static IReadOnlyList<Promotion> AllPromotions => Catalog.LoadedItems;

	public static IReadOnlyDictionary<string, Promotion> AllPromotionsDict => Catalog.LoadedDict;

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
		IReadOnlyList<Promotion> promotions = GetAllPromotions();
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

using System.Collections.Generic;
using Godot;

/// <summary>
/// Buff definition catalog (lazy). ID = filename under <see cref="BuffRootPath"/>.
/// </summary>
public static class BuffExt
{
	public const string BuffRootPath = "res://Data/Buff/";

	public static readonly ResourceCatalog<Buff> Catalog = CreateCatalog();

	static ResourceCatalog<Buff> CreateCatalog()
	{
		ResourceCatalog<Buff> catalog = new(BuffRootPath, "Buff", OnLoaded);
		ResourceCatalogRegistry.Register(catalog);
		return catalog;
	}

	static bool OnLoaded(Buff buff, string id)
	{
		buff.BindCatalogId(id);
		if (buff.buffInfo == null)
		{
			GD.PrintErr($"Buff.buffInfo == null: {id}");
			return false;
		}

		return true;
	}

	public static void Init(string rootPath = null) => Catalog.Init(rootPath);

	public static Buff GetBuff(string buffId) => Catalog.Get(buffId);

	public static bool TryGetBuff(string buffId, out Buff buff) => Catalog.TryGet(buffId, out buff);

	public static bool HasBuff(string buffId) => Catalog.Has(buffId);

	public static IReadOnlyList<Buff> GetAllBuffs() => Catalog.GetAll();

	public static IReadOnlyList<Buff> AllBuffs => Catalog.LoadedItems;

	public static IReadOnlyDictionary<string, Buff> AllBuffsDict => Catalog.LoadedDict;
}

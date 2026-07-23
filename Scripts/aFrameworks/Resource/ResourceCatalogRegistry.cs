using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Central registry for <see cref="ResourceCatalog{T}"/> instances.
/// Concrete projects can register more catalogs and query by type.
/// </summary>
public static class ResourceCatalogRegistry
{
	static readonly Dictionary<Type, IResourceCatalog> catalogs = new();

	public static void Register<T>(ResourceCatalog<T> catalog) where T : Resource
	{
		if (catalog == null)
		{
			throw new ArgumentNullException(nameof(catalog));
		}

		catalogs[typeof(T)] = catalog;
	}

	public static bool TryGetCatalog<T>(out ResourceCatalog<T> catalog) where T : Resource
	{
		if (catalogs.TryGetValue(typeof(T), out IResourceCatalog raw) && raw is ResourceCatalog<T> typed)
		{
			catalog = typed;
			return true;
		}

		catalog = null;
		return false;
	}

	public static ResourceCatalog<T> Of<T>() where T : Resource
	{
		if (TryGetCatalog(out ResourceCatalog<T> catalog))
		{
			return catalog;
		}

		GD.PrintErr($"未注册资源目录: {typeof(T).Name}");
		return null;
	}

	public static bool HasCatalog<T>() where T : Resource => catalogs.ContainsKey(typeof(T));

	public static T Get<T>(string id) where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		return catalog == null ? null : catalog.Get(id);
	}

	public static bool Has<T>(string id) where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		return catalog != null && catalog.Has(id);
	}

	public static bool TryGet<T>(string id, out T resource) where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		if (catalog == null)
		{
			resource = null;
			return false;
		}

		return catalog.TryGet(id, out resource);
	}

	public static IReadOnlyList<T> GetAll<T>() where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		return catalog == null ? Array.Empty<T>() : catalog.GetAll();
	}

	public static T Find<T>(Func<T, bool> predicate) where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		return catalog == null ? null : catalog.Find(predicate);
	}

	public static List<T> FindAll<T>(Func<T, bool> predicate) where T : Resource
	{
		ResourceCatalog<T> catalog = Of<T>();
		return catalog == null ? new List<T>() : catalog.FindAll(predicate);
	}

	/// <summary>Init every registered catalog (optional root overrides are per-catalog).</summary>
	public static void InitAll()
	{
		foreach (IResourceCatalog catalog in catalogs.Values)
		{
			catalog.Init();
		}
	}

	public static IReadOnlyCollection<IResourceCatalog> AllCatalogs => catalogs.Values;
}

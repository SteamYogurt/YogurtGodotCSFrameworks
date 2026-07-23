using System;
using System.Collections.Generic;
using System.IO;
using Godot;

/// <summary>
/// Non-generic catalog surface for registry registration and cross-type queries.
/// </summary>
public interface IResourceCatalog
{
	Type ResourceType { get; }
	string RootPath { get; }
	string TypeLabel { get; }

	void Init(string rootPath = null);
	void Clear();
	bool Has(string id);
	Resource GetResource(string id);
	IReadOnlyList<string> GetIds();
	int PathCount { get; }
	int LoadedCount { get; }
}

/// <summary>
/// Lazy Resource catalog: scan paths first, load instances on demand.
/// ID = filename without extension. Skips names/folders ending with '_'.
/// </summary>
public sealed class ResourceCatalog<T> : IResourceCatalog where T : Resource
{
	readonly string typeLabel;
	readonly Func<T, string, bool> onLoaded;
	readonly HashSet<string> extensions;

	string rootPath;
	bool scanned;
	readonly Dictionary<string, string> pathDict = new();
	readonly Dictionary<string, T> loadedDict = new();
	readonly List<T> loadedList = new();

	public Type ResourceType => typeof(T);
	public string RootPath => rootPath;
	public string TypeLabel => typeLabel;
	public int PathCount
	{
		get
		{
			EnsurePathCache();
			return pathDict.Count;
		}
	}
	public int LoadedCount => loadedDict.Count;

	public IReadOnlyList<T> LoadedItems => loadedList;
	public IReadOnlyDictionary<string, T> LoadedDict => loadedDict;

	public ResourceCatalog(
		string rootPath,
		string typeLabel = null,
		Func<T, string, bool> onLoaded = null,
		IEnumerable<string> extensions = null)
	{
		if (string.IsNullOrEmpty(rootPath))
		{
			throw new ArgumentException("rootPath is required.", nameof(rootPath));
		}

		this.rootPath = NormalizeRootPath(rootPath);
		this.typeLabel = string.IsNullOrEmpty(typeLabel) ? typeof(T).Name : typeLabel;
		this.onLoaded = onLoaded;
		this.extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (extensions == null)
		{
			this.extensions.Add(".res");
			this.extensions.Add(".tres");
		}
		else
		{
			foreach (string extension in extensions)
			{
				if (!string.IsNullOrEmpty(extension))
				{
					this.extensions.Add(extension.StartsWith('.') ? extension : "." + extension);
				}
			}
		}
	}

	/// <summary>Rescan path index. Optionally override root. Clears previously loaded instances.</summary>
	public void Init(string rootPath = null)
	{
		if (!string.IsNullOrEmpty(rootPath))
		{
			this.rootPath = NormalizeRootPath(rootPath);
		}

		Clear();
		EnsurePathCache();
	}

	public void Clear()
	{
		pathDict.Clear();
		loadedDict.Clear();
		loadedList.Clear();
		scanned = false;
	}

	public bool Has(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return false;
		}

		EnsurePathCache();
		return pathDict.ContainsKey(id);
	}

	public T Get(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}

		if (loadedDict.TryGetValue(id, out T cached))
		{
			return cached;
		}

		EnsurePathCache();
		if (!pathDict.TryGetValue(id, out string path))
		{
			GD.PrintErr($"未找到{typeLabel}资源: {id}");
			return null;
		}

		T resource = GD.Load<T>(path);
		if (resource == null)
		{
			GD.PrintErr($"{typeLabel}资源加载失败: {path}");
			return null;
		}

		if (onLoaded != null && !onLoaded(resource, id))
		{
			return null;
		}

		loadedList.Add(resource);
		loadedDict[id] = resource;
		return resource;
	}

	public Resource GetResource(string id) => Get(id);

	public bool TryGet(string id, out T resource)
	{
		resource = Get(id);
		return resource != null;
	}

	/// <summary>Forces load of every scanned path, then returns loaded instances.</summary>
	public IReadOnlyList<T> GetAll()
	{
		EnsurePathCache();
		foreach (string id in pathDict.Keys)
		{
			Get(id);
		}

		return loadedList;
	}

	public IReadOnlyList<string> GetIds()
	{
		EnsurePathCache();
		return new List<string>(pathDict.Keys);
	}

	public T Find(Func<T, bool> predicate)
	{
		if (predicate == null)
		{
			return null;
		}

		foreach (T item in GetAll())
		{
			if (predicate(item))
			{
				return item;
			}
		}

		return null;
	}

	public List<T> FindAll(Func<T, bool> predicate)
	{
		List<T> results = new();
		if (predicate == null)
		{
			return results;
		}

		foreach (T item in GetAll())
		{
			if (predicate(item))
			{
				results.Add(item);
			}
		}

		return results;
	}

	void EnsurePathCache()
	{
		if (scanned)
		{
			return;
		}

		scanned = true;
		pathDict.Clear();
		ScanPath(rootPath);
	}

	void ScanPath(string openPath)
	{
		if (string.IsNullOrEmpty(openPath) || ShouldSkipDirectory(openPath))
		{
			return;
		}

		string[] entries = ResourceLoader.ListDirectory(openPath);
		foreach (string entry in entries)
		{
			if (entry.Length == 0)
			{
				continue;
			}

			if (entry[^1] == '/')
			{
				ScanPath(openPath + entry);
				continue;
			}

			string extension = Path.GetExtension(entry);
			if (!extensions.Contains(extension))
			{
				continue;
			}

			string id = Path.GetFileNameWithoutExtension(entry);
			if (string.IsNullOrEmpty(id) || id.EndsWith('_'))
			{
				continue;
			}

			string fullPath = openPath + entry;
			if (pathDict.ContainsKey(id))
			{
				GD.PrintErr($"发现重名{typeLabel}资源: {id}，路径: {fullPath}");
			}

			pathDict[id] = fullPath;
		}
	}

	static string NormalizeRootPath(string path)
	{
		return path.EndsWith('/') ? path : path + "/";
	}

	static bool ShouldSkipDirectory(string path)
	{
		string trimmed = path.EndsWith('/') ? path[..^1] : path;
		int slash = trimmed.LastIndexOf('/');
		string folderName = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
		return folderName.EndsWith('_');
	}
}

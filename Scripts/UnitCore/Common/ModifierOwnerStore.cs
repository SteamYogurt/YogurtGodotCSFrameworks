using System.Runtime.CompilerServices;

/// <summary>
/// ConditionalWeakTable store for per-owner modifier controllers.
/// </summary>
public static class ModifierOwnerStore<TController> where TController : class, new()
{
	static ConditionalWeakTable<object, TController> controllers = new();

	public static TController Get(object owner)
	{
		if (owner == null)
		{
			return null;
		}

		return controllers.GetValue(owner, _ => new TController());
	}

	public static bool TryGet(object owner, out TController controller)
	{
		controller = null;
		if (owner == null)
		{
			return false;
		}

		return controllers.TryGetValue(owner, out controller);
	}

	public static void ResetAll()
	{
		controllers = new ConditionalWeakTable<object, TController>();
	}
}

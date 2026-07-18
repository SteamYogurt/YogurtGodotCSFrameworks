public class BuffModifierController : ModifierController<BuffModifier>
{
	public BuffModifierController()
		: base(
			static m => m.Priority,
			static m => m.RuntimeSource)
	{
	}
}

public static class BuffModifierOwnerExt
{
	public static BuffModifierController GetBuffModifierController(this object owner) =>
		ModifierOwnerStore<BuffModifierController>.Get(owner);

	public static bool TryGetBuffModifierController(
		this object owner,
		out BuffModifierController controller) =>
		ModifierOwnerStore<BuffModifierController>.TryGet(owner, out controller);

	public static void ResetAllBuffModifierControllers() =>
		ModifierOwnerStore<BuffModifierController>.ResetAll();
}

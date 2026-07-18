public class DamageModifierController : ModifierController<DamageModifier>
{
	public DamageModifierController()
		: base(
			static m => m.Priority,
			static m => m.RuntimeSource)
	{
	}
}

public static class DamageModifierOwnerExt
{
	public static DamageModifierController GetDamageModifierController(this object owner) =>
		ModifierOwnerStore<DamageModifierController>.Get(owner);

	public static bool TryGetDamageModifierController(
		this object owner,
		out DamageModifierController controller) =>
		ModifierOwnerStore<DamageModifierController>.TryGet(owner, out controller);

	public static void ResetAllDamageModifierControllers() =>
		ModifierOwnerStore<DamageModifierController>.ResetAll();
}

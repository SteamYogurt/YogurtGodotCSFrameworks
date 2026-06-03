public enum ConditionSubjectKey
{
    None,
    Attacker,
    Target,
    Caster,
    Source,
    Subject,
    DamageContext,
    Position,
    LaunchPosition,
    TargetPosition,
}

public static class ConditionSubjectKeys
{
    public const ConditionSubjectKey None = ConditionSubjectKey.None;
    public const ConditionSubjectKey Attacker = ConditionSubjectKey.Attacker;
    public const ConditionSubjectKey Target = ConditionSubjectKey.Target;
    public const ConditionSubjectKey Caster = ConditionSubjectKey.Caster;
    public const ConditionSubjectKey Source = ConditionSubjectKey.Source;
    public const ConditionSubjectKey Subject = ConditionSubjectKey.Subject;
    public const ConditionSubjectKey DamageContext = ConditionSubjectKey.DamageContext;
    public const ConditionSubjectKey Position = ConditionSubjectKey.Position;
    public const ConditionSubjectKey LaunchPosition = ConditionSubjectKey.LaunchPosition;
    public const ConditionSubjectKey TargetPosition = ConditionSubjectKey.TargetPosition;
}

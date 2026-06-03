using Godot;
using Godot.Collections;

public static class ConditionEvaluator
{
    public static bool All(Array<Condition> conditions, ConditionContext context)
    {
        return Evaluate(conditions, context, requireAll: true);
    }

    public static bool Any(Array<Condition> conditions, ConditionContext context)
    {
        return Evaluate(conditions, context, requireAll: false);
    }

    public static bool Evaluate(Array<Condition> conditions, ConditionContext context, bool requireAll)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return true;
        }

        bool anyMatched = false;
        for (int i = 0; i < conditions.Count; i++)
        {
            Condition condition = conditions[i];
            if (condition == null)
            {
                continue;
            }

            bool matched = condition.IsMatch(context);
            if (matched)
            {
                anyMatched = true;
                if (!requireAll)
                {
                    return true;
                }
            }
            else if (requireAll)
            {
                return false;
            }
        }

        return requireAll ? true : anyMatched;
    }
}

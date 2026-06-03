using System;
using System.Collections.Generic;

public static class ModifierCollectionUtil
{
    public static void InsertSortedByPriority<T>(List<T> list, T item, Func<T, int> getPriority)
    {
        if (item == null)
        {
            return;
        }

        int priority = getPriority(item);
        int insertIndex = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (getPriority(list[i]) > priority)
            {
                insertIndex = i;
                break;
            }
        }

        list.Insert(insertIndex, item);
    }

    public static void MergeSortedByPriority<T>(
        List<T> target,
        IReadOnlyList<T> source,
        Func<T, int> getPriority)
    {
        if (source == null || source.Count == 0)
        {
            return;
        }

        if (target.Count == 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item != null)
                {
                    target.Add(item);
                }
            }

            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            T item = source[i];
            if (item != null)
            {
                InsertSortedByPriority(target, item, getPriority);
            }
        }
    }
}

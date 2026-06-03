using System;
using System.Collections.Generic;
using Godot;

public sealed class PromotionEffectHandle : IDisposable
{
    public static PromotionEffectHandle Empty { get; } = new();

    readonly List<IDisposable> disposables = new();
    readonly List<Action> cleanups = new();
    bool disposed;

    public void AddSubscription(IDisposable subscription)
    {
        if (subscription == null || disposed)
        {
            return;
        }

        disposables.Add(subscription);
    }

    public void AddCleanup(Action cleanup)
    {
        if (cleanup == null || disposed)
        {
            return;
        }

        cleanups.Add(cleanup);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        for (int i = disposables.Count - 1; i >= 0; i--)
        {
            disposables[i]?.Dispose();
        }
        disposables.Clear();

        for (int i = cleanups.Count - 1; i >= 0; i--)
        {
            try
            {
                cleanups[i]?.Invoke();
            }
            catch (Exception e)
            {
                GD.PrintErr($"Promotion effect cleanup failed: {e}");
            }
        }
        cleanups.Clear();
    }
}

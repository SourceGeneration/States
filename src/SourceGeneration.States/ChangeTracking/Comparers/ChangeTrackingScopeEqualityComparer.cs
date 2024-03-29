﻿using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SourceGeneration.States;

public sealed class ChangeTrackingScopeEqualityComparer<TValue>(ChangeTrackingScope changeTrackingScope) : IEqualityComparer<TValue>
{
    public bool Equals(TValue? x, TValue? y)
    {
        if (changeTrackingScope == ChangeTrackingScope.Always)
            return false;

        if (!EqualityComparer<TValue>.Default.Equals(x, y))
            return false;

        if (y is IChangeTracking tracking && tracking.IsChanged)
        {
            if (changeTrackingScope == ChangeTrackingScope.Root)
            {
                if (y is ICascadingChangeTracking cascading)
                    return cascading.IsCascadingChanged;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode([DisallowNull] TValue obj) => obj.GetHashCode();
}


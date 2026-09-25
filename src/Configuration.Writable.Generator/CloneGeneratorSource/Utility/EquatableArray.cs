using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IDeepCloneable.Generator;

/// <summary>
/// An immutable array that implements value-based equality.
/// This is used in incremental generators to ensure proper caching behavior.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[] _array;

    public EquatableArray(T[] array)
    {
        _array = array ?? [];
    }

    public EquatableArray(IEnumerable<T> items)
    {
        _array = items?.ToArray() ?? [];
    }

    public int Count => _array.Length;

    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.Length != other._array.Length)
            return false;

        for (int i = 0; i < _array.Length; i++)
        {
            var item1 = _array[i];
            var item2 = other._array[i];

            if (item1 is null && item2 is null)
                continue;

            if (item1 is null || item2 is null)
                return false;

            if (!item1.Equals(item2))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var item in _array)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)_array).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _array.GetEnumerator();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}

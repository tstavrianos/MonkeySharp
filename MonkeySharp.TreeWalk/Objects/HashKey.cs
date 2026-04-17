using System;

namespace MonkeySharp.TreeWalk.Objects;

internal readonly struct HashKey : IEquatable<HashKey>
{
    public string Type { get; }

    public ulong Value { get; }

    internal HashKey(string objectType, ulong value)
    {
        Type = objectType;
        Value = value;
    }

    public bool Equals(HashKey other)
    {
        return Type == other.Type && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        return obj is HashKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Value);
    }

    public static bool operator ==(HashKey left, HashKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HashKey left, HashKey right)
    {
        return !left.Equals(right);
    }
}

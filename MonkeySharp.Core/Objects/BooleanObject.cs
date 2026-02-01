using System;

namespace MonkeySharp.Core.Objects;

public static partial class ObjectType
{
    public const string Boolean = "BOOLEAN";
}

public readonly struct BooleanObject : IHashableObject, IEquatable<BooleanObject>
{
    public static readonly BooleanObject False = new(false);
    public static readonly BooleanObject True = new(true);
    private readonly ulong _hash;

    private BooleanObject(bool value)
    {
        Value = value;
        _hash = (ulong) (value ? 1 : 0);
    }

    public string Type => ObjectType.Boolean;
    public bool Value { get; }

    public string Inspect => Value.ToString().ToLower();

    public HashKey HashKey()
    {
        return new HashKey(Type, _hash);
    }

    public bool Equals(BooleanObject other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is BooleanObject other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(BooleanObject left, BooleanObject right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BooleanObject left, BooleanObject right)
    {
        return !left.Equals(right);
    }
}
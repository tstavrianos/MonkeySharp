namespace MonkeySharp.Core.Objects;

public static partial class ObjectType
{
    public const string Integer = "INTEGER";
}

public readonly struct IntegerObject : IHashableObject
{
    private static readonly IObject[] _cache = new IObject[256];

    static IntegerObject()
    {
        // Pre-box common integers (-128 to 127)
        for (var i = -128; i < 128; i++) _cache[i + 128] = new IntegerObject(i);
    }

    public static IObject Create(long value)
    {
        if (value is >= -128 and < 128)
            return _cache[value + 128];

        return new IntegerObject(value); // Will be boxed by caller
    }

    private readonly ulong _hash;

    internal IntegerObject(long value)
    {
        Value = value;
        _hash = (ulong) value;
    }

    public string Type => ObjectType.Integer;
    public long Value { get; }

    public string Inspect => Value.ToString();

    public HashKey HashKey()
    {
        return new HashKey(Type, _hash);
    }
}
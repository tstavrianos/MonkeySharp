using System.Text;

namespace MonkeySharp.Core.Objects;

public static partial class ObjectType
{
    public const string String = "STRING";
}

public class StringObject : IHashableObject
{
    private readonly ulong _hash;

    private const ulong Basis = 0xCBF29CE484222325UL;
    private const ulong Prime = 0x00000100000001B3UL;

    internal StringObject(string value)
    {
        Value = value;
        var byteData = Encoding.ASCII.GetBytes(value);
        _hash = Basis;
        foreach (var b in byteData)
        {
            _hash ^= b;
            _hash *= Prime;
        }
    }

    public static StringObject Create(string value)
    {
        return new StringObject(value);
    }

    public string Type => ObjectType.String;
    public string Value { get; }

    public string Inspect => Value;

    public HashKey HashKey()
    {
        return new HashKey(Type, _hash);
    }
}
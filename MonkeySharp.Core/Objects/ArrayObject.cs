using System.Collections.Generic;
using System.Linq;

namespace MonkeySharp.Core.Objects;

public static partial class ObjectType
{
    public const string Array = "ARRAY";
}

public class ArrayObject : IObject
{
    internal ArrayObject(IReadOnlyList<IObject> elements)
    {
        Elements = new List<IObject>(elements);
    }

    public string Type => ObjectType.Array;
    public List<IObject> Elements { get; }
    public string Inspect => $"[{string.Join(", ", Elements.Select(x => x.Inspect))}]";
}
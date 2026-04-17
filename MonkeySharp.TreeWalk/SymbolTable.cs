using System.Collections.Generic;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

internal sealed class SymbolTable
{
    private readonly Dictionary<string, Value> _store;
    private readonly SymbolTable? _outer;

    public SymbolTable(SymbolTable? outer = null, int? capacity = null)
    {
        _outer = outer;
        _store =
            capacity != null
                ? new Dictionary<string, Value>(capacity.Value)
                : new Dictionary<string, Value>();
    }

    public (Value, bool) Get(string name)
    {
        var current = this;
        while (current != null)
        {
            if (current._store.TryGetValue(name, out var value))
                return (value, true);

            current = current._outer;
        }

        return (Value.NullValue, false);
    }

    public Value Set(string name, Value value)
    {
        _store[name] = value;
        return value;
    }
}

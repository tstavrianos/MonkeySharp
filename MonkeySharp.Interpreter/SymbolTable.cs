using System.Collections.Generic;
using MonkeySharp.Interpreter.Objects;

namespace MonkeySharp.Interpreter;

public class SymbolTable
{
    private readonly Dictionary<string, Value> _store;
    private readonly SymbolTable _outer;

    public SymbolTable(SymbolTable outer = null, int? capacity = null)
    {
        _outer = outer;
        _store = capacity != null
            ? new Dictionary<string, Value>(capacity.Value)
            : new Dictionary<string, Value>();
    }

    public (Value, bool) Get(string name)
    {
        if (_store.TryGetValue(name, out var value)) return (value, true);
        if (_outer != null)
        {
            var (obj, found) = _outer.Get(name);
            if (found)
                // Convert IObject to Value
                return (obj, true);
        }

        return (Value.NullValue, false);
    }

    public Value Set(string name, Value value)
    {
        _store[name] = value;
        return value;
    }
}
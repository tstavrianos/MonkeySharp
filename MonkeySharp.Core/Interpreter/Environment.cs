using MonkeySharp.Core.Objects;
using System.Collections.Generic;

namespace MonkeySharp.Core.Interpreter;

public class Environment
{
    private readonly Dictionary<string, Value> _store;
    private readonly Environment _outer;

    public Environment(Environment outer = null, int? capacity = null)
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

        return (Value.Null(), false);
    }

    public Value Set(string name, Value value)
    {
        _store[name] = value;
        return value;
    }
}
using System.Collections.Generic;
using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.Interpreter
{
    public class Environment(Environment outer = null)
    {
        private readonly Dictionary<string, IObject> _store = [];

        public (IObject, bool) Get(string name)
        {
            if (_store.TryGetValue(name, out var value)) return (value, true);
            if (outer != null) return outer.Get(name);
            return (null, false);
        }

        public IObject Set(string name, IObject value)
        {
            _store[name] = value;
            return value;
        }
    }
}
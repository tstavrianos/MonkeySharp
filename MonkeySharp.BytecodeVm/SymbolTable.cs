using System.Collections.Generic;

namespace MonkeySharp.BytecodeVm;

internal sealed class SymbolTable
{
    private readonly Dictionary<string, Symbol> _store = [];
    public int NumDefinitions { get; private set; }
    public readonly SymbolTable? Outer;
    public readonly List<Symbol> FreeSymbols = [];

    public SymbolTable(SymbolTable? outer = null)
    {
        Outer = outer;
    }

    public Symbol Define(string name)
    {
        var symbol = new Symbol(
            name,
            Outer == null ? SymbolScope.Global : SymbolScope.Local,
            NumDefinitions
        );
        _store[name] = symbol;
        NumDefinitions++;
        return symbol;
    }

    public bool Resolve(string name, out Symbol result)
    {
        // Walk outward iteratively to avoid recursion overhead and deep-stack risk.
        var missedScopes = new List<SymbolTable>();
        var current = this;

        while (current != null)
        {
            if (current._store.TryGetValue(name, out var found))
            {
                if (
                    current == this
                    || found.Scope == SymbolScope.Global
                    || found.Scope == SymbolScope.Builtin
                )
                {
                    result = found;
                    return true;
                }

                // Mirror recursive unwind behavior: define free symbols from nearest outer
                // miss toward the current scope.
                for (var i = missedScopes.Count - 1; i >= 0; i--)
                    found = missedScopes[i].DefineFree(found);

                result = found;
                return true;
            }

            missedScopes.Add(current);
            current = current.Outer;
        }

        result = default;
        return false;
    }

    internal void DefineBuiltin(int index, string name)
    {
        _store[name] = new Symbol(name, SymbolScope.Builtin, index);
    }

    private Symbol DefineFree(Symbol original)
    {
        FreeSymbols.Add(original);
        return _store[original.Name] = new Symbol(
            original.Name,
            SymbolScope.Free,
            FreeSymbols.Count - 1
        );
    }

    internal void DefineFunctionName(string name)
    {
        _store[name] = new Symbol(name, SymbolScope.Function, 0);
    }
}

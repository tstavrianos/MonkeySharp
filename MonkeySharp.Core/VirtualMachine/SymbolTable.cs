using System.Collections.Generic;

namespace MonkeySharp.Core.VirtualMachine
{
    public class SymbolTable
    {
        private readonly Dictionary<string, Symbol> _store = [];
        public int NumDefinitions { get; private set; }
        public readonly SymbolTable Outer;
        public readonly List<Symbol> FreeSymbols = [];

        public SymbolTable(SymbolTable outer = null)
        {
            Outer = outer;
        }

        public Symbol Define(string name)
        {
            var symbol = new Symbol(name, Outer == null ? SymbolScope.Global : SymbolScope.Local, NumDefinitions);
            _store[name] = symbol;
            NumDefinitions++;
            return symbol;
        }

        public bool Resolve(string name, out Symbol result)
        {
            var ok = _store.TryGetValue(name, out result);
            if (!ok && Outer != null)
            {
                ok = Outer.Resolve(name, out result);
                if (!ok) return false;

                if (result.Scope == SymbolScope.Global || result.Scope == SymbolScope.Builtin)
                    return true;

                var free = DefineFree(result);
                result = free;
                return true;
            }

            return ok;
        }

        internal Symbol DefineBuiltin(int index, string name)
        {
            var symbol = new Symbol(name, SymbolScope.Builtin, index);
            _store[name] = symbol;
            return symbol;
        }

        internal Symbol DefineFree(Symbol original)
        {
            FreeSymbols.Add(original);
            var symbol = new Symbol(original.Name, SymbolScope.Free, FreeSymbols.Count - 1);
            _store[original.Name] = symbol;
            return symbol;
        }

        internal Symbol DefineFunctionName(string name)
        {
            var symbol = new Symbol(name, SymbolScope.Function, 0);
            _store[name] = symbol;
            return symbol;
        }
    }
}
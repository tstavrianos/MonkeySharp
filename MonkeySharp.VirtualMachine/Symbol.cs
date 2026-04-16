using System;

namespace MonkeySharp.VirtualMachine;

public readonly struct Symbol : IEquatable<Symbol>
{
    public readonly string Name;
    public readonly SymbolScope Scope;
    public readonly int Index;

    internal Symbol(string name, SymbolScope scope, int index)
    {
        Name = name;
        Scope = scope;
        Index = index;
    }

    public bool Equals(Symbol other)
    {
        return Name == other.Name && Scope == other.Scope && Index == other.Index;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        return obj is Symbol other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, (int)Scope, Index);
    }

    public static bool operator ==(Symbol left, Symbol right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Symbol left, Symbol right)
    {
        return !left.Equals(right);
    }
}

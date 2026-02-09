using System;
using System.Collections.Generic;

namespace MonkeySharp.Compiler;

/// <summary>
/// Base class for all runtime values in the MonkeySharp language.
/// </summary>
public abstract class MonkeyObject
{
    /// <summary>
    /// Returns a string representation of this object.
    /// </summary>
    public abstract string Inspect();

    /// <summary>
    /// Gets the type name of this object.
    /// </summary>
    public abstract string TypeName();
}

/// <summary>
/// Represents a 64-bit integer value.
/// </summary>
public sealed class MonkeyInteger : MonkeyObject
{
    public long Value { get; }

    public MonkeyInteger(long value)
    {
        Value = value;
    }

    public override string Inspect()
    {
        return Value.ToString();
    }

    public override string TypeName()
    {
        return "INTEGER";
    }

    public override bool Equals(object obj)
    {
        return obj is MonkeyInteger other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}

/// <summary>
/// Represents a string value.
/// </summary>
public sealed class MonkeyString : MonkeyObject
{
    public string Value { get; }

    public MonkeyString(string value)
    {
        Value = value ?? string.Empty;
    }

    public override string Inspect()
    {
        return Value;
    }

    public override string TypeName()
    {
        return "STRING";
    }

    public override bool Equals(object obj)
    {
        return obj is MonkeyString other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}

/// <summary>
/// Represents a boolean value.
/// </summary>
public sealed class MonkeyBoolean : MonkeyObject
{
    public bool Value { get; }

    private MonkeyBoolean(bool value)
    {
        Value = value;
    }

    public static readonly MonkeyBoolean True = new(true);
    public static readonly MonkeyBoolean False = new(false);

    public static MonkeyBoolean From(bool value)
    {
        return value ? True : False;
    }

    public override string Inspect()
    {
        return Value.ToString().ToLower();
    }

    public override string TypeName()
    {
        return "BOOLEAN";
    }

    public override bool Equals(object obj)
    {
        return obj is MonkeyBoolean other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}

/// <summary>
/// Represents a null value.
/// </summary>
public sealed class MonkeyNull : MonkeyObject
{
    private MonkeyNull()
    {
    }

    public static readonly MonkeyNull Instance = new();

    public override string Inspect()
    {
        return "null";
    }

    public override string TypeName()
    {
        return "NULL";
    }
}

/// <summary>
/// Represents an array of MonkeySharp objects.
/// </summary>
public sealed class MonkeyArray : MonkeyObject
{
    public MonkeyObject[] Elements { get; }

    public MonkeyArray(MonkeyObject[] elements)
    {
        Elements = elements ?? [];
    }

    public override string Inspect()
    {
        var elements = string.Join(", ", Array.ConvertAll(Elements, e => e.Inspect()));
        return $"[{elements}]";
    }

    public override string TypeName()
    {
        return "ARRAY";
    }
}

/// <summary>
/// Represents a hash table (dictionary) of MonkeySharp objects.
/// </summary>
public sealed class MonkeyHash : MonkeyObject
{
    public Dictionary<MonkeyObject, MonkeyObject> Pairs { get; }

    public MonkeyHash(Dictionary<MonkeyObject, MonkeyObject> pairs)
    {
        Pairs = pairs ?? [];
    }

    public override string Inspect()
    {
        var pairs = new List<string>();
        foreach (var kvp in Pairs) pairs.Add($"{kvp.Key.Inspect()}: {kvp.Value.Inspect()}");
        return $"{{{string.Join(", ", pairs)}}}";
    }

    public override string TypeName()
    {
        return "HASH";
    }
}

/// <summary>
/// Represents a compiled function.
/// </summary>
public sealed class MonkeyFunction : MonkeyObject
{
    public Delegate CompiledFunction { get; }
    public string Name { get; }
    public int ParameterCount { get; }

    public MonkeyFunction(Delegate compiledFunction, string name, int parameterCount)
    {
        CompiledFunction = compiledFunction ?? throw new ArgumentNullException(nameof(compiledFunction));
        Name = name ?? "<anonymous>";
        ParameterCount = parameterCount;
    }

    public override string Inspect()
    {
        return $"<function:{Name}>";
    }

    public override string TypeName()
    {
        return "FUNCTION";
    }
}
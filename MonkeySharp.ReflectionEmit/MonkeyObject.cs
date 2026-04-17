using System;
using System.Collections.Generic;

namespace MonkeySharp.ReflectionEmit;

/// <summary>
/// Base class for all runtime values in the MonkeySharp language.
/// </summary>
internal abstract class MonkeyObject
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

internal interface IHashable
{
    /// <summary>
    /// Gets the hash code for this object, used as a key in a hash table.
    /// </summary>
    int GetHashCode();

    bool Equals(object obj);
}

/// <summary>
/// Represents a 64-bit integer value.
/// </summary>
internal sealed class MonkeyInteger : MonkeyObject, IHashable
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

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
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
internal sealed class MonkeyString : MonkeyObject, IHashable
{
    public string Value { get; }

    public MonkeyString(string? value)
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

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
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
internal sealed class MonkeyBoolean : MonkeyObject, IHashable
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

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
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
internal sealed class MonkeyNull : MonkeyObject
{
    private MonkeyNull() { }

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
internal sealed class MonkeyArray : MonkeyObject
{
    public MonkeyObject[] Elements { get; }

    public MonkeyArray(MonkeyObject[]? elements)
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
internal sealed class MonkeyHash : MonkeyObject
{
    public Dictionary<MonkeyObject, MonkeyObject> Pairs { get; }

    public MonkeyHash(Dictionary<MonkeyObject, MonkeyObject>? pairs)
    {
        Pairs = pairs ?? [];
    }

    public override string Inspect()
    {
        var pairs = new List<string>();
        foreach (var kvp in Pairs)
            pairs.Add($"{kvp.Key.Inspect()}: {kvp.Value.Inspect()}");
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
internal sealed class MonkeyFunction : MonkeyObject
{
    public Delegate CompiledFunction { get; }
    public string Name { get; }
    public int ParameterCount { get; }

    public MonkeyFunction(Delegate? compiledFunction, string? name, int parameterCount)
    {
        CompiledFunction =
            compiledFunction ?? throw new ArgumentNullException(nameof(compiledFunction));
        Name = name ?? "<anonymous>";
        ParameterCount = parameterCount;
    }

    /// <summary>
    /// Validates the number of arguments matches the expected parameter count.
    /// Returns null if valid, or a MonkeyError if invalid.
    /// </summary>
    public MonkeyError? ValidateArgumentCount(int argumentCount)
    {
        if (argumentCount != ParameterCount)
            return new MonkeyError(
                $"wrong number of arguments. want={ParameterCount}, got={argumentCount}"
            );
        return null;
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

internal sealed class MonkeyError : MonkeyObject
{
    public string Message { get; }

    public MonkeyError(string? message)
    {
        Message = message ?? string.Empty;
    }

    public override string Inspect()
    {
        return $"ERROR: {Message}";
    }

    public override string TypeName()
    {
        return "ERROR";
    }
}

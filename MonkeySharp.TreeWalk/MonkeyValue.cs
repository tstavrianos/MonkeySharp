using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

/// <summary>
/// Represents the public runtime kind of a <see cref="MonkeyValue"/>.
/// </summary>
public enum MonkeyValueKind
{
    /// <summary>
    /// Integer value.
    /// </summary>
    Integer,

    /// <summary>
    /// String value.
    /// </summary>
    String,

    /// <summary>
    /// Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Null value.
    /// </summary>
    Null,

    /// <summary>
    /// Array value.
    /// </summary>
    Array,

    /// <summary>
    /// Hash value.
    /// </summary>
    Hash,

    /// <summary>
    /// Error value.
    /// </summary>
    Error,
}

/// <summary>
/// Represents a public, engine-agnostic Monkey runtime value.
/// </summary>
public readonly struct MonkeyValue : IEquatable<MonkeyValue>
{
    private readonly bool _hasValue;

    // default(MonkeyValue) should behave as NULL at the public boundary.
    private Value RuntimeValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasValue ? field : Value.NullValue;
    }

    /// <summary>
    /// Gets the value kind.
    /// </summary>
    public MonkeyValueKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            RuntimeValue.IsInteger ? MonkeyValueKind.Integer
            : RuntimeValue.IsString ? MonkeyValueKind.String
            : RuntimeValue.IsBoolean ? MonkeyValueKind.Boolean
            : RuntimeValue.IsNull ? MonkeyValueKind.Null
            : RuntimeValue.IsArray ? MonkeyValueKind.Array
            : RuntimeValue.IsHash ? MonkeyValueKind.Hash
            : MonkeyValueKind.Error;
    }

    /// <summary>
    /// Gets the integer value when <see cref="Kind"/> is <see cref="MonkeyValueKind.Integer"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public long? IntegerValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsInteger ? RuntimeValue.IntValue : null;
    }

    /// <summary>
    /// Gets the string value when <see cref="Kind"/> is <see cref="MonkeyValueKind.String"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public string? StringValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsString ? RuntimeValue.StringValue : null;
    }

    /// <summary>
    /// Gets the boolean value when <see cref="Kind"/> is <see cref="MonkeyValueKind.Boolean"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public bool? BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsBoolean ? RuntimeValue.BooleanValue : null;
    }

    /// <summary>
    /// Gets the error message when <see cref="Kind"/> is <see cref="MonkeyValueKind.Error"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public string? ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsError ? RuntimeValue.ErrorMessage : null;
    }

    /// <summary>
    /// Gets array elements when <see cref="Kind"/> is <see cref="MonkeyValueKind.Array"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<MonkeyValue>? ArrayElements
    {
        get
        {
            if (!RuntimeValue.IsArray)
                return null;

            var elements = RuntimeValue.ArrayElements!;
            var ret = new MonkeyValue[elements.Count];
            for (var i = 0; i < elements.Count; i++)
                ret[i] = new MonkeyValue(elements[i]);

            return ret;
        }
    }

    /// <summary>
    /// Gets hash pairs when <see cref="Kind"/> is <see cref="MonkeyValueKind.Hash"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<MonkeyValue, MonkeyValue>? HashPairs
    {
        get
        {
            if (!RuntimeValue.IsHash)
                return null;

            var ret = new Dictionary<MonkeyValue, MonkeyValue>();
            foreach (var (_, pair) in RuntimeValue.HashPairs!)
                ret[new MonkeyValue(pair.Key)] = new MonkeyValue(pair.Value);

            return ret;
        }
    }

    /// <summary>
    /// Gets the Monkey string representation of the value.
    /// </summary>
    public string Inspect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.Inspect;
    }

    /// <summary>
    /// Creates an integer Monkey value.
    /// </summary>
    /// <param name="value">The integer payload.</param>
    /// <returns>A Monkey integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Integer(long value)
    {
        return new MonkeyValue(Value.Integer(value));
    }

    /// <summary>
    /// Creates a string Monkey value.
    /// </summary>
    /// <param name="value">The string payload.</param>
    /// <returns>A Monkey string value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue String(string value)
    {
        return new MonkeyValue(Value.String(value));
    }

    /// <summary>
    /// Creates a boolean Monkey value.
    /// </summary>
    /// <param name="value">The boolean payload.</param>
    /// <returns>A Monkey boolean value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Boolean(bool value)
    {
        return new MonkeyValue(value ? Value.True : Value.False);
    }

    /// <summary>
    /// Creates a null Monkey value.
    /// </summary>
    /// <returns>A Monkey null value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Null()
    {
        return new MonkeyValue(Value.NullValue);
    }

    /// <summary>
    /// Creates an error Monkey value.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A Monkey error value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Error(string message)
    {
        return new MonkeyValue(Value.Error(message));
    }

    /// <summary>
    /// Creates an array Monkey value.
    /// </summary>
    /// <param name="elements">The array elements.</param>
    /// <returns>A Monkey array value.</returns>
    public static MonkeyValue Array(IReadOnlyList<MonkeyValue> elements)
    {
        var ret = new Value[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            ret[i] = elements[i].ToInternal();

        return new MonkeyValue(Value.Array(ret));
    }

    /// <summary>
    /// Creates a hash Monkey value.
    /// </summary>
    /// <param name="pairs">The key-value pairs.</param>
    /// <returns>A Monkey hash value, or an error value when a key is not hashable.</returns>
    public static MonkeyValue Hash(IReadOnlyDictionary<MonkeyValue, MonkeyValue> pairs)
    {
        var ret = new Dictionary<HashKey, (Value Key, Value Value)>();
        foreach (var (key, value) in pairs)
        {
            var runtimeKey = key.ToInternal();
            if (!runtimeKey.IsHashable)
                return Error($"unusable as hash key in host value: {runtimeKey.Type}");

            ret[runtimeKey.GetHashKey()] = (runtimeKey, value.ToInternal());
        }

        return new MonkeyValue(Value.Hash(ret));
    }

    internal MonkeyValue(Value value)
    {
        RuntimeValue = value;
        _hasValue = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Value ToInternal()
    {
        return RuntimeValue;
    }

    /// <summary>
    /// Indicates whether this value equals another value.
    /// </summary>
    /// <param name="other">The value to compare with the current value.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MonkeyValue other)
    {
        return RuntimeValue.Equals(other.RuntimeValue);
    }

    /// <summary>
    /// Indicates whether this value equals a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current value.</param>
    /// <returns><see langword="true"/> if the object is an equal <see cref="MonkeyValue"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return obj is MonkeyValue other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this value.
    /// </summary>
    /// <returns>A hash code for the current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return RuntimeValue.GetHashCode();
    }
}

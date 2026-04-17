using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonkeySharp.ReflectionEmit;

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
    private readonly MonkeyObject _value;

    // default(MonkeyValue) has a null backing field; treat it as NULL for parity/safety.
    private MonkeyObject RuntimeValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value ?? MonkeyNull.Instance;
    }

    /// <summary>
    /// Gets the value kind.
    /// </summary>
    public MonkeyValueKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            RuntimeValue is MonkeyInteger ? MonkeyValueKind.Integer
            : RuntimeValue is MonkeyString ? MonkeyValueKind.String
            : RuntimeValue is MonkeyBoolean ? MonkeyValueKind.Boolean
            : RuntimeValue is MonkeyNull ? MonkeyValueKind.Null
            : RuntimeValue is MonkeyArray ? MonkeyValueKind.Array
            : RuntimeValue is MonkeyHash ? MonkeyValueKind.Hash
            : MonkeyValueKind.Error;
    }

    /// <summary>
    /// Gets the integer value when <see cref="Kind"/> is <see cref="MonkeyValueKind.Integer"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public long? IntegerValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyInteger i ? i.Value : null;
    }

    /// <summary>
    /// Gets the string value when <see cref="Kind"/> is <see cref="MonkeyValueKind.String"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public string? StringValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyString s ? s.Value : null;
    }

    /// <summary>
    /// Gets the boolean value when <see cref="Kind"/> is <see cref="MonkeyValueKind.Boolean"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public bool? BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyBoolean b ? b.Value : null;
    }

    /// <summary>
    /// Gets the error message when <see cref="Kind"/> is <see cref="MonkeyValueKind.Error"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public string? ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyError e ? e.Message : null;
    }

    /// <summary>
    /// Gets array elements when <see cref="Kind"/> is <see cref="MonkeyValueKind.Array"/>; otherwise, <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<MonkeyValue>? ArrayElements
    {
        get
        {
            if (RuntimeValue is not MonkeyArray a)
                return null;

            var elements = a.Elements;
            var ret = new MonkeyValue[elements.Length];
            for (var i = 0; i < elements.Length; i++)
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
            if (RuntimeValue is not MonkeyHash h)
                return null;

            var ret = new Dictionary<MonkeyValue, MonkeyValue>();
            foreach (var (key, value) in h.Pairs)
                ret[new MonkeyValue(key)] = new MonkeyValue(value);

            return ret;
        }
    }

    /// <summary>
    /// Gets the Monkey string representation of the value.
    /// </summary>
    public string Inspect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.Inspect();
    }

    /// <summary>
    /// Creates an integer Monkey value.
    /// </summary>
    /// <param name="value">The integer payload.</param>
    /// <returns>A Monkey integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Integer(long value)
    {
        return new MonkeyValue(new MonkeyInteger(value));
    }

    /// <summary>
    /// Creates a string Monkey value.
    /// </summary>
    /// <param name="value">The string payload.</param>
    /// <returns>A Monkey string value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue String(string value)
    {
        return new MonkeyValue(new MonkeyString(value));
    }

    /// <summary>
    /// Creates a boolean Monkey value.
    /// </summary>
    /// <param name="value">The boolean payload.</param>
    /// <returns>A Monkey boolean value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Boolean(bool value)
    {
        return new MonkeyValue(value ? MonkeyBoolean.True : MonkeyBoolean.False);
    }

    /// <summary>
    /// Creates a null Monkey value.
    /// </summary>
    /// <returns>A Monkey null value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Null()
    {
        return new MonkeyValue(MonkeyNull.Instance);
    }

    /// <summary>
    /// Creates an error Monkey value.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A Monkey error value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Error(string message)
    {
        return new MonkeyValue(new MonkeyError(message));
    }

    /// <summary>
    /// Creates an array Monkey value.
    /// </summary>
    /// <param name="elements">The array elements.</param>
    /// <returns>A Monkey array value.</returns>
    public static MonkeyValue Array(IReadOnlyList<MonkeyValue> elements)
    {
        var ret = new MonkeyObject[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            ret[i] = elements[i].ToInternal();

        return new MonkeyValue(new MonkeyArray(ret));
    }

    /// <summary>
    /// Creates a hash Monkey value.
    /// </summary>
    /// <param name="pairs">The key-value pairs.</param>
    /// <returns>A Monkey hash value, or an error value when a key is not hashable.</returns>
    public static MonkeyValue Hash(IReadOnlyDictionary<MonkeyValue, MonkeyValue> pairs)
    {
        var ret = new Dictionary<MonkeyObject, MonkeyObject>();
        foreach (var (key, value) in pairs)
        {
            var runtimeKey = key.ToInternal();
            if (runtimeKey is not IHashable)
                return Error($"unusable as hash key in host value: {runtimeKey.TypeName()}");

            ret[runtimeKey] = value.ToInternal();
        }

        return new MonkeyValue(new MonkeyHash(ret));
    }

    internal MonkeyValue(MonkeyObject value)
    {
        _value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MonkeyObject ToInternal()
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

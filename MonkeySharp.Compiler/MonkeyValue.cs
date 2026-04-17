using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Compiler;

public enum MonkeyValueKind
{
    Integer,
    String,
    Boolean,
    Null,
    Array,
    Hash,
    Error,
}

public readonly struct MonkeyValue : IEquatable<MonkeyValue>
{
    private readonly MonkeyObject _value;

    // default(MonkeyValue) has a null backing field; treat it as NULL for parity/safety.
    private MonkeyObject RuntimeValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value ?? MonkeyNull.Instance;
    }

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

    public long? IntegerValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyInteger i ? i.Value : null;
    }

    public string? StringValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyString s ? s.Value : null;
    }

    public bool? BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyBoolean b ? b.Value : null;
    }

    public string? ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue is MonkeyError e ? e.Message : null;
    }

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

    public string Inspect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.Inspect();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Integer(long value)
    {
        return new MonkeyValue(new MonkeyInteger(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue String(string value)
    {
        return new MonkeyValue(new MonkeyString(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Boolean(bool value)
    {
        return new MonkeyValue(value ? MonkeyBoolean.True : MonkeyBoolean.False);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Null()
    {
        return new MonkeyValue(MonkeyNull.Instance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Error(string message)
    {
        return new MonkeyValue(new MonkeyError(message));
    }

    public static MonkeyValue Array(IReadOnlyList<MonkeyValue> elements)
    {
        var ret = new MonkeyObject[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            ret[i] = elements[i].ToInternal();

        return new MonkeyValue(new MonkeyArray(ret));
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MonkeyValue other)
    {
        return RuntimeValue.Equals(other.RuntimeValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return obj is MonkeyValue other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return RuntimeValue.GetHashCode();
    }
}

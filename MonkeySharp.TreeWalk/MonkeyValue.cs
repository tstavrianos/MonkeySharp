using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

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
    private readonly Value _value;
    private readonly bool _hasValue;

    // default(MonkeyValue) should behave as NULL at the public boundary.
    private Value RuntimeValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasValue ? _value : Value.NullValue;
    }

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

    public long? IntegerValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsInteger ? RuntimeValue.IntValue : null;
    }

    public string? StringValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsString ? RuntimeValue.StringValue : null;
    }

    public bool? BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsBoolean ? RuntimeValue.BooleanValue : null;
    }

    public string? ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.IsError ? RuntimeValue.ErrorMessage : null;
    }

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

    public string Inspect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeValue.Inspect;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Integer(long value)
    {
        return new MonkeyValue(Value.Integer(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue String(string value)
    {
        return new MonkeyValue(Value.String(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Boolean(bool value)
    {
        return new MonkeyValue(value ? Value.True : Value.False);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Null()
    {
        return new MonkeyValue(Value.NullValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MonkeyValue Error(string message)
    {
        return new MonkeyValue(Value.Error(message));
    }

    public static MonkeyValue Array(IReadOnlyList<MonkeyValue> elements)
    {
        var ret = new Value[elements.Count];
        for (var i = 0; i < elements.Count; i++)
            ret[i] = elements[i].ToInternal();

        return new MonkeyValue(Value.Array(ret));
    }

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
        _value = value;
        _hasValue = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Value ToInternal()
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

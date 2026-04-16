using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeySharp.Interpreter.Objects;

namespace MonkeySharp.Interpreter;

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

    public MonkeyValueKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            _value.IsInteger ? MonkeyValueKind.Integer
            : _value.IsString ? MonkeyValueKind.String
            : _value.IsBoolean ? MonkeyValueKind.Boolean
            : _value.IsNull ? MonkeyValueKind.Null
            : _value.IsArray ? MonkeyValueKind.Array
            : _value.IsHash ? MonkeyValueKind.Hash
            : MonkeyValueKind.Error;
    }

    public long? IntegerValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsInteger ? _value.IntValue : null;
    }

    public string? StringValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsString ? _value.StringValue : null;
    }

    public bool? BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsBoolean ? _value.BooleanValue : null;
    }

    public string? ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.IsError ? _value.ErrorMessage : null;
    }

    public IReadOnlyList<MonkeyValue>? ArrayElements
    {
        get
        {
            if (!_value.IsArray)
                return null;

            var elements = _value.ArrayElements!;
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
            if (!_value.IsHash)
                return null;

            var ret = new Dictionary<MonkeyValue, MonkeyValue>();
            foreach (var (_, pair) in _value.HashPairs!)
                ret[new MonkeyValue(pair.Key)] = new MonkeyValue(pair.Value);

            return ret;
        }
    }

    public string Inspect
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value.Inspect;
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
            ret[i] = elements[i]._value;

        return new MonkeyValue(Value.Array(ret));
    }

    public static MonkeyValue Hash(IReadOnlyDictionary<MonkeyValue, MonkeyValue> pairs)
    {
        var ret = new Dictionary<HashKey, (Value Key, Value Value)>();
        foreach (var (key, value) in pairs)
        {
            var runtimeKey = key._value;
            if (!runtimeKey.IsHashable)
                return Error($"unusable as hash key in host value: {runtimeKey.Type}");

            ret[runtimeKey.GetHashKey()] = (runtimeKey, value._value);
        }

        return new MonkeyValue(Value.Hash(ret));
    }

    internal MonkeyValue(Value value)
    {
        _value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Value ToInternal()
    {
        return _value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MonkeyValue other)
    {
        return _value.Equals(other._value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return obj is MonkeyValue other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}

using MonkeySharp.Core.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.VirtualMachine;

/// <summary>
/// Runtime support library for IL-compiled Monkey code
/// </summary>
public static class ILRuntime
{
    public static IObject CreateArray(params IObject[] elements)
    {
        return new ArrayObject(elements.ToList());
    }

    public static IObject CreateHash(params IObject[] keyValuePairs)
    {
        var pairs = new Dictionary<HashKey, (IHashableObject, IObject)>();
        for (var i = 0; i < keyValuePairs.Length; i += 2)
        {
            if (keyValuePairs[i] is not IHashableObject key)
                throw new InvalidOperationException($"unusable as hash key: {keyValuePairs[i]?.GetType().Name}");

            var value = keyValuePairs[i + 1];
            pairs[key.HashKey()] = (key, value);
        }

        return new HashObject(pairs);
    }

    public static IObject CreateFunction(Delegate compiledDelegate, int paramCount, string name)
    {
        return new ILCompiledFunction(compiledDelegate, paramCount, name);
    }

    public static IObject CreateClosure(Delegate compiledDelegate, int paramCount, IObject[] closureValues, string name)
    {
        return new ILCompiledClosure(compiledDelegate, paramCount, closureValues, name);
    }

    public static IObject IndexOperation(IObject left, IObject index)
    {
        return (left, index) switch
        {
            (ArrayObject array, IntegerObject idx) =>
                idx.Value >= 0 && idx.Value < array.Elements.Count
                    ? array.Elements[(int) idx.Value]
                    : NullObject.Null,

            (HashObject hash, IHashableObject hashable) =>
                hash.Pairs.TryGetValue(hashable.HashKey(), out var pair)
                    ? pair.Item2
                    : NullObject.Null,

            _ => throw new InvalidOperationException($"index operator not supported: {left?.GetType().Name}")
        };
    }

    public static IObject CallBuiltin(int builtinIndex, params IObject[] args)
    {
        var builtin = Builtins.ByIndex(builtinIndex);
        return builtin.Function(args.ToList());
    }

    public static IObject CallFunction(IObject function, IObject[] args)
    {
        if (function is ILCompiledFunction compiledFn) return compiledFn.Invoke(args);
        if (function is ILCompiledClosure compiledClosure) return compiledClosure.Invoke(args);

        throw new InvalidOperationException($"not a function: {function?.GetType().Name}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTruthy(IObject obj)
    {
        return obj switch
        {
            NullObject => false,
            BooleanObject b => b.Value,
            _ => true
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Add(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => IntegerObject.Create(l.Value + r.Value),
            (StringObject l, StringObject r) => StringObject.Create(l.Value + r.Value),
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} + {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Subtract(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => IntegerObject.Create(l.Value - r.Value),
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} - {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Multiply(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => IntegerObject.Create(l.Value * r.Value),
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} * {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Divide(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => IntegerObject.Create(l.Value / r.Value),
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} / {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject GreaterThan(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => l.Value > r.Value ? BooleanObject.True : BooleanObject.False,
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} > {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject LessThan(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => l.Value < r.Value ? BooleanObject.True : BooleanObject.False,
            _ => throw new InvalidOperationException($"type mismatch: {left?.GetType().Name} < {right?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Equal(IObject left, IObject right)
    {
        return (left, right) switch
        {
            (IntegerObject l, IntegerObject r) => l.Value == r.Value ? BooleanObject.True : BooleanObject.False,
            (BooleanObject l, BooleanObject r) => l.Value == r.Value ? BooleanObject.True : BooleanObject.False,
            (StringObject l, StringObject r) => l.Value == r.Value ? BooleanObject.True : BooleanObject.False,
            (NullObject, NullObject) => BooleanObject.True,
            _ => BooleanObject.False
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject NotEqual(IObject left, IObject right)
    {
        var result = Equal(left, right);
        return result is BooleanObject b && b == BooleanObject.True ? BooleanObject.False : BooleanObject.True;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Negate(IObject obj)
    {
        return obj switch
        {
            IntegerObject i => IntegerObject.Create(-i.Value),
            _ => throw new InvalidOperationException($"unknown operator: -{obj?.GetType().Name}")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObject Bang(IObject obj)
    {
        return IsTruthy(obj) ? BooleanObject.False : BooleanObject.True;
    }
}
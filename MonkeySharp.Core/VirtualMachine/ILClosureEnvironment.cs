using MonkeySharp.Core.Objects;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.VirtualMachine;

/// <summary>
/// Represents a compiled function (Expression Trees handle closures automatically)
/// </summary>
public class ILCompiledFunction : IObject
{
    private readonly Delegate _delegate;
    public int ParameterCount { get; }
    public string Name { get; }

    public ILCompiledFunction(Delegate del, int parameterCount, string name = null)
    {
        _delegate = del;
        ParameterCount = parameterCount;
        Name = name ?? "<lambda>";
    }

    public string Type => "COMPILED_FUNCTION_IL";
    public string Inspect => $"CompiledFunction[{Name}]";

    // Zero-allocation overloads for common cases
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke()
    {
        if (ParameterCount != 0)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=0");
        return ((Func<IObject>) _delegate)();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0)
    {
        if (ParameterCount != 1)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=1");
        return ((Func<IObject, IObject>) _delegate)(arg0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0, IObject arg1)
    {
        if (ParameterCount != 2)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=2");
        return ((Func<IObject, IObject, IObject>) _delegate)(arg0, arg1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0, IObject arg1, IObject arg2)
    {
        if (ParameterCount != 3)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=3");
        return ((Func<IObject, IObject, IObject, IObject>) _delegate)(arg0, arg1, arg2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0, IObject arg1, IObject arg2, IObject arg3)
    {
        if (ParameterCount != 4)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=4");
        return ((Func<IObject, IObject, IObject, IObject, IObject>) _delegate)(arg0, arg1, arg2, arg3);
    }

    // Fallback for > 4 parameters
    public IObject Invoke(params IObject[] args)
    {
        if (args.Length != ParameterCount)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got={args.Length}");

        return args.Length switch
        {
            0 => ((Func<IObject>) _delegate)(),
            1 => ((Func<IObject, IObject>) _delegate)(args[0]),
            2 => ((Func<IObject, IObject, IObject>) _delegate)(args[0], args[1]),
            3 => ((Func<IObject, IObject, IObject, IObject>) _delegate)(args[0], args[1], args[2]),
            4 => ((Func<IObject, IObject, IObject, IObject, IObject>) _delegate)(args[0], args[1], args[2], args[3]),
            5 => ((Func<IObject, IObject, IObject, IObject, IObject, IObject>) _delegate)(args[0], args[1], args[2],
                args[3], args[4]),
            _ => (IObject) _delegate.DynamicInvoke(args)
        };
    }
}

public class ILCompiledClosure : IObject
{
    private readonly Delegate _delegate;
    private readonly IObject[] _closureValues;
    public int ParameterCount { get; }
    public string Name { get; }

    public ILCompiledClosure(Delegate del, int parameterCount, IObject[] closureValues, string name = null)
    {
        _delegate = del;
        _closureValues = closureValues;
        ParameterCount = parameterCount;
        Name = name ?? "<closure>";
    }

    public string Type => "COMPILED_CLOSURE_IL";
    public string Inspect => $"CompiledClosure[{Name}]";

    // Optimized overloads for 1 closure variable (most common case for fibonacci)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0)
    {
        if (ParameterCount != 1)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=1");

        return _closureValues.Length switch
        {
            1 => ((Func<IObject, IObject, IObject>) _delegate)(_closureValues[0], arg0),
            _ => InvokeGeneric(arg0)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObject Invoke(IObject arg0, IObject arg1)
    {
        if (ParameterCount != 2)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got=2");

        return _closureValues.Length switch
        {
            1 => ((Func<IObject, IObject, IObject, IObject>) _delegate)(_closureValues[0], arg0, arg1),
            2 => ((Func<IObject, IObject, IObject, IObject, IObject>) _delegate)(_closureValues[0], _closureValues[1],
                arg0, arg1),
            _ => InvokeGeneric(arg0, arg1)
        };
    }

    // Fallback
    public IObject Invoke(params IObject[] args)
    {
        if (args.Length != ParameterCount)
            throw new InvalidOperationException($"wrong number of arguments. want={ParameterCount}, got={args.Length}");

        return InvokeGeneric(args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObject InvokeGeneric(params IObject[] args)
    {
        var totalArgs = _closureValues.Length + args.Length;

        // Optimize for common closure patterns
        if (_closureValues.Length == 1 && args.Length == 1)
            return ((Func<IObject, IObject, IObject>) _delegate)(_closureValues[0], args[0]);

        // Generic path
        var allArgs = new IObject[totalArgs];
        Array.Copy(_closureValues, 0, allArgs, 0, _closureValues.Length);
        Array.Copy(args, 0, allArgs, _closureValues.Length, args.Length);

        return totalArgs switch
        {
            1 => ((Func<IObject, IObject>) _delegate)(allArgs[0]),
            2 => ((Func<IObject, IObject, IObject>) _delegate)(allArgs[0], allArgs[1]),
            3 => ((Func<IObject, IObject, IObject, IObject>) _delegate)(allArgs[0], allArgs[1], allArgs[2]),
            4 => ((Func<IObject, IObject, IObject, IObject, IObject>) _delegate)(allArgs[0], allArgs[1], allArgs[2],
                allArgs[3]),
            _ => (IObject) _delegate.DynamicInvoke(allArgs)
        };
    }
}
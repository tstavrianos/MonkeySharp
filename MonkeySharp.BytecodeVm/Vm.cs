using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeySharp.BytecodeVm.Objects;

namespace MonkeySharp.BytecodeVm;

internal sealed class Vm
{
    private const int StackSize = 2048;

    public const int GlobalsSize = 65536;
    private const int MaxFrames = 1024;

    private readonly Value[] _constants;
    private readonly Value[] _stack = new Value[StackSize];
    private readonly Value[] _globals = new Value[GlobalsSize];
    private readonly Value[] _builtins;
    private int _sp;

    private readonly Frame[] _frames = new Frame[MaxFrames];
    private int _frameIndex;

    internal Vm(Bytecode bytecode)
        : this(bytecode, new Value[GlobalsSize], GetDefaultBuiltins()) { }

    internal Vm(Bytecode bytecode, Value[] globals, Value[] builtins)
    {
        var function = Value.CompiledFunction(bytecode.Instructions, 0, 0);
        var closure = Value.Closure(function, []);
        PushFrame(new Frame(closure, 0));
        _constants = bytecode.Constants;
        Array.Copy(globals, _globals, Math.Min(globals.Length, GlobalsSize));
        _builtins = builtins;
        _sp = 0;
    }

    private static Value[] GetDefaultBuiltins()
    {
        var builtins = new Value[Builtins.Entries.Count];
        for (var i = 0; i < Builtins.Entries.Count; i++)
            builtins[i] = Builtins.Entries[i].builtin;

        return builtins;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Frame CurrentFrame()
    {
        return _frames[_frameIndex - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFrame(Frame frame)
    {
        _frames[_frameIndex++] = frame;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Frame PopFrame()
    {
        var frame = _frames[--_frameIndex];
        //_frames[_frameIndex] = null; // Clear reference
        return frame;
    }

    internal Value LastPoppedStackElement { get; private set; }

    internal string? Run()
    {
        var currentFrame = CurrentFrame();
        var ins = currentFrame.Instructions().AsSpan();
        while (currentFrame.Ip < ins.Length - 1)
        {
            currentFrame.Ip++;
            var ip = currentFrame.Ip;
            var op = (OpCode)ins[ip];
            switch (op)
            {
                case OpCode.Constant:
                {
                    var constIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;

                    var err = Push(_constants[constIndex]);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Add:
                case OpCode.Subtract:
                case OpCode.Multiply:
                case OpCode.Divide:
                case OpCode.Equal:
                case OpCode.NotEqual:
                case OpCode.GreaterThan:
                {
                    var err = ExecuteBinaryOperation(op);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Pop:
                    Pop();
                    break;
                case OpCode.True:
                {
                    var err = Push(Value.True);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.False:
                {
                    var err = Push(Value.False);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Bang:
                case OpCode.Minus:
                {
                    var err = ExecutePrefixOperation(op);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Jump:
                {
                    var pos = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip = pos - 1;
                    break;
                }
                case OpCode.JumpNotTruthy:
                {
                    var pos = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var condition = Pop();
                    if (!condition.IsTruthy())
                        currentFrame.Ip = pos - 1;
                    break;
                }
                case OpCode.Null:
                {
                    var err = Push(Value.NullValue);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.SetGlobal:
                {
                    var globalIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    _globals[globalIndex] = Pop();
                    break;
                }
                case OpCode.GetGlobal:
                {
                    var globalIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var err = Push(_globals[globalIndex]);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Array:
                {
                    var numElements = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var array = BuildArray(_sp - numElements, _sp);
                    _sp = _sp - numElements;
                    var err = Push(array);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Hash:
                {
                    var numElements = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var (hash, err) = BuildHash(_sp - numElements, _sp);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    _sp = _sp - numElements;
                    err = Push(hash);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Index:
                {
                    var index = Pop();
                    var left = Pop();
                    var err = ExecuteIndexExpression(left, index);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Call:
                {
                    var numArgs = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var err = ExecuteCall(numArgs);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    currentFrame = CurrentFrame();
                    ins = currentFrame.Instructions().AsSpan();
                    break;
                }
                case OpCode.ReturnValue:
                {
                    var returnValue = Pop();
                    var frame = PopFrame();
                    currentFrame = CurrentFrame();
                    ins = currentFrame.Instructions().AsSpan();
                    _sp = frame.BasePointer - 1;
                    var err = Push(returnValue);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Return:
                {
                    var frame = PopFrame();
                    currentFrame = CurrentFrame();
                    ins = currentFrame.Instructions().AsSpan();
                    _sp = frame.BasePointer - 1;
                    var err = Push(Value.NullValue);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.SetLocal:
                {
                    var localIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var frame = currentFrame;
                    _stack[frame.BasePointer + localIndex] = Pop();
                    break;
                }
                case OpCode.GetLocal:
                {
                    var localIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var frame = currentFrame;
                    var err = Push(_stack[frame.BasePointer + localIndex]);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.GetBuiltin:
                {
                    var builtinIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var definition = _builtins[builtinIndex];
                    var err = Push(definition);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.Closure:
                {
                    var constantIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    var numFree = ins[ip + 3];
                    currentFrame.Ip += 3;
                    var err = PushClosure(constantIndex, numFree);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.GetFree:
                {
                    var freeIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var currentClosure = currentFrame.Closure;
                    var err = Push(currentClosure.ClosureData!.Free[freeIndex]);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.CurrentClosure:
                {
                    var currentClosure = currentFrame.Closure;
                    var err = Push(currentClosure);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    break;
                }
                case OpCode.TailCall:
                {
                    var numArgs = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var err = ExecuteTailCall(numArgs);
                    if (!string.IsNullOrEmpty(err))
                        return err;
                    currentFrame = CurrentFrame();
                    ins = currentFrame.Instructions().AsSpan();
                    break;
                }
            }
        }

        return null;
    }

    private string? ExecuteTailCall(int numArgs)
    {
        var callee = _stack[_sp - 1 - numArgs];

        if (callee.IsClosure)
            return TailCallClosure(callee, numArgs);
        if (callee.IsBuiltin)
            return CallBuiltin(callee, numArgs);

        return "tail calling a non-function and non-built-in";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? TailCallClosure(Value closure, int numArgs)
    {
        var closureData = closure.ClosureData!;
        var functionData = closureData.Function.CompiledFunctionData!;

        if (functionData.NumParameters != numArgs)
            return $"wrong number of arguments. want={functionData.NumParameters}, got={numArgs}";

        // Get current frame
        var currentFrame = CurrentFrame();
        var oldBasePointer = currentFrame.BasePointer;

        // Copy arguments to the current frame's position
        // Arguments are at: _sp - numArgs to _sp - 1
        // Target position: oldBasePointer to oldBasePointer + numArgs - 1
        var argStartPos = _sp - numArgs;
        for (var i = 0; i < numArgs; i++)
            _stack[oldBasePointer + i] = _stack[argStartPos + i];

        // Update the current frame instead of pushing a new one
        currentFrame.Closure = closure;
        currentFrame.Ip = -1; // Will be incremented to 0 in the main loop

        // Adjust stack pointer
        _sp = oldBasePointer + functionData.NumLocals;

        return null;
    }

    private string? PushClosure(ushort constantIndex, byte numFree)
    {
        var constant = _constants[constantIndex];
        if (!constant.IsCompiledFunction)
            return $"not a function: {constant.Type}";
        var free = new Value[numFree];
        for (var i = 0; i < numFree; i++)
            free[i] = _stack[_sp - (numFree - i)];
        _sp = _sp - numFree;
        var closure = Value.Closure(constant, free);
        return Push(closure);
    }

    private string? ExecuteCall(int numArgs)
    {
        var callee = _stack[_sp - 1 - numArgs];
        if (callee.IsClosure)
            return CallClosure(callee, numArgs);
        if (callee.IsBuiltin)
            return CallBuiltin(callee, numArgs);
        return "calling a non-function and non-built-in";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? CallClosure(Value closure, int numArgs)
    {
        var closureData = closure.ClosureData!;
        var functionData = closureData.Function.CompiledFunctionData!;
        if (functionData.NumParameters != numArgs)
            return $"wrong number of arguments. want={functionData.NumParameters}, got={numArgs}";
        var frame = new Frame(closure, _sp - numArgs);
        PushFrame(frame);
        _sp = frame.BasePointer + functionData.NumLocals;
        return null;
    }

    private string? CallBuiltin(Value builtinValue, int numArgs)
    {
        var args = _stack[(_sp - numArgs).._sp];
        var result = builtinValue.BuiltinFunction!(args);
        _sp = _sp - numArgs - 1;
        if (result.IsError)
            return result.ErrorMessage;
        Push(result.IsNull ? Value.NullValue : result);
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? ExecuteIndexExpression(Value left, Value index)
    {
        if (left.IsArray && index.IsInteger)
            return ExecuteArrayIndex(left, index);
        if (left.IsHash)
            return ExecuteHashIndex(left, index);
        return $"index operator not supported: {left.Type}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? ExecuteHashIndex(Value hashLeft, Value index)
    {
        if (!index.IsHashable)
            return $"unusable as hash key: {index.Type}";
        var hashKey = index.GetHashKey();
        if (!hashLeft.HashPairs!.TryGetValue(hashKey, out var pair))
            return Push(Value.NullValue);
        return Push(pair.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? ExecuteArrayIndex(Value arrayLeft, Value integerIndex)
    {
        var i = (int)integerIndex.IntValue;
        var elements = arrayLeft.ArrayElements!;
        var max = elements.Count - 1;
        if (i < 0 || i > max)
            return Push(Value.NullValue);
        return Push(elements[i]);
    }

    private (Value hash, string? err) BuildHash(int startIndex, int endIndex)
    {
        var hashedPairs = new Dictionary<HashKey, (Value Key, Value Value)>(endIndex - startIndex);

        for (var i = startIndex; i < endIndex; i += 2)
        {
            var key = _stack[i];
            var value = _stack[i + 1];

            if (!key.IsHashable)
                return (default, $"unusable as hash key: {key.Type}");

            hashedPairs.Add(key.GetHashKey(), (key, value));
        }

        return (Value.Hash(hashedPairs), null);
    }

    private Value BuildArray(int startIndex, int endIndex)
    {
        return Value.Array(_stack[startIndex..endIndex]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? ExecutePrefixOperation(OpCode op)
    {
        var operand = Pop();
        var result = Value.PrefixOperation(op, operand);
        if (result.IsError)
            return result.ErrorMessage!;
        return Push(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? ExecuteBinaryOperation(OpCode op)
    {
        var right = Pop();
        var left = Pop();
        var result = Value.InfixOperation(left, op, right);
        if (result.IsError)
            return result.ErrorMessage!;
        return Push(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? Push(Value obj)
    {
        if (_sp >= StackSize)
            return "stack overflow";

        _stack[_sp] = obj;
        _sp++;
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Value Pop()
    {
        var o = _stack[_sp - 1];
        _sp--;
        LastPoppedStackElement = o;
        return o;
    }
}

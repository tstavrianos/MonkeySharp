using MonkeySharp.Core.Objects;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.VirtualMachine;

public class Vm
{
    private const int StackSize = 2048;

    public const int GlobalsSize = 65536;
    private const int MaxFrames = 1024;

    private readonly IObject[] _constants;
    private readonly IObject[] _stack = new IObject[StackSize];
    private readonly IObject[] _globals = new IObject[GlobalsSize];
    private int _sp;

    private readonly Frame[] _frames = new Frame[MaxFrames];
    private int _frameIndex;

    public Vm(ByteCode bytecode)
    {
        var function = new CompiledFunctionObject(bytecode.Instructions, 0, 0);
        var closure = new ClosureObject(function, []);
        PushFrame(new Frame(closure, 0));
        _constants = bytecode.Constants;
        _sp = 0;
    }

    public Vm(ByteCode bytecode, IObject[] s) : this(bytecode)
    {
        Array.Copy(s, _globals, s.Length);
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
        return _frames[--_frameIndex];
    }

    public IObject LastPoppedStackElement { get; private set; }

    public string Run()
    {
        var currentFrame = CurrentFrame();
        var ins = currentFrame.Instructions().AsSpan();
        while (currentFrame.Ip < ins.Length - 1)
        {
            currentFrame.Ip++;
            var ip = currentFrame.Ip;
            //var ins = currentFrame.Instructions();
            var op = (OpCode) ins[ip];
            switch (op)
            {
                case OpCode.Constant:
                {
                    var constIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;

                    var err = Push(_constants[constIndex]);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Add:
                case OpCode.Subtract:
                case OpCode.Multiply:
                case OpCode.Divide:
                {
                    var err = ExecuteBinaryOperation(op);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Pop:
                    Pop();
                    break;
                case OpCode.True:
                {
                    var err = Push(BooleanObject.True);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.False:
                {
                    var err = Push(BooleanObject.False);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Equal:
                case OpCode.NotEqual:
                case OpCode.GreaterThan:
                {
                    var err = ExecuteComparison(op);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Bang:
                {
                    var err = ExecuteBangOperator();
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Minus:
                {
                    var err = ExecuteMinusOperator();
                    if (!string.IsNullOrEmpty(err)) return err;
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
                    if (!IsTruthy(condition)) currentFrame.Ip = pos - 1;
                    break;
                }
                case OpCode.Null:
                {
                    var err = Push(NullObject.Null);
                    if (!string.IsNullOrEmpty(err)) return err;
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
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Array:
                {
                    var numElements = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var array = BuildArray(_sp - numElements, _sp);
                    _sp = _sp - numElements;
                    var err = Push(array);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Hash:
                {
                    var numElements = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    currentFrame.Ip += 2;
                    var (hash, err) = BuildHash(_sp - numElements, _sp);
                    if (!string.IsNullOrEmpty(err)) return err;
                    _sp = _sp - numElements;
                    err = Push(hash);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Index:
                {
                    var index = Pop();
                    var left = Pop();
                    var err = ExecuteIndexExpression(left, index);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Call:
                {
                    var numArgs = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var err = ExecuteCall(numArgs);
                    if (!string.IsNullOrEmpty(err)) return err;
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
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Return:
                {
                    var frame = PopFrame();
                    currentFrame = CurrentFrame();
                    ins = currentFrame.Instructions().AsSpan();
                    _sp = frame.BasePointer - 1;
                    var err = Push(NullObject.Null);
                    if (!string.IsNullOrEmpty(err)) return err;
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
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.GetBuiltin:
                {
                    var builtinIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var definition = Builtins.ByIndex(builtinIndex);
                    var err = Push(definition);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.Closure:
                {
                    var constantIndex = BinaryPrimitives.ReadUInt16BigEndian(ins.Slice(ip + 1));
                    var numFree = ins[ip + 3];
                    currentFrame.Ip += 3;
                    var err = PushClosure(constantIndex, numFree);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.GetFree:
                {
                    var freeIndex = ins[ip + 1];
                    currentFrame.Ip += 1;
                    var currentClosure = currentFrame.Closure;
                    var err = Push(currentClosure.Free[freeIndex]);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
                case OpCode.CurrentClosure:
                {
                    var currentClosure = currentFrame.Closure;
                    var err = Push(currentClosure);
                    if (!string.IsNullOrEmpty(err)) return err;
                    break;
                }
            }
        }

        return null;
    }

    private string PushClosure(ushort constantIndex, byte numFree)
    {
        var constant = _constants[constantIndex];
        if (constant is not CompiledFunctionObject function) return $"not a function: {constant.GetType().Name}";
        var free = new IObject[numFree];
        for (var i = 0; i < numFree; i++) free[i] = _stack[_sp - (numFree - i)];
        var closure = new ClosureObject(function, free);
        return Push(closure);
    }

    private string ExecuteCall(int numArgs)
    {
        var callee = _stack[_sp - 1 - numArgs];
        switch (callee)
        {
            case ClosureObject closure:
                return CallClosure(closure, numArgs);
            case BuiltinObject builtinObject:
                return CallBuiltin(builtinObject, numArgs);
            default:
                return "calling a non-function and non-built-in";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string CallClosure(ClosureObject closure, int numArgs)
    {
        if (closure.Function.NumParameters != numArgs)
            return $"wrong number of arguments. want={closure.Function.NumParameters}, got={numArgs}";
        var frame = new Frame(closure, _sp - numArgs);
        PushFrame(frame);
        _sp = frame.BasePointer + closure.Function.NumLocals;
        return null;
    }

    private string CallBuiltin(BuiltinObject builtinObject, int numArgs)
    {
        var args = _stack[(_sp - numArgs).._sp];
        var result = builtinObject.Function(args);
        _sp = _sp - numArgs - 1;
        if (result != null)
        {
            if (result is ErrorObject error) return error.Message;
            Push(result);
        }
        else
        {
            Push(NullObject.Null);
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteIndexExpression(IObject left, IObject index)
    {
        if (left is ArrayObject arrayLeft && index is IntegerObject integerIndex)
            return ExecuteArrayIndex(arrayLeft, integerIndex);
        if (left is HashObject hashLeft)
            return ExecuteHashIndex(hashLeft, index);
        return $"index operator not supported: {left.GetType().Name}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteHashIndex(HashObject hashLeft, IObject index)
    {
        if (index is not IHashableObject hashable) return $"unusable as hash key: {index.GetType().Name}";
        if (!hashLeft.Pairs.TryGetValue(hashable.HashKey(), out var pair))
            return Push(NullObject.Null);
        return Push(pair.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteArrayIndex(ArrayObject arrayLeft, IntegerObject integerIndex)
    {
        var i = (int) integerIndex.Value;
        var max = arrayLeft.Elements.Count - 1;
        if (i < 0 || i > max) return Push(NullObject.Null);
        return Push(arrayLeft.Elements[i]);
    }

    private (HashObject hash, string err) BuildHash(int startIndex, int endIndex)
    {
        var hashedPairs = new Dictionary<HashKey, (IHashableObject Key, IObject Value)>(endIndex - startIndex);

        for (var i = startIndex; i < endIndex; i += 2)
        {
            var key = _stack[i];
            var value = _stack[i + 1];

            if (key is not IHashableObject hashable) return (null, $"unusable as hash key: {key.Type}");

            hashedPairs.Add(hashable.HashKey(), (hashable, value));
        }

        return (new HashObject(hashedPairs), null);
    }

    private ArrayObject BuildArray(int startIndex, int endIndex)
    {
        var elements = new List<IObject>(endIndex - startIndex);
        for (var i = startIndex; i < endIndex; i++)
            elements.Add(_stack[i]);
        return new ArrayObject(elements);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTruthy(IObject o)
    {
        if (o == NullObject.Null) return false;
        if (o is BooleanObject b && b == BooleanObject.False) return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteMinusOperator()
    {
        var op = Pop();
        switch (op)
        {
            case IntegerObject i:
                return Push(IntegerObject.Create(-i.Value));
            default:
                return $"unsupported type for negation: {op.GetType().Name}";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteBangOperator()
    {
        var op = Pop();
        switch (op)
        {
            case BooleanObject b:
                return Push(b.Value ? BooleanObject.False : BooleanObject.True);
            case NullObject _:
                return Push(BooleanObject.True);
            default:
                return Push(BooleanObject.False);
        }
    }

    private string ExecuteComparison(OpCode op)
    {
        var right = Pop();
        var left = Pop();

        if (left is IntegerObject leftInt && right is IntegerObject rightInt)
            return ExecuteIntegerComparison(leftInt, op, rightInt);

        if (left is StringObject leftStr && right is StringObject rightStr)
        {
            if (op == OpCode.Equal)
                return Push(leftStr.Value == rightStr.Value ? BooleanObject.True : BooleanObject.False);
            if (op == OpCode.NotEqual)
                return Push(leftStr.Value != rightStr.Value ? BooleanObject.True : BooleanObject.False);
            return $"unknown operator: {op} (STRING)";
        }

        switch (op)
        {
            case OpCode.Equal:
                return Push(right == left ? BooleanObject.True : BooleanObject.False);
            case OpCode.NotEqual:
                return Push(right != left ? BooleanObject.True : BooleanObject.False);
            default:
                return $"unknown operator: {op} ({left.GetType().Name} {right.GetType().Name})";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteIntegerComparison(IntegerObject leftInt, OpCode op, IntegerObject rightInt)
    {
        switch (op)
        {
            case OpCode.Equal:
                return Push(leftInt.Value == rightInt.Value ? BooleanObject.True : BooleanObject.False);
            case OpCode.NotEqual:
                return Push(leftInt.Value != rightInt.Value ? BooleanObject.True : BooleanObject.False);
            case OpCode.GreaterThan:
                return Push(leftInt.Value > rightInt.Value ? BooleanObject.True : BooleanObject.False);
            default:
                return $"unknown operator: {op}";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteBinaryOperation(OpCode op)
    {
        var right = Pop();
        var left = Pop();
        if (left is IntegerObject leftInt && right is IntegerObject rightInt)
            return ExecuteBinaryIntegerOperation(leftInt, op, rightInt);
        if (left is StringObject leftStr && right is StringObject rightStr && op == OpCode.Add)
            return Push(new StringObject(leftStr.Value + rightStr.Value));

        return $"unsupported types for binary operation: {left.GetType().Name} {right.GetType().Name}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ExecuteBinaryIntegerOperation(IntegerObject leftInt, OpCode op, IntegerObject rightInt)
    {
        long result;
        switch (op)
        {
            case OpCode.Add:
                result = leftInt.Value + rightInt.Value;
                break;
            case OpCode.Subtract:
                result = leftInt.Value - rightInt.Value;
                break;
            case OpCode.Multiply:
                result = leftInt.Value * rightInt.Value;
                break;
            case OpCode.Divide:
                result = leftInt.Value / rightInt.Value;
                break;
            default:
                return $"unknown integer operator: {op}";
        }

        return Push(IntegerObject.Create(result));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string Push(IObject obj)
    {
        if (_sp >= StackSize) return "stack overflow";

        _stack[_sp] = obj;
        _sp++;
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObject Pop()
    {
        var o = _stack[_sp - 1];
        //_stack[_sp - 1] = null;
        _sp--;
        LastPoppedStackElement = o;
        return o;
    }
}
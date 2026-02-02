using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.VirtualMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Environment = MonkeySharp.Core.Interpreter.Environment;

namespace MonkeySharp.Core.Objects;

public enum ValueKind
{
    Int,
    String,
    Boolean,
    Null,
    Error,
    Array,
    Hash,
    Function,
    Builtin,
    ReturnValue,
    CompiledFunction,
    Closure
}

// Dedicated classes to avoid tuple boxing
public sealed class CompiledFunctionData
{
    public byte[] Instructions { get; }
    public int NumLocals { get; }
    public int NumParameters { get; }

    internal CompiledFunctionData(byte[] instructions, int numLocals, int numParameters)
    {
        Instructions = instructions;
        NumLocals = numLocals;
        NumParameters = numParameters;
    }
}

public sealed class ClosureData
{
    public Value Function { get; }
    public Value[] Free { get; }

    internal ClosureData(Value function, Value[] free)
    {
        Function = function;
        Free = free;
    }
}

public sealed class FunctionData
{
    public IReadOnlyList<Identifier> Parameters { get; }
    public BlockStatement Body { get; }
    public Environment Environment { get; }

    internal FunctionData(IReadOnlyList<Identifier> parameters, BlockStatement body, Environment environment)
    {
        Parameters = parameters;
        Body = body;
        Environment = environment;
    }
}

public readonly struct Value : IEquatable<Value>
{
    public static readonly Value NullValue = Null();
    public static readonly Value True = Boolean(true);
    public static readonly Value False = Boolean(false);

    private readonly ValueKind _kind;
    private readonly long _intValue;
    private readonly object _objValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Value(ValueKind valueKind, long val, object o)
    {
        _kind = valueKind;
        _intValue = val;
        _objValue = o;
    }

    public string Type => _kind switch
    {
        ValueKind.Int => "INTEGER",
        ValueKind.String => "STRING",
        ValueKind.Boolean => "BOOLEAN",
        ValueKind.Null => "NULL",
        ValueKind.Error => "ERROR",
        ValueKind.Array => "ARRAY",
        ValueKind.Hash => "HASH",
        ValueKind.Function => "FUNCTION",
        ValueKind.Builtin => "BUILTIN",
        ValueKind.ReturnValue => "RETURN_VALUE",
        ValueKind.CompiledFunction => "COMPILED_FUNCTION_OBJ",
        ValueKind.Closure => "CLOSURE",
        _ => "UNKNOWN"
    };

    public string Inspect => _kind switch
    {
        ValueKind.Int => _intValue.ToString(),
        ValueKind.String => (string) _objValue,
        ValueKind.Boolean => _intValue != 0 ? "true" : "false",
        ValueKind.Null => "null",
        ValueKind.Error => (string) _objValue,
        ValueKind.Array => ArrayInspect,
        ValueKind.Hash => HashInspect,
        ValueKind.Function => FunctionInspect,
        ValueKind.Builtin => "<builtin function>",
        ValueKind.ReturnValue => ReturnValueInspect,
        ValueKind.CompiledFunction => CompiledFunctionInspect,
        ValueKind.Closure => ClosureInspect,
        _ => _objValue?.ToString() ?? "null"
    };

    // Basic type factory methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value Integer(long val)
    {
        return new Value(ValueKind.Int, val, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value String(string val)
    {
        return new Value(ValueKind.String, 0, val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value Boolean(bool val)
    {
        return new Value(ValueKind.Boolean, val ? 1 : 0, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value Null()
    {
        return new Value(ValueKind.Null, 0, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value Error(string message)
    {
        return new Value(ValueKind.Error, 0, message);
    }

    // Complex type factory methods
    public static Value Array(IReadOnlyList<Value> elements)
    {
        return new Value(ValueKind.Array, 0, new List<Value>(elements));
    }

    public static Value Hash(Dictionary<HashKey, (Value Key, Value Value)> pairs)
    {
        return new Value(ValueKind.Hash, 0, pairs);
    }

    public static Value Function(IReadOnlyList<Identifier> parameters, BlockStatement body,
        Environment environment)
    {
        return new Value(ValueKind.Function, 0, new FunctionData(parameters, body, environment));
    }

    public static Value Builtin(Func<IReadOnlyList<Value>, Value> function)
    {
        return new Value(ValueKind.Builtin, 0, function);
    }

    public static Value ReturnValue(Value value)
    {
        return new Value(ValueKind.ReturnValue, 0, value);
    }

    public static Value CompiledFunction(byte[] instructions, int numLocals, int numParameters)
    {
        return new Value(ValueKind.CompiledFunction, 0,
            new CompiledFunctionData(instructions, numLocals, numParameters));
    }

    public static Value Closure(Value compiledFunction, Value[] freeVariables)
    {
        return new Value(ValueKind.Closure, 0, new ClosureData(compiledFunction, freeVariables));
    }

    // Basic value accessors
    public long IntValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Int ? _intValue : 0;
    }

    public string StringValue => _kind == ValueKind.String ? (string) _objValue : null;

    public bool BooleanValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Boolean && _intValue != 0;
    }

    public string ErrorMessage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Error ? (string) _objValue : null;
    }

    public ValueKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind;
    }

    // Complex value accessors - now returning dedicated classes instead of tuples
    public List<Value> ArrayElements => _kind == ValueKind.Array ? (List<Value>) _objValue : null;

    public Dictionary<HashKey, (Value Key, Value Value)> HashPairs =>
        _kind == ValueKind.Hash ? (Dictionary<HashKey, (Value Key, Value Value)>) _objValue : null;

    public FunctionData FunctionData =>
        _kind == ValueKind.Function ? (FunctionData) _objValue : null;

    public Func<IReadOnlyList<Value>, Value> BuiltinFunction =>
        _kind == ValueKind.Builtin ? (Func<IReadOnlyList<Value>, Value>) _objValue : null;

    public Value InnerReturnValue =>
        _kind == ValueKind.ReturnValue ? (Value) _objValue : default;

    public CompiledFunctionData CompiledFunctionData =>
        _kind == ValueKind.CompiledFunction ? (CompiledFunctionData) _objValue : null;

    public ClosureData ClosureData =>
        _kind == ValueKind.Closure ? (ClosureData) _objValue : null;

    // Helper methods for type checking
    public bool IsInteger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Int;
    }

    public bool IsString
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.String;
    }

    public bool IsBoolean
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Boolean;
    }

    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Null;
    }

    public bool IsError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Error;
    }

    public bool IsArray
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Array;
    }

    public bool IsHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Hash;
    }

    public bool IsFunction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Function;
    }

    public bool IsBuiltin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Builtin;
    }

    public bool IsReturnValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.ReturnValue;
    }

    public bool IsCompiledFunction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.CompiledFunction;
    }

    public bool IsClosure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == ValueKind.Closure;
    }

    // Inspect helpers for complex types
    private string ArrayInspect
    {
        get
        {
            var elements = ArrayElements;
            return elements != null
                ? $"[{string.Join(", ", elements.Select(x => x.Inspect))}]"
                : "[]";
        }
    }

    private string HashInspect
    {
        get
        {
            var pairs = HashPairs;
            if (pairs == null || pairs.Count == 0)
                return "{}";

            var ss = new System.Text.StringBuilder();
            ss.Append('{');
            ss.Append(string.Join(", ",
                pairs.OrderBy(x => x.Value.Key.Inspect)
                    .Select(x => $"{x.Value.Key.Inspect}: {x.Value.Value.Inspect}")));
            ss.Append('}');
            return ss.ToString();
        }
    }

    private string FunctionInspect
    {
        get
        {
            var data = FunctionData;
            if (data?.Parameters == null)
                return "fn() {}";

            var buffer = new System.Text.StringBuilder();
            buffer.Append("fn(");
            buffer.Append(string.Join(", ", data.Parameters.Select(x => x.ToString())));
            buffer.Append(") {\n");
            buffer.Append(data.Body?.ToString() ?? "");
            buffer.Append("}\n");
            return buffer.ToString();
        }
    }

    private string ReturnValueInspect => InnerReturnValue.Inspect;

    private string CompiledFunctionInspect => $"CompiledFunction[{GetHashCode()}]";

    private string ClosureInspect => $"Closure[{GetHashCode()}]";

    public bool IsHashable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind is ValueKind.Int or ValueKind.String or ValueKind.Boolean;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HashKey GetHashKey()
    {
        return _kind switch
        {
            ValueKind.Int => new HashKey(Type, (ulong) _intValue),
            ValueKind.String => ComputeStringHash((string) _objValue),
            ValueKind.Boolean => new HashKey(Type, (ulong) (_intValue != 0 ? 1 : 0)),
            _ => throw new InvalidOperationException($"Type {Type} is not hashable")
        };
    }

    private static HashKey ComputeStringHash(string value)
    {
        const ulong basis = 0xCBF29CE484222325UL;
        const ulong prime = 0x00000100000001B3UL;

        var hash = basis;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return new HashKey("STRING", hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Value other)
    {
        return _kind == other._kind && _intValue == other._intValue && Equals(_objValue, other._objValue);
    }

    public override bool Equals(object obj)
    {
        return obj is Value other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int) _kind, _intValue, _objValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Value left, Value right)
    {
        return left.Equals(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Value left, Value right)
    {
        return !left.Equals(right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTruthy()
    {
        if (IsNull) return false;
        if (IsBoolean && !BooleanValue) return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value EvaluatorInfixOperation(Value left, string op, Value right)
    {
        if (left._kind != right._kind) return Error($"type mismatch: {left.Type} {op} {right.Type}");
        if (op == "==") return left == right ? True : False;
        if (op == "!=") return left != right ? True : False;
        if (left.IsInteger && right.IsInteger)
            return op switch
            {
                "+" => Integer(left.IntValue + right.IntValue),
                "-" => Integer(left.IntValue - right.IntValue),
                "*" => Integer(left.IntValue * right.IntValue),
                "/" => Integer(left.IntValue / right.IntValue),
                "<" => left.IntValue < right.IntValue ? True : False,
                ">" => left.IntValue > right.IntValue ? True : False,
                _ => Error($"unknown operator: {left.Type} {op} {right.Type}")
            };

        if (left.IsString && right.IsString && op == "+")
            return String(left.StringValue + right.StringValue);

        return Error($"unknown operator: {left.Type} {op} {right.Type}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value EvaluatorPrefixOperation(string op, Value right)
    {
        if (op == "!")
        {
            if (right.IsBoolean)
                return !right.BooleanValue ? True : False;
            if (right.IsNull)
                return True;
            return False;
        }

        if (op == "-" && right.IsInteger) return Integer(-right.IntValue);
        return Error($"unknown operator: {op}{right.Type}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value VmInfixOperation(Value left, OpCode op, Value right)
    {
        if (left._kind != right._kind) return Error($"type mismatch: {left.Type} {op} {right.Type}");
        if (op == OpCode.Equal) return left == right ? True : False;
        if (op == OpCode.NotEqual) return left != right ? True : False;
        if (left.IsInteger && right.IsInteger)
            return op switch
            {
                OpCode.Add => Integer(left.IntValue + right.IntValue),
                OpCode.Subtract => Integer(left.IntValue - right.IntValue),
                OpCode.Multiply => Integer(left.IntValue * right.IntValue),
                OpCode.Divide => Integer(left.IntValue / right.IntValue),
                OpCode.GreaterThan => left.IntValue > right.IntValue ? True : False,
                _ => Error($"unknown operator: {left.Type} {op} {right.Type}")
            };

        if (left.IsString && right.IsString && op == OpCode.Add)
            return String(left.StringValue + right.StringValue);

        return Error($"unknown operator: {left.Type} {op} {right.Type}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Value VmPrefixOperation(OpCode op, Value right)
    {
        if (op == OpCode.Bang)
        {
            if (right.IsBoolean)
                return !right.BooleanValue ? True : False;
            if (right.IsNull)
                return True;
            return False;
        }

        if (op == OpCode.Minus && right.IsInteger) return Integer(-right.IntValue);
        return Error($"unknown operator: {op}{right.Type}");
    }
}
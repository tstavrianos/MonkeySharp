using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Objects;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonkeySharp.Core.Interpreter;

public class Evaluator
{
    public Value Eval(Node node, Environment environment)
    {
        switch (node)
        {
            case ProgramNode program:
                return EvalProgram(program, environment);
            case ExpressionStatement expressionStatement:
                return Eval(expressionStatement.Expression, environment);
            case IntegerLiteral integerLiteral:
                return Value.Integer(integerLiteral.Value);
            case BooleanLiteral booleanLiteral:
                return Value.Boolean(booleanLiteral.Value);
            case PrefixExpression prefixExpression:
            {
                var right = Eval(prefixExpression.Right, environment);
                if (right.IsError) return right;
                return Value.EvaluatorPrefixOperation(prefixExpression.Operator, right);
            }
            case InfixExpression infixExpression:
            {
                var left = Eval(infixExpression.Left, environment);
                if (left.IsError) return left;
                var right = Eval(infixExpression.Right, environment);
                if (right.IsError) return right;
                return Value.EvaluatorInfixOperation(left, infixExpression.Operator, right);
            }
            case BlockStatement blockStatement:
                return EvalBlockStatement(blockStatement, environment);
            case IfExpression ifExpression:
                return EvalIfExpression(ifExpression, environment);
            case ReturnStatement returnStatement:
            {
                var value = Eval(returnStatement.ReturnValue, environment);
                if (value.IsError) return value;
                return Value.ReturnValue(value);
            }
            case LetStatement letStatement:
            {
                var value = Eval(letStatement.Value, environment);
                if (value.IsError) return value;
                environment.Set(letStatement.Name.Value, value);
                break;
            }
            case Identifier identifier:
                return EvalIdentifier(identifier, environment);
            case FunctionLiteral functionLiteral:
            {
                var parameters = functionLiteral.Parameters;
                var body = functionLiteral.Body;
                return Value.Function(parameters, body, environment);
            }
            case CallExpression callExpression:
            {
                var function = Eval(callExpression.Function, environment);
                if (function.IsError) return function;
                var args = EvalExpressions(callExpression.Arguments, environment);
                if (args.Length == 1 && args[0].IsError) return args[0];

                return ApplyFunction(function, args);
            }
            case StringLiteral stringLiteral:
                return Value.String(stringLiteral.Value);
            case ArrayLiteral arrayLiteral:
            {
                var elements = EvalExpressions(arrayLiteral.Elements, environment);
                if (elements.Length == 1 && elements[0].IsError) return elements[0];
                return Value.Array(elements);
            }
            case IndexExpression indexExpression:
            {
                var left = Eval(indexExpression.Left, environment);
                if (left.IsError) return left;
                var index = Eval(indexExpression.Index, environment);
                if (index.IsError) return index;
                return EvalIndexExpression(left, index);
            }
            case HashLiteral hashLiteral:
                return EvalHashLiteral(hashLiteral, environment);
        }

        return Value.Null();
    }

    private Value EvalHashLiteral(HashLiteral hashLiteral, Environment environment)
    {
        var pairs = new Dictionary<HashKey, (Value Key, Value Value)>();
        foreach (var (key, value) in hashLiteral.Pairs)
        {
            var k = Eval(key, environment);
            if (k.IsError) return k;
            if (!k.IsHashable)
                return Value.Error($"unusable as hash key: {k.Type}");
            var v = Eval(value, environment);
            if (v.IsError) return v;
            var hashed = k.GetHashKey();
            pairs.Add(hashed, (k, v));
        }

        return Value.Hash(pairs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value EvalIndexExpression(Value left, Value index)
    {
        if (left.IsArray && index.IsInteger)
            return EvalArrayIndexExpression(left, index);
        if (left.IsHash)
            return EvalHashIndexExpression(left, index);

        return Value.Error($"index operator not supported: {left.Type}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value EvalHashIndexExpression(Value hashValue, Value index)
    {
        if (!index.IsHashable)
            return Value.Error($"unusable as hash key: {index.Type}");

        var hashed = index.GetHashKey();
        var pairs = hashValue.HashPairs;

        if (!pairs.TryGetValue(hashed, out var pair))
            return Value.Null();

        return pair.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value EvalArrayIndexExpression(Value arrayValue, Value indexValue)
    {
        var elements = arrayValue.ArrayElements;
        var idx = indexValue.IntValue;
        var max = elements.Count - 1;

        if (idx < 0 || idx > max)
            return Value.Null();

        return elements[(int) idx];
    }

    private Value ApplyFunction(Value function, IReadOnlyList<Value> args)
    {
        if (function.IsFunction)
        {
            var functionData = function.FunctionData;
            var extendedEnv = ExtendFunctionEnv(functionData, args);
            var evaluated = Eval(functionData.Body, extendedEnv);
            if (evaluated.IsReturnValue) return evaluated.InnerReturnValue;
            return evaluated;
        }

        if (function.IsBuiltin) return function.BuiltinFunction(args);

        return Value.Error($"not a function: {function.Type}");
    }

    private static Environment ExtendFunctionEnv(
        (IReadOnlyList<Identifier> Parameters, BlockStatement Body, Environment Environment) functionData,
        IReadOnlyList<Value> args)
    {
        var env = new Environment(functionData.Environment, args.Count);
        for (var i = 0; i < functionData.Parameters.Count; i++)
            env.Set(functionData.Parameters[i].Value, args[i]);
        return env;
    }

    private Value[] EvalExpressions(IReadOnlyList<Expression> callExpressionArguments,
        Environment environment)
    {
        var result = ArrayPool<Value>.Shared.Rent(callExpressionArguments.Count);
        var actualCount = 0;
        foreach (var expression in callExpressionArguments)
        {
            var evaluated = Eval(expression, environment);
            if (evaluated.IsError)
            {
                ArrayPool<Value>.Shared.Return(result);
                return [evaluated];
            }

            result[actualCount++] = evaluated;
        }

        var final = new Value[actualCount];
        Array.Copy(result, final, actualCount);
        ArrayPool<Value>.Shared.Return(result);
        return final;
    }

    private static Value EvalIdentifier(Identifier identifier, Environment environment)
    {
        var (val, ok) = environment.Get(identifier.Value);
        if (ok) return val;
        if (!Builtins.TryGet(identifier.Value, out var builtinValue))
            return Value.Error($"identifier not found: {identifier.Value}");
        return builtinValue;
    }

    private Value EvalBlockStatement(BlockStatement blockStatement, Environment environment)
    {
        var result = Value.Null();
        foreach (var statement in blockStatement.Statements)
        {
            result = Eval(statement, environment);
            if (result.IsReturnValue || result.IsError) return result;
        }

        return result;
    }

    private Value EvalProgram(ProgramNode programNode, Environment environment)
    {
        var result = Value.Null();
        foreach (var statement in programNode.Statements)
        {
            result = Eval(statement, environment);
            if (result.IsReturnValue) return result.InnerReturnValue;
            if (result.IsError) return result;
        }

        return result;
    }

    private Value EvalIfExpression(IfExpression ifExpression, Environment environment)
    {
        var condition = Eval(ifExpression.Condition, environment);
        if (condition.IsError) return condition;
        if (condition.IsTruthy()) return Eval(ifExpression.Consequence, environment);
        if (ifExpression.Alternative != null) return Eval(ifExpression.Alternative, environment);
        return Value.Null();
    }
}
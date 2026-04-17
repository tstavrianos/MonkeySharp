using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.TreeWalk;

internal static class TreeWalker
{
    internal static MonkeyValue Evaluate(
        CompilationResult compilationResult,
        Session? session = null
    )
    {
        if (!compilationResult.IsValid)
            return MonkeyValue.Error(string.Join("; ", compilationResult.Diagnostics));

        return new MonkeyValue(
            Eval(
                compilationResult.ProgramNode!,
                session?.SymbolTable ?? Session.CreateDefaultSymbolTable()
            )
        );
    }

    internal static Value Eval(Node? node, SymbolTable symbolTable)
    {
        switch (node)
        {
            case ProgramNode program:
                return EvalProgram(program, symbolTable);
            case ExpressionStatement expressionStatement:
                return Eval(expressionStatement.Expression, symbolTable);
            case IntegerLiteral integerLiteral:
                return Value.Integer(integerLiteral.Value);
            case BooleanLiteral booleanLiteral:
                return booleanLiteral.Value ? Value.True : Value.False;
            case PrefixExpression prefixExpression:
            {
                var right = Eval(prefixExpression.Right, symbolTable);
                if (right.IsError)
                    return right;
                return Value.PrefixOperation(prefixExpression.Operator, right);
            }
            case InfixExpression infixExpression:
            {
                var left = Eval(infixExpression.Left, symbolTable);
                if (left.IsError)
                    return left;
                var right = Eval(infixExpression.Right, symbolTable);
                if (right.IsError)
                    return right;
                return Value.InfixOperation(left, infixExpression.Operator, right);
            }
            case BlockStatement blockStatement:
                return EvalBlockStatement(blockStatement, symbolTable);
            case IfExpression ifExpression:
                return EvalIfExpression(ifExpression, symbolTable);
            case ReturnStatement returnStatement:
            {
                var value = Eval(returnStatement.ReturnValue, symbolTable);
                if (value.IsError)
                    return value;
                return Value.ReturnValue(value);
            }
            case LetStatement letStatement:
            {
                var value = Eval(letStatement.Value, symbolTable);
                if (value.IsError)
                    return value;
                symbolTable.Set(letStatement.Name.Value, value);
                break;
            }
            case Identifier identifier:
                return EvalIdentifier(identifier, symbolTable);
            case FunctionLiteral functionLiteral:
            {
                var parameters = functionLiteral.Parameters;
                var body = functionLiteral.Body;
                return Value.Function(parameters, body, symbolTable);
            }
            case CallExpression callExpression:
            {
                var function = Eval(callExpression.Function, symbolTable);
                if (function.IsError)
                    return function;
                var args = EvalExpressions(callExpression.Arguments, symbolTable);
                if (args.Length == 1 && args[0].IsError)
                    return args[0];

                return ApplyFunction(function, args);
            }
            case StringLiteral stringLiteral:
                return Value.String(stringLiteral.Value);
            case ArrayLiteral arrayLiteral:
            {
                var elements = EvalExpressions(arrayLiteral.Elements, symbolTable);
                if (elements.Length == 1 && elements[0].IsError)
                    return elements[0];
                return Value.Array(elements);
            }
            case IndexExpression indexExpression:
            {
                var left = Eval(indexExpression.Left, symbolTable);
                if (left.IsError)
                    return left;
                var index = Eval(indexExpression.Index, symbolTable);
                if (index.IsError)
                    return index;
                return EvalIndexExpression(left, index);
            }
            case HashLiteral hashLiteral:
                return EvalHashLiteral(hashLiteral, symbolTable);
        }

        return Value.NullValue;
    }

    private static Value EvalHashLiteral(HashLiteral hashLiteral, SymbolTable symbolTable)
    {
        var pairs = new Dictionary<HashKey, (Value Key, Value Value)>();
        foreach (var (key, value) in hashLiteral.Pairs)
        {
            var k = Eval(key, symbolTable);
            if (k.IsError)
                return k;
            if (!k.IsHashable)
                return Value.Error($"unusable as hash key: {k.Type}");
            var v = Eval(value, symbolTable);
            if (v.IsError)
                return v;
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
        var pairs = hashValue.HashPairs!;

        if (!pairs.TryGetValue(hashed, out var pair))
            return Value.NullValue;

        return pair.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value EvalArrayIndexExpression(Value arrayValue, Value indexValue)
    {
        var elements = arrayValue.ArrayElements!;
        var idx = indexValue.IntValue;
        var max = elements.Count - 1;

        if (idx < 0 || idx > max)
            return Value.NullValue;

        return elements[(int)idx];
    }

    private static Value ApplyFunction(Value function, IReadOnlyList<Value> args)
    {
        if (function.IsFunction)
        {
            var functionData = function.FunctionData!;
            var currentEnv = ExtendFunctionEnv(functionData, args);
            var currentBody = functionData.Body;

            // Tail call optimization loop
            while (true)
            {
                var evaluated = EvalBlockStatementWithTailCall(
                    currentBody,
                    currentEnv,
                    function,
                    out var tailCall
                );

                if (!tailCall.HasValue)
                {
                    // No tail call detected, return the result
                    if (evaluated.IsReturnValue)
                        return evaluated.InnerReturnValue;
                    return evaluated;
                }

                // Tail call detected - prepare for next iteration
                var (tailFunction, tailArgs) = tailCall.Value;

                if (!tailFunction.IsFunction)
                    // Not a function, can't optimize
                    return ApplyFunctionDirect(tailFunction, tailArgs);

                // Update for next iteration
                function = tailFunction;
                functionData = tailFunction.FunctionData!;
                currentEnv = ExtendFunctionEnv(functionData, tailArgs);
                currentBody = functionData.Body;
            }
        }

        if (function.IsBuiltin)
            return function.BuiltinFunction!(args);

        return Value.Error($"not a function: {function.Type}");
    }

    private static Value ApplyFunctionDirect(Value function, IReadOnlyList<Value> args)
    {
        if (function.IsFunction)
        {
            var functionData = function.FunctionData!;
            var extendedEnv = ExtendFunctionEnv(functionData, args);
            var evaluated = Eval(functionData.Body, extendedEnv);
            if (evaluated.IsReturnValue)
                return evaluated.InnerReturnValue;
            return evaluated;
        }

        if (function.IsBuiltin)
            return function.BuiltinFunction!(args);

        return Value.Error($"not a function: {function.Type}");
    }

    private static Value EvalBlockStatementWithTailCall(
        BlockStatement blockStatement,
        SymbolTable symbolTable,
        Value currentFunction,
        out (Value Function, Value[] Args)? tailCall
    )
    {
        tailCall = null;
        var result = Value.NullValue;
        var statements = blockStatement.Statements;

        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            var isLastStatement = i == statements.Count - 1;

            // Check if this is a return statement in tail position
            if (isLastStatement && statement is ReturnStatement returnStatement)
            {
                // Check if the return value is a tail call
                if (returnStatement.ReturnValue is CallExpression callExpression)
                {
                    var function = Eval(callExpression.Function, symbolTable);
                    if (function.IsError)
                        return Value.ReturnValue(function);

                    var args = EvalExpressions(callExpression.Arguments, symbolTable);
                    if (args.Length == 1 && args[0].IsError)
                        return Value.ReturnValue(args[0]);

                    // Check if this is a recursive call to the same function
                    if (
                        function.IsFunction
                        && ReferenceEquals(function.FunctionData, currentFunction.FunctionData)
                    )
                    {
                        // Tail call detected!
                        tailCall = (function, args);
                        return Value.NullValue; // Placeholder return
                    }

                    // Not a recursive call, but still in tail position
                    // Could optimize non-recursive tail calls too
                    return Value.ReturnValue(ApplyFunctionDirect(function, args));
                }

                // Regular return statement
                var value = Eval(returnStatement.ReturnValue, symbolTable);
                //if (value.IsError) return Value.ReturnValue(value);
                return Value.ReturnValue(value);
            }

            result = Eval(statement, symbolTable);
            if (result.IsReturnValue || result.IsError)
                return result;
        }

        return result;
    }

    private static SymbolTable ExtendFunctionEnv(
        FunctionData functionData,
        IReadOnlyList<Value> args
    )
    {
        var env = new SymbolTable(functionData.SymbolTable, args.Count);
        for (var i = 0; i < functionData.Parameters.Count; i++)
            env.Set(functionData.Parameters[i].Value, args[i]);
        return env;
    }

    private static Value[] EvalExpressions(
        IReadOnlyList<Expression> callExpressionArguments,
        SymbolTable symbolTable
    )
    {
        var result = ArrayPool<Value>.Shared.Rent(callExpressionArguments.Count);
        var actualCount = 0;
        foreach (var expression in callExpressionArguments)
        {
            var evaluated = Eval(expression, symbolTable);
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

    private static Value EvalIdentifier(Identifier identifier, SymbolTable symbolTable)
    {
        var (val, ok) = symbolTable.Get(identifier.Value);
        if (ok)
            return val;
        if (Builtins.TryGet(identifier.Value, out var builtinValue))
            return builtinValue;
        return Value.Error($"identifier not found: {identifier.Value}");
    }

    private static Value EvalBlockStatement(BlockStatement blockStatement, SymbolTable symbolTable)
    {
        var result = Value.NullValue;
        foreach (var statement in blockStatement.Statements)
        {
            result = Eval(statement, symbolTable);
            if (result.IsReturnValue || result.IsError)
                return result;
        }

        return result;
    }

    private static Value EvalProgram(ProgramNode programNode, SymbolTable symbolTable)
    {
        var result = Value.NullValue;
        foreach (var statement in programNode.Statements)
        {
            result = Eval(statement, symbolTable);
            if (result.IsReturnValue)
                return result.InnerReturnValue;
            if (result.IsError)
                return result;
        }

        return result;
    }

    private static Value EvalIfExpression(IfExpression ifExpression, SymbolTable symbolTable)
    {
        var condition = Eval(ifExpression.Condition, symbolTable);
        if (condition.IsError)
            return condition;
        if (condition.IsTruthy())
            return Eval(ifExpression.Consequence, symbolTable);
        if (ifExpression.Alternative != null)
            return Eval(ifExpression.Alternative, symbolTable);
        return Value.NullValue;
    }
}

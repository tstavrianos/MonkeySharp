using System.Collections.Generic;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.Interpreter;

public class Evaluator
{
    public IObject Eval(Node node, Environment environment)
    {
        switch (node)
        {
            case ProgramNode program:
                return EvalProgram(program, environment);
            case ExpressionStatement expressionStatement:
                return Eval(expressionStatement.Expression, environment);
            case IntegerLiteral integerLiteral:
                return new IntegerObject(integerLiteral.Value);
            case BooleanLiteral booleanLiteral:
                return booleanLiteral.Value ? BooleanObject.True : BooleanObject.False;
            case PrefixExpression prefixExpression:
            {
                var right = Eval(prefixExpression.Right, environment);
                if (right is ErrorObject) return right;
                return EvalPrefixExpression(prefixExpression.Operator, right);
            }
            case InfixExpression infixExpression:
            {
                var left = Eval(infixExpression.Left, environment);
                if (left is ErrorObject) return left;
                var right = Eval(infixExpression.Right, environment);
                if (right is ErrorObject) return right;
                return EvalInfixExpression(left, infixExpression.Operator, right);
            }
            case BlockStatement blockStatement:
                return EvalBlockStatement(blockStatement, environment);
            case IfExpression ifExpression:
                return EvalIfExpression(ifExpression, environment);
            case ReturnStatement returnStatement:
            {
                var value = Eval(returnStatement.ReturnValue, environment);
                if (value is ErrorObject) return value;
                return new ReturnValueObject(value);
            }
            case LetStatement letStatement:
            {
                var value = Eval(letStatement.Value, environment);
                if (value is ErrorObject) return value;
                environment.Set(letStatement.Name.Value, value);
                break;
            }
            case Identifier identifier:
                return EvalIdentifier(identifier, environment);
            case FunctionLiteral functionLiteral:
            {
                var parameters = functionLiteral.Parameters;
                var body = functionLiteral.Body;
                return new FunctionObject(parameters, body, environment);
            }
            case CallExpression callExpression:
            {
                var function = Eval(callExpression.Function, environment);
                if (function is ErrorObject) return function;
                var args = EvalExpressions(callExpression.Arguments, environment);
                if (args.Count == 1 && args[0] is ErrorObject) return args[0];

                return ApplyFunction(function, args);
            }
            case StringLiteral stringLiteral:
                return new StringObject(stringLiteral.Value);
            case ArrayLiteral arrayLiteral:
            {
                var elements = EvalExpressions(arrayLiteral.Elements, environment);
                if (elements.Count == 1 && elements[0] is ErrorObject) return elements[0];
                return new ArrayObject(elements);
            }
            case IndexExpression indexExpression:
            {
                var left = Eval(indexExpression.Left, environment);
                if (left is ErrorObject) return left;
                var index = Eval(indexExpression.Index, environment);
                if (index is ErrorObject) return index;
                return EvalIndexExpression(left, index);
            }
            case HashLiteral hashLiteral:
                return EvalHashLiteral(hashLiteral, environment);
        }

        return null;
    }

    private IObject EvalHashLiteral(HashLiteral hashLiteral, Environment environment)
    {
        var pairs = new Dictionary<HashKey, (IHashableObject, IObject)>();
        foreach (var (key, value) in hashLiteral.Pairs)
        {
            var k = Eval(key, environment);
            if (k is ErrorObject) return k;
            if (k is not IHashableObject hashKey)
                return new ErrorObject($"unusable as hash key: {k.Type}");
            var v = Eval(value, environment);
            if (v is ErrorObject) return v;
            var hashed = hashKey.HashKey();
            pairs.Add(hashed, (hashKey, v));
        }

        return new HashObject(pairs);
    }

    private static IObject EvalIndexExpression(IObject left, IObject index)
    {
        if (left is ArrayObject arrayObject && index is IntegerObject integerObject)
            return EvalArrayIndexExpression(arrayObject, integerObject);
        if (left is HashObject hashObject)
            return EvalHashIndexExpression(hashObject, index);

        return new ErrorObject($"index operator not supported: {left.Type}");
    }

    private static IObject EvalHashIndexExpression(HashObject hashObject, IObject index)
    {
        if (index is not IHashableObject hashKey)
            return new ErrorObject($"unusable as hash key: {index.Type}");
        var hashed = hashKey.HashKey();
        if (!hashObject.Pairs.TryGetValue(hashed, out var pair))
            return NullObject.Null;
        return pair.Value;
    }

    private static IObject EvalArrayIndexExpression(ArrayObject arrayObject, IntegerObject integerObject)
    {
        var max = arrayObject.Elements.Count - 1;
        if (integerObject.Value < 0 || integerObject.Value > max)
            return NullObject.Null;
        return arrayObject.Elements[(int) integerObject.Value];
    }

    private IObject ApplyFunction(IObject function, List<IObject> args)
    {
        switch (function)
        {
            case FunctionObject functionObject:
            {
                var extendedEnv = ExtendFunctionEnv(functionObject, args);
                var evaluated = Eval(functionObject.Body, extendedEnv);
                if (evaluated is ReturnValueObject returnValueObject) return returnValueObject.Value;
                return evaluated;
            }
            case BuiltinObject builtinObject:
            {
                return builtinObject.Function(args);
            }
        }

        return new ErrorObject($"not a function: {function.Type}");
    }

    private static Environment ExtendFunctionEnv(FunctionObject functionObject, List<IObject> args)
    {
        var env = new Environment(functionObject.Environment);
        for (var i = 0; i < functionObject.Parameters.Count; i++)
            env.Set(functionObject.Parameters[i].Value, args[i]);
        return env;
    }

    private List<IObject> EvalExpressions(IReadOnlyList<Expression> callExpressionArguments,
        Environment environment)
    {
        var result = new List<IObject>(callExpressionArguments.Count);
        foreach (var expression in callExpressionArguments)
        {
            var evaluated = Eval(expression, environment);
            if (evaluated is ErrorObject) return [evaluated];
            result.Add(evaluated);
        }

        return result;
    }

    private static IObject EvalIdentifier(Identifier identifier, Environment environment)
    {
        var (val, ok) = environment.Get(identifier.Value);
        if (ok) return val;
        if (!Builtins.TryGet(identifier.Value, out var builtinObject))
            return new ErrorObject($"identifier not found: {identifier.Value}");
        return builtinObject;
    }

    private IObject EvalBlockStatement(BlockStatement blockStatement, Environment environment)
    {
        IObject result = null;
        foreach (var statement in blockStatement.Statements)
        {
            result = Eval(statement, environment);
            if (result is ReturnValueObject || result is ErrorObject) return result;
        }

        return result;
    }

    private IObject EvalProgram(ProgramNode programNode, Environment environment)
    {
        IObject result = null;
        foreach (var statement in programNode.Statements)
        {
            result = Eval(statement, environment);
            if (result is ReturnValueObject returnValueObject) return returnValueObject.Value;
            if (result is ErrorObject) return result;
        }

        return result;
    }

    private IObject EvalIfExpression(IfExpression ifExpression, Environment environment)
    {
        var condition = Eval(ifExpression.Condition, environment);
        if (condition is ErrorObject) return condition;
        if (IsTruthy(condition)) return Eval(ifExpression.Consequence, environment);
        if (ifExpression.Alternative != null) return Eval(ifExpression.Alternative, environment);
        return NullObject.Null;
    }

    private static bool IsTruthy(IObject obj)
    {
        if (obj == NullObject.Null) return false;
        if (obj == BooleanObject.False) return false;
        return true;
    }

    private static IObject EvalInfixExpression(IObject left, string @operator, IObject right)
    {
        if (left is IntegerObject leftInteger && right is IntegerObject rightInteger)
            return EvalIntegerInfixOperator(leftInteger, @operator, rightInteger);
        if (left is BooleanObject leftBoolean && right is BooleanObject rightBoolean)
            return EvalBooleanInfixOperator(leftBoolean, @operator, rightBoolean);
        if (left is StringObject leftString && right is StringObject rightString)
            return EvalStringInfixOperator(leftString, @operator, rightString);

        if (left.Type != right.Type)
            return new ErrorObject(
                $"type mismatch: {left.Type} {@operator} {right.Type}");
        return new ErrorObject(
            $"unknown operator: {left.Type} {@operator} {right.Type}");
    }

    private static IObject EvalStringInfixOperator(StringObject leftString, string @operator, StringObject rightString)
    {
        if (@operator == "+")
            return new StringObject(leftString.Value + rightString.Value);
        if (@operator == "==")
            return leftString.Value == rightString.Value ? BooleanObject.True : BooleanObject.False;
        if (@operator == "!=")
            return leftString.Value != rightString.Value ? BooleanObject.True : BooleanObject.False;
        return new ErrorObject($"unknown operator: STRING {@operator} STRING");
    }

    private static IObject EvalIntegerInfixOperator(IntegerObject leftInteger, string @operator,
        IntegerObject rightInteger)
    {
        switch (@operator)
        {
            case "+":
                return new IntegerObject(leftInteger.Value + rightInteger.Value);
            case "-":
                return new IntegerObject(leftInteger.Value - rightInteger.Value);
            case "*":
                return new IntegerObject(leftInteger.Value * rightInteger.Value);
            case "/":
                return new IntegerObject(leftInteger.Value / rightInteger.Value);
            case "<":
                return leftInteger.Value < rightInteger.Value ? BooleanObject.True : BooleanObject.False;
            case ">":
                return leftInteger.Value > rightInteger.Value ? BooleanObject.True : BooleanObject.False;
            case "==":
                return leftInteger.Value == rightInteger.Value ? BooleanObject.True : BooleanObject.False;
            case "!=":
                return leftInteger.Value != rightInteger.Value ? BooleanObject.True : BooleanObject.False;
        }

        return new ErrorObject($"unknown operator: INTEGER {@operator} INTEGER");
    }

    private static IObject EvalBooleanInfixOperator(BooleanObject leftBoolean, string @operator,
        BooleanObject rightBoolean)
    {
        switch (@operator)
        {
            case "==":
                return leftBoolean.Value == rightBoolean.Value ? BooleanObject.True : BooleanObject.False;
            case "!=":
                return leftBoolean.Value != rightBoolean.Value ? BooleanObject.True : BooleanObject.False;
        }

        return new ErrorObject($"unknown operator: BOOLEAN {@operator} BOOLEAN");
    }

    private static IObject EvalPrefixExpression(string @operator, IObject right)
    {
        switch (@operator)
        {
            case "!":
                return EvalBangOperator(right);
            case "-":
                return EvalMinusPrefixOperator(right);
            default:
                return new ErrorObject($"unknown operator: {@operator}{right.Type}");
        }
    }

    private static IObject EvalMinusPrefixOperator(IObject right)
    {
        if (right is not IntegerObject integer)
            return new ErrorObject($"unknown operator: -{right.Type}");
        return new IntegerObject(-integer.Value);
    }

    private static BooleanObject EvalBangOperator(IObject right)
    {
        if (right == BooleanObject.True) return BooleanObject.False;
        if (right == BooleanObject.False) return BooleanObject.True;
        if (right == NullObject.Null) return BooleanObject.True;
        return BooleanObject.False;
    }
}
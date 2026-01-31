using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Ast.Visitors;
using MonkeySharp.Core.Objects;
using System.Collections.Generic;

namespace MonkeySharp.Core.Interpreter;

/// <summary>
/// Evaluates AST nodes using the Visitor pattern to produce runtime objects.
/// Implements both expression and statement visitor interfaces to traverse and evaluate
/// the abstract syntax tree of MonkeySharp programs.
/// </summary>
public class VisitorEvaluator : IExpressionVisitor<IObject>, IStatementVisitor<IObject>
{
    /// <summary>
    /// The current execution environment containing variable bindings.
    /// </summary>
    private Environment _environment;

    /// <summary>
    /// Evaluates an AST node within the specified environment.
    /// </summary>
    /// <param name="node">The AST node to evaluate (program, expression, or statement).</param>
    /// <param name="environment">The environment containing variable bindings.</param>
    /// <returns>The resulting object from evaluation, or null if the node type is unknown.</returns>
    public IObject Eval(Node node, Environment environment)
    {
        switch (node)
        {
            case ProgramNode program:
                return EvalProgram(program, environment);
            case Expression expression:
                return Eval(expression, environment);
            case Statement statement:
                return Eval(statement, environment);
        }

        return null;
    }

    /// <summary>
    /// Evaluates a complete program by executing each statement in sequence.
    /// Stops early if a return statement or error is encountered.
    /// </summary>
    /// <param name="program">The program node containing all statements.</param>
    /// <param name="environment">The environment for variable bindings.</param>
    /// <returns>The result of the last statement, unwrapped return values, or error objects.</returns>
    private IObject EvalProgram(ProgramNode program, Environment environment)
    {
        _environment = environment;
        IObject result = null;

        foreach (var statement in program.Statements)
        {
            result = statement.Accept(this);

            // Early return on return statements and errors
            if (result is ReturnValueObject returnValueObject)
                return returnValueObject.Value;
            if (result is ErrorObject)
                return result;
        }

        return result;
    }

    /// <summary>
    /// Evaluates a statement within the specified environment.
    /// </summary>
    /// <param name="statement">The statement to evaluate.</param>
    /// <param name="environment">The environment for variable bindings.</param>
    /// <returns>The resulting object from evaluation.</returns>
    private IObject Eval(Statement statement, Environment environment)
    {
        _environment = environment;
        return statement.Accept(this);
    }

    /// <summary>
    /// Evaluates an expression within the specified environment.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="environment">The environment for variable bindings.</param>
    /// <returns>The resulting object from evaluation.</returns>
    private IObject Eval(Expression expression, Environment environment)
    {
        _environment = environment;
        return expression.Accept(this);
    }

    #region IStatementVisitor<IObject> Implementation

    /// <summary>
    /// Visits and evaluates a let statement, binding a value to an identifier in the environment.
    /// </summary>
    /// <param name="letStatement">The let statement containing the identifier and value.</param>
    /// <returns>The evaluated value, or an error object if evaluation fails.</returns>
    IObject IStatementVisitor<IObject>.Visit(LetStatement letStatement)
    {
        var value = letStatement.Value.Accept(this);
        if (value is ErrorObject)
            return value;

        _environment.Set(letStatement.Name.Value, value);
        return value;
    }

    /// <summary>
    /// Visits and evaluates a return statement, wrapping the return value.
    /// </summary>
    /// <param name="returnStatement">The return statement containing the value to return.</param>
    /// <returns>A ReturnValueObject wrapping the evaluated value, or an error object.</returns>
    IObject IStatementVisitor<IObject>.Visit(ReturnStatement returnStatement)
    {
        var value = returnStatement.ReturnValue.Accept(this);
        if (value is ErrorObject)
            return value;

        return new ReturnValueObject(value);
    }

    /// <summary>
    /// Visits and evaluates an expression statement.
    /// </summary>
    /// <param name="expressionStatement">The expression statement to evaluate.</param>
    /// <returns>The result of evaluating the contained expression.</returns>
    IObject IStatementVisitor<IObject>.Visit(ExpressionStatement expressionStatement)
    {
        return expressionStatement.Expression.Accept(this);
    }

    /// <summary>
    /// Visits and evaluates a block statement by executing each statement in sequence.
    /// Stops execution early if a return statement or error is encountered.
    /// </summary>
    /// <param name="blockStatement">The block statement containing multiple statements.</param>
    /// <returns>The result of the last statement, or a return/error object if encountered.</returns>
    IObject IStatementVisitor<IObject>.Visit(BlockStatement blockStatement)
    {
        IObject result = null;

        foreach (var statement in blockStatement.Statements)
        {
            result = statement.Accept(this);

            // Stop execution on return or error
            if (result is ReturnValueObject or ErrorObject)
                return result;
        }

        return result;
    }

    #endregion

    #region IExpressionVisitor<IObject> Implementation

    /// <summary>
    /// Visits and evaluates an identifier by looking it up in the environment or builtins.
    /// </summary>
    /// <param name="identifier">The identifier to look up.</param>
    /// <returns>The value bound to the identifier, or an error if not found.</returns>
    IObject IExpressionVisitor<IObject>.Visit(Identifier identifier)
    {
        var (val, ok) = _environment.Get(identifier.Value);
        if (ok)
            return val;

        if (Builtins.TryGet(identifier.Value, out var builtinObject))
            return builtinObject;

        return new ErrorObject($"identifier not found: {identifier.Value}");
    }

    /// <summary>
    /// Visits and evaluates an integer literal.
    /// </summary>
    /// <param name="integerLiteral">The integer literal to evaluate.</param>
    /// <returns>An IntegerObject containing the literal's value.</returns>
    IObject IExpressionVisitor<IObject>.Visit(IntegerLiteral integerLiteral)
    {
        return new IntegerObject(integerLiteral.Value);
    }

    /// <summary>
    /// Visits and evaluates a boolean literal.
    /// </summary>
    /// <param name="booleanLiteral">The boolean literal to evaluate.</param>
    /// <returns>A singleton BooleanObject (True or False).</returns>
    IObject IExpressionVisitor<IObject>.Visit(BooleanLiteral booleanLiteral)
    {
        return booleanLiteral.Value ? BooleanObject.True : BooleanObject.False;
    }

    /// <summary>
    /// Visits and evaluates a string literal.
    /// </summary>
    /// <param name="stringLiteral">The string literal to evaluate.</param>
    /// <returns>A StringObject containing the literal's value.</returns>
    IObject IExpressionVisitor<IObject>.Visit(StringLiteral stringLiteral)
    {
        return new StringObject(stringLiteral.Value);
    }

    /// <summary>
    /// Visits and evaluates a prefix expression (e.g., -5, !true).
    /// </summary>
    /// <param name="prefixExpression">The prefix expression to evaluate.</param>
    /// <returns>The result of applying the prefix operator, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(PrefixExpression prefixExpression)
    {
        var right = prefixExpression.Right.Accept(this);
        if (right is ErrorObject)
            return right;

        return EvaluatePrefixExpression(prefixExpression.Operator, right);
    }

    /// <summary>
    /// Visits and evaluates an infix expression (e.g., 5 + 3, x == y).
    /// </summary>
    /// <param name="infixExpression">The infix expression to evaluate.</param>
    /// <returns>The result of applying the infix operator, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(InfixExpression infixExpression)
    {
        var left = infixExpression.Left.Accept(this);
        if (left is ErrorObject)
            return left;

        var right = infixExpression.Right.Accept(this);
        if (right is ErrorObject)
            return right;

        return EvaluateInfixExpression(left, infixExpression.Operator, right);
    }

    /// <summary>
    /// Visits and evaluates an if expression with optional else branch.
    /// </summary>
    /// <param name="ifExpression">The if expression containing condition, consequence, and optional alternative.</param>
    /// <returns>The result of the executed branch, or NullObject if no branch executes.</returns>
    IObject IExpressionVisitor<IObject>.Visit(IfExpression ifExpression)
    {
        var condition = ifExpression.Condition.Accept(this);
        if (condition is ErrorObject)
            return condition;

        if (IsTruthy(condition))
            return ifExpression.Consequence.Accept(this);

        if (ifExpression.Alternative != null)
            return ifExpression.Alternative.Accept(this);

        return NullObject.Null;
    }

    /// <summary>
    /// Visits and evaluates a function literal, creating a closure with the current environment.
    /// </summary>
    /// <param name="functionLiteral">The function literal containing parameters and body.</param>
    /// <returns>A FunctionObject representing the function closure.</returns>
    IObject IExpressionVisitor<IObject>.Visit(FunctionLiteral functionLiteral)
    {
        return new FunctionObject(functionLiteral.Parameters, functionLiteral.Body, _environment);
    }

    /// <summary>
    /// Visits and evaluates a function call expression.
    /// </summary>
    /// <param name="callExpression">The call expression containing the function and arguments.</param>
    /// <returns>The result of calling the function with the evaluated arguments, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(CallExpression callExpression)
    {
        var function = callExpression.Function.Accept(this);
        if (function is ErrorObject)
            return function;

        var args = EvaluateExpressions(callExpression.Arguments);
        if (args.Count == 1 && args[0] is ErrorObject)
            return args[0];

        return ApplyFunction(function, args);
    }

    /// <summary>
    /// Visits and evaluates an array literal.
    /// </summary>
    /// <param name="arrayLiteral">The array literal containing element expressions.</param>
    /// <returns>An ArrayObject containing the evaluated elements, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(ArrayLiteral arrayLiteral)
    {
        var elements = EvaluateExpressions(arrayLiteral.Elements);
        if (elements.Count == 1 && elements[0] is ErrorObject)
            return elements[0];

        return new ArrayObject(elements);
    }

    /// <summary>
    /// Visits and evaluates an index expression (e.g., array[0], hash[key]).
    /// </summary>
    /// <param name="indexExpression">The index expression containing the left side and index.</param>
    /// <returns>The value at the specified index, NullObject if not found, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(IndexExpression indexExpression)
    {
        var left = indexExpression.Left.Accept(this);
        if (left is ErrorObject)
            return left;

        var index = indexExpression.Index.Accept(this);
        if (index is ErrorObject)
            return index;

        return EvaluateIndexExpression(left, index);
    }

    /// <summary>
    /// Visits and evaluates a hash literal (dictionary/map).
    /// </summary>
    /// <param name="hashLiteral">The hash literal containing key-value pair expressions.</param>
    /// <returns>A HashObject containing the evaluated key-value pairs, or an error object.</returns>
    IObject IExpressionVisitor<IObject>.Visit(HashLiteral hashLiteral)
    {
        var pairs = new Dictionary<HashKey, (IHashableObject, IObject)>();

        foreach (var (keyExpr, valueExpr) in hashLiteral.Pairs)
        {
            var key = keyExpr.Accept(this);
            if (key is ErrorObject)
                return key;

            if (key is not IHashableObject hashableKey)
                return new ErrorObject($"unusable as hash key: {key.Type}");

            var value = valueExpr.Accept(this);
            if (value is ErrorObject)
                return value;

            var hashed = hashableKey.HashKey();
            pairs.Add(hashed, (hashableKey, value));
        }

        return new HashObject(pairs);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines if an object is truthy according to MonkeySharp semantics.
    /// Only null and false are considered falsy; everything else is truthy.
    /// </summary>
    /// <param name="obj">The object to test.</param>
    /// <returns>True if the object is truthy, false otherwise.</returns>
    private static bool IsTruthy(IObject obj)
    {
        if (obj == NullObject.Null)
            return false;
        if (obj == BooleanObject.False)
            return false;
        return true;
    }

    /// <summary>
    /// Evaluates a prefix operator expression.
    /// </summary>
    /// <param name="operator">The prefix operator (!, -).</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of applying the operator, or an error object.</returns>
    private static IObject EvaluatePrefixExpression(string @operator, IObject right)
    {
        return @operator switch
        {
            "!" => EvaluateBangOperator(right),
            "-" => EvaluateMinusPrefixOperator(right),
            _ => new ErrorObject($"unknown operator: {@operator}{right.Type}")
        };
    }

    /// <summary>
    /// Evaluates the bang (!) prefix operator for boolean negation.
    /// </summary>
    /// <param name="right">The operand to negate.</param>
    /// <returns>The boolean negation result.</returns>
    private static BooleanObject EvaluateBangOperator(IObject right)
    {
        if (right == BooleanObject.True)
            return BooleanObject.False;
        if (right == BooleanObject.False)
            return BooleanObject.True;
        if (right == NullObject.Null)
            return BooleanObject.True;
        return BooleanObject.False;
    }

    /// <summary>
    /// Evaluates the minus (-) prefix operator for numeric negation.
    /// </summary>
    /// <param name="right">The operand to negate.</param>
    /// <returns>The negated integer, or an error if the operand is not an integer.</returns>
    private static IObject EvaluateMinusPrefixOperator(IObject right)
    {
        if (right is not IntegerObject integer)
            return new ErrorObject($"unknown operator: -{right.Type}");

        return new IntegerObject(-integer.Value);
    }

    /// <summary>
    /// Evaluates an infix operator expression, delegating to type-specific evaluators.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="operator">The infix operator (+, -, *, /, ==, !=, &lt;, &gt;).</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of applying the operator, or an error object.</returns>
    private static IObject EvaluateInfixExpression(IObject left, string @operator, IObject right)
    {
        // Type-specific evaluation
        if (left is IntegerObject leftInteger && right is IntegerObject rightInteger)
            return EvaluateIntegerInfixExpression(leftInteger, @operator, rightInteger);

        if (left is BooleanObject leftBoolean && right is BooleanObject rightBoolean)
            return EvaluateBooleanInfixExpression(leftBoolean, @operator, rightBoolean);

        if (left is StringObject leftString && right is StringObject rightString)
            return EvaluateStringInfixExpression(leftString, @operator, rightString);

        // Type mismatch
        if (left.Type != right.Type)
            return new ErrorObject($"type mismatch: {left.Type} {@operator} {right.Type}");

        return new ErrorObject($"unknown operator: {left.Type} {@operator} {right.Type}");
    }

    /// <summary>
    /// Evaluates an infix operator expression on integer operands.
    /// </summary>
    /// <param name="left">The left integer operand.</param>
    /// <param name="operator">The operator to apply.</param>
    /// <param name="right">The right integer operand.</param>
    /// <returns>The result of the operation, or an error object.</returns>
    private static IObject EvaluateIntegerInfixExpression(IntegerObject left, string @operator, IntegerObject right)
    {
        return @operator switch
        {
            "+" => new IntegerObject(left.Value + right.Value),
            "-" => new IntegerObject(left.Value - right.Value),
            "*" => new IntegerObject(left.Value * right.Value),
            "/" => new IntegerObject(left.Value / right.Value),
            "<" => left.Value < right.Value ? BooleanObject.True : BooleanObject.False,
            ">" => left.Value > right.Value ? BooleanObject.True : BooleanObject.False,
            "==" => left.Value == right.Value ? BooleanObject.True : BooleanObject.False,
            "!=" => left.Value != right.Value ? BooleanObject.True : BooleanObject.False,
            _ => new ErrorObject($"unknown operator: INTEGER {@operator} INTEGER")
        };
    }

    /// <summary>
    /// Evaluates an infix operator expression on boolean operands.
    /// </summary>
    /// <param name="left">The left boolean operand.</param>
    /// <param name="operator">The operator to apply (==, !=).</param>
    /// <param name="right">The right boolean operand.</param>
    /// <returns>The result of the comparison, or an error object.</returns>
    private static IObject EvaluateBooleanInfixExpression(BooleanObject left, string @operator, BooleanObject right)
    {
        return @operator switch
        {
            "==" => left.Value == right.Value ? BooleanObject.True : BooleanObject.False,
            "!=" => left.Value != right.Value ? BooleanObject.True : BooleanObject.False,
            _ => new ErrorObject($"unknown operator: BOOLEAN {@operator} BOOLEAN")
        };
    }

    /// <summary>
    /// Evaluates an infix operator expression on string operands.
    /// </summary>
    /// <param name="left">The left string operand.</param>
    /// <param name="operator">The operator to apply (+, ==, !=).</param>
    /// <param name="right">The right string operand.</param>
    /// <returns>The result of the operation, or an error object.</returns>
    private static IObject EvaluateStringInfixExpression(StringObject left, string @operator, StringObject right)
    {
        return @operator switch
        {
            "+" => new StringObject(left.Value + right.Value),
            "==" => left.Value == right.Value ? BooleanObject.True : BooleanObject.False,
            "!=" => left.Value != right.Value ? BooleanObject.True : BooleanObject.False,
            _ => new ErrorObject($"unknown operator: STRING {@operator} STRING")
        };
    }

    /// <summary>
    /// Evaluates an index expression for arrays and hashes.
    /// </summary>
    /// <param name="left">The object being indexed (array or hash).</param>
    /// <param name="index">The index value.</param>
    /// <returns>The value at the index, NullObject if not found, or an error object.</returns>
    private static IObject EvaluateIndexExpression(IObject left, IObject index)
    {
        if (left is ArrayObject arrayObject && index is IntegerObject integerObject)
            return EvaluateArrayIndexExpression(arrayObject, integerObject);

        if (left is HashObject hashObject)
            return EvaluateHashIndexExpression(hashObject, index);

        return new ErrorObject($"index operator not supported: {left.Type}");
    }

    /// <summary>
    /// Evaluates an array index expression.
    /// </summary>
    /// <param name="arrayObject">The array being indexed.</param>
    /// <param name="index">The integer index.</param>
    /// <returns>The element at the index, or NullObject if out of bounds.</returns>
    private static IObject EvaluateArrayIndexExpression(ArrayObject arrayObject, IntegerObject index)
    {
        var max = arrayObject.Elements.Count - 1;
        if (index.Value < 0 || index.Value > max)
            return NullObject.Null;

        return arrayObject.Elements[(int) index.Value];
    }

    /// <summary>
    /// Evaluates a hash index expression.
    /// </summary>
    /// <param name="hashObject">The hash being indexed.</param>
    /// <param name="index">The key to look up.</param>
    /// <returns>The value associated with the key, NullObject if not found, or an error if the key is not hashable.</returns>
    private static IObject EvaluateHashIndexExpression(HashObject hashObject, IObject index)
    {
        if (index is not IHashableObject hashKey)
            return new ErrorObject($"unusable as hash key: {index.Type}");

        var hashed = hashKey.HashKey();
        if (!hashObject.Pairs.TryGetValue(hashed, out var pair))
            return NullObject.Null;

        return pair.Value;
    }

    /// <summary>
    /// Evaluates a list of expressions, returning early if any evaluation produces an error.
    /// </summary>
    /// <param name="expressions">The expressions to evaluate.</param>
    /// <returns>A list of evaluated objects, or a single-element list containing an error.</returns>
    private List<IObject> EvaluateExpressions(IReadOnlyList<Expression> expressions)
    {
        var result = new List<IObject>(expressions.Count);

        foreach (var expression in expressions)
        {
            var evaluated = expression.Accept(this);
            if (evaluated is ErrorObject)
                return [evaluated];

            result.Add(evaluated);
        }

        return result;
    }

    /// <summary>
    /// Applies a function to its arguments, handling both user-defined and builtin functions.
    /// For user-defined functions, creates a new environment and manages return value unwrapping.
    /// </summary>
    /// <param name="function">The function to call.</param>
    /// <param name="args">The evaluated arguments.</param>
    /// <returns>The result of the function call, or an error object.</returns>
    private IObject ApplyFunction(IObject function, List<IObject> args)
    {
        switch (function)
        {
            case FunctionObject functionObject:
            {
                var extendedEnv = ExtendFunctionEnvironment(functionObject, args);
                var previousEnv = _environment;
                _environment = extendedEnv;

                var evaluated = functionObject.Body.Accept(this);

                _environment = previousEnv;

                // Unwrap return values
                if (evaluated is ReturnValueObject returnValue)
                    return returnValue.Value;

                return evaluated;
            }
            case BuiltinObject builtinObject:
            {
                return builtinObject.Function(args);
            }
            default:
                return new ErrorObject($"not a function: {function.Type}");
        }
    }

    /// <summary>
    /// Creates a new environment for function execution by binding parameters to arguments.
    /// The new environment extends the function's closure environment.
    /// </summary>
    /// <param name="function">The function whose parameters to bind.</param>
    /// <param name="args">The argument values to bind.</param>
    /// <returns>A new environment with parameter bindings.</returns>
    private static Environment ExtendFunctionEnvironment(FunctionObject function, List<IObject> args)
    {
        var env = new Environment(function.Environment);

        for (var i = 0; i < function.Parameters.Count; i++)
            env.Set(function.Parameters[i].Value, args[i]);

        return env;
    }

    #endregion
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.AbstractSyntaxTree;

internal class Optimizer
    : IExpressionVisitor<(Expression?, bool)>,
        IStatementVisitor<(Statement?, bool)>
{
    private const int MaxOptimizationPasses = 100;

    // simple scoped constant propagation maps
    private readonly List<Dictionary<string, Expression>> _scopes = [];
    private readonly Dictionary<string, bool> _mutableVariables = new();

    public ProgramNode Optimize(ProgramNode programNode)
    {
        var currentProgram = programNode;

        for (var pass = 0; pass < MaxOptimizationPasses; pass++)
        {
            _scopes.Clear();
            _scopes.Add(new Dictionary<string, Expression>());

            var modified = false;
            var statements = new List<Statement>();
            foreach (var statement in currentProgram.Statements)
            {
                Debug.Assert(statement != null);
                var (newStatement, statementModified) = statement.Accept(this);
                Debug.Assert(newStatement != null);
                statements.Add(newStatement);
                modified |= statementModified;
            }

            var optimizedProgram = new ProgramNode(statements);

            if (!modified)
                return currentProgram;

            currentProgram = optimizedProgram;
        }

        return currentProgram;
    }

    private Dictionary<string, Expression> CurrentScope()
    {
        return _scopes.Last();
    }

    private Expression? TryResolveIdentifier(string name)
    {
        for (var i = _scopes.Count - 1; i >= 0; i--)
            if (_scopes[i].TryGetValue(name, out var expr))
                return expr;

        return null;
    }

    public (Expression, bool) Visit(Identifier identifier)
    {
        var resolved = TryResolveIdentifier(identifier.Value);
        if (resolved != null)
            return (resolved, true);

        return (identifier, false);
    }

    public (Expression, bool) Visit(IntegerLiteral integerLiteral)
    {
        return (integerLiteral, false);
    }

    public (Expression, bool) Visit(PrefixExpression prefixExpression)
    {
        var modified = false;

        var (right, rightModified) = prefixExpression.Right.Accept(this);
        modified |= rightModified;

        if (prefixExpression.Operator == "!")
        {
            if (right is PrefixExpression { Operator: "!", Right: BooleanLiteral } innerPrefix1)
                return (innerPrefix1.Right, true);

            if (right is BooleanLiteral booleanLiteral)
                return (booleanLiteral.Value ? BooleanLiteral.False : BooleanLiteral.True, true);

            if (right is InfixExpression ie && (ie.Operator == "==" || ie.Operator == "!="))
            {
                var flipped = ie.Operator == "==" ? "!=" : "==";
                return (new InfixExpression(ie.Token, ie.Left, flipped, ie.Right), true);
            }
        }

        if (prefixExpression.Operator == "-")
        {
            if (right is IntegerLiteral integerLiteral)
            {
                var value = -integerLiteral.Value;
                return (new IntegerLiteral(new Token(TokenType.Integer, $"{value}"), value), true);
            }

            if (right is PrefixExpression { Operator: "-", Right: IntegerLiteral } innerPrefix2)
                return (innerPrefix2.Right, true);
        }

        if (modified)
            return (
                new PrefixExpression(prefixExpression.Token, prefixExpression.Operator, right!),
                true
            );

        return (prefixExpression, false);
    }

    public (Expression, bool) Visit(InfixExpression infixExpression)
    {
        var modified = false;
        var (left, leftModified) = infixExpression.Left.Accept(this);
        modified |= leftModified;
        var (right, rightModified) = infixExpression.Right.Accept(this);
        modified |= rightModified;

        switch (infixExpression.Operator)
        {
            case "==":
            {
                if (
                    left is BooleanLiteral leftBooleanLiteral
                    && right is BooleanLiteral rightBooleanLiteral
                )
                    return (
                        leftBooleanLiteral.Value == rightBooleanLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                    return (
                        leftIntegerLiteral.Value == rightIntegerLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (
                    left is StringLiteral leftStringLiteral
                    && right is StringLiteral rightStringLiteral
                )
                    return (
                        leftStringLiteral.Value == rightStringLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (left is Identifier li && right is Identifier ri && li.Value == ri.Value)
                    return (BooleanLiteral.True, true);

                break;
            }
            case "!=":
            {
                if (
                    left is BooleanLiteral leftBooleanLiteral
                    && right is BooleanLiteral rightBooleanLiteral
                )
                    return (
                        leftBooleanLiteral.Value != rightBooleanLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                    return (
                        leftIntegerLiteral.Value != rightIntegerLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (
                    left is StringLiteral leftStringLiteral
                    && right is StringLiteral rightStringLiteral
                )
                    return (
                        leftStringLiteral.Value != rightStringLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );

                if (left is Identifier li && right is Identifier ri && li.Value == ri.Value)
                    return (BooleanLiteral.False, true);

                break;
            }
            case "+":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                {
                    if (leftIntegerLiteral.Value == 0)
                        return (right, true);
                    if (rightIntegerLiteral.Value == 0)
                        return (left, true);

                    return (
                        new IntegerLiteral(
                            new Token(
                                TokenType.Integer,
                                $"{leftIntegerLiteral.Value + rightIntegerLiteral.Value}"
                            ),
                            leftIntegerLiteral.Value + rightIntegerLiteral.Value
                        ),
                        true
                    );
                }

                if (
                    left is StringLiteral leftStringLiteral
                    && right is StringLiteral rightStringLiteral
                )
                {
                    if (leftStringLiteral.Value == "")
                        return (right, true);
                    if (rightStringLiteral.Value == "")
                        return (left, true);

                    return (
                        new StringLiteral(
                            new Token(
                                TokenType.String,
                                $"{leftStringLiteral.Value + rightStringLiteral.Value}"
                            ),
                            leftStringLiteral.Value + rightStringLiteral.Value
                        ),
                        true
                    );
                }

                break;
            }
            case "-":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                {
                    if (leftIntegerLiteral.Value == rightIntegerLiteral.Value)
                        return (new IntegerLiteral(new Token(TokenType.Integer, "0"), 0), true);

                    if (rightIntegerLiteral.Value == 0)
                        return (left, true);

                    if (leftIntegerLiteral.Value == 0)
                        return (new PrefixExpression(infixExpression.Token, "-", right), true);
                    return (
                        new IntegerLiteral(
                            new Token(
                                TokenType.Integer,
                                $"{leftIntegerLiteral.Value - rightIntegerLiteral.Value}"
                            ),
                            leftIntegerLiteral.Value - rightIntegerLiteral.Value
                        ),
                        true
                    );
                }

                break;
            }
            case "*":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                {
                    if (leftIntegerLiteral.Value == 0 || rightIntegerLiteral.Value == 0)
                        return (new IntegerLiteral(new Token(TokenType.Integer, "0"), 0), true);

                    if (leftIntegerLiteral.Value == 1)
                        return (right, true);
                    if (rightIntegerLiteral.Value == 1)
                        return (left, true);

                    return (
                        new IntegerLiteral(
                            new Token(
                                TokenType.Integer,
                                $"{leftIntegerLiteral.Value * rightIntegerLiteral.Value}"
                            ),
                            leftIntegerLiteral.Value * rightIntegerLiteral.Value
                        ),
                        true
                    );
                }

                break;
            }
            case "/":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                    && rightIntegerLiteral.Value != 0
                )
                {
                    if (leftIntegerLiteral.Value == 0)
                        return (new IntegerLiteral(new Token(TokenType.Integer, "0"), 0), true);
                    if (rightIntegerLiteral.Value == 1)
                        return (left, true);

                    return (
                        new IntegerLiteral(
                            new Token(
                                TokenType.Integer,
                                $"{leftIntegerLiteral.Value / rightIntegerLiteral.Value}"
                            ),
                            leftIntegerLiteral.Value / rightIntegerLiteral.Value
                        ),
                        true
                    );
                }

                break;
            }
            case ">":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                    return (
                        leftIntegerLiteral.Value > rightIntegerLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );
                break;
            }
            case "<":
            {
                if (
                    left is IntegerLiteral leftIntegerLiteral
                    && right is IntegerLiteral rightIntegerLiteral
                )
                    return (
                        leftIntegerLiteral.Value < rightIntegerLiteral.Value
                            ? BooleanLiteral.True
                            : BooleanLiteral.False,
                        true
                    );
                break;
            }
        }

        if (modified)
            return (
                new InfixExpression(infixExpression.Token, left!, infixExpression.Operator, right!),
                true
            );

        return (infixExpression, false);
    }

    public (Expression, bool) Visit(BooleanLiteral booleanLiteral)
    {
        return (booleanLiteral, false);
    }

    public (Expression, bool) Visit(IfExpression ifExpression)
    {
        var modified = false;
        var (condition, conditionModified) = ifExpression.Condition.Accept(this);
        modified |= conditionModified;
        var (consequence, consequenceModified) = ifExpression.Consequence.Accept(this);
        modified |= consequenceModified;
        var (alternative, alternativeModified) =
            ifExpression.Alternative?.Accept(this) ?? (null, false);
        modified |= alternativeModified;

        // If condition is a boolean literal, we can simplify an IfExpression:
        if (condition is BooleanLiteral condBool)
        {
            if (condBool.Value)
            {
                // true -> use consequence (expression context might require further handling outside this optimizer)
                // return original IfExpression with simplified condition/consequence for safety, but if consequence is a single expression statement, we can return that expression
                if (
                    consequence is BlockStatement bs
                    && bs.Statements.Count == 1
                    && bs.Statements[0] is ExpressionStatement es
                )
                    return (es.Expression, true);

                // otherwise keep the if with simplified parts
            }
            else
            {
                // false -> use alternative if present
                if (alternative is BlockStatement abs)
                    if (abs.Statements.Count == 1 && abs.Statements[0] is ExpressionStatement aes)
                        return (aes.Expression, true);
                // if no alternative, this if evaluates to null in runtime; keep as-is for safety
            }
        }

        if (modified)
            return (
                new IfExpression(
                    ifExpression.Token,
                    condition!,
                    (BlockStatement)consequence!,
                    (BlockStatement?)alternative
                ),
                true
            );

        return (ifExpression, false);
    }

    public (Expression, bool) Visit(FunctionLiteral functionLiteral)
    {
        var modified = false;

        // new function scope
        _scopes.Add(new Dictionary<string, Expression>());

        // mark parameters as non-constant by creating entries that won't be filled
        // (we simply don't add any constant mapping for them)

        var (body, bodyModified) = functionLiteral.Body.Accept(this);
        modified |= bodyModified;

        _scopes.RemoveAt(_scopes.Count - 1);

        if (modified)
            return (
                new FunctionLiteral(
                    functionLiteral.Token,
                    functionLiteral.Parameters,
                    (BlockStatement)body!
                )
                {
                    Name = functionLiteral.Name,
                },
                true
            );

        return (functionLiteral, false);
    }

    public (Expression, bool) Visit(CallExpression callExpression)
    {
        var modified = false;

        var (function, functionModified) = callExpression.Function.Accept(this);
        modified |= functionModified;

        var arguments = new List<Expression>();
        foreach (var argument in callExpression.Arguments)
        {
            var (argumentExpression, argumentModified) = argument.Accept(this);
            modified |= argumentModified;
            arguments.Add(argumentExpression!);
        }

        if (modified)
            return (new CallExpression(callExpression.Token, function!, arguments), true);

        return (callExpression, false);
    }

    public (Expression, bool) Visit(StringLiteral stringLiteral)
    {
        return (stringLiteral, false);
    }

    public (Expression, bool) Visit(ArrayLiteral arrayLiteral)
    {
        var modified = false;

        var elements = new List<Expression>();
        foreach (var element in arrayLiteral.Elements)
        {
            var (elementExpression, elementModified) = element.Accept(this);
            modified |= elementModified;
            elements.Add(elementExpression!);
        }

        if (modified)
            return (new ArrayLiteral(arrayLiteral.Token, elements), true);

        return (arrayLiteral, false);
    }

    public (Expression, bool) Visit(IndexExpression indexExpression)
    {
        var modified = false;

        var (left, leftModified) = indexExpression.Left.Accept(this);
        modified |= leftModified;

        var (index, indexModified) = indexExpression.Index.Accept(this);
        modified |= indexModified;

        if (left is ArrayLiteral arrayLit && index is IntegerLiteral indexLit)
        {
            var idx = (int)indexLit.Value;
            if (idx >= 0 && idx < arrayLit.Elements.Count)
                return (arrayLit.Elements[idx], true);
        }

        if (modified)
            return (new IndexExpression(indexExpression.Token, left!, index!), true);

        return (indexExpression, false);
    }

    public (Expression, bool) Visit(HashLiteral hashLiteral)
    {
        var modified = false;

        var pairs = new Dictionary<Expression, Expression>();
        foreach (var (key, value) in hashLiteral.Pairs)
        {
            var (keyExpression, keyModified) = key.Accept(this);
            modified |= keyModified;
            var (valueExpression, valueModified) = value.Accept(this);
            modified |= valueModified;

            pairs.Add(keyExpression!, valueExpression!);
        }

        if (modified)
            return (new HashLiteral(hashLiteral.Token, pairs), true);

        return (hashLiteral, false);
    }

    public (Statement, bool) Visit(LetStatement letStatement)
    {
        var modified = false;
        var (value, valueModified) = letStatement.Value.Accept(this);
        modified |= valueModified;

        var varName = letStatement.Name.Value;

        // Check if variable was already defined (reassignment)
        if (_mutableVariables.ContainsKey(varName))
        {
            _mutableVariables[varName] = true; // Mark as mutated
            // Remove from all scopes since it's no longer constant
            foreach (var scope in _scopes)
                scope.Remove(varName);
        }
        else
        {
            _mutableVariables[varName] = false;
        }

        // Only propagate if immutable and literal
        if (
            !_mutableVariables[varName]
            && value is IntegerLiteral or BooleanLiteral or StringLiteral
        )
        {
            var current = CurrentScope();
            current[varName] = value;
        }

        // If value is a simple literal, record it in the current scope for propagation
        if (value is IntegerLiteral or BooleanLiteral or StringLiteral)
        {
            var current = CurrentScope();
            current[letStatement.Name.Value] = value;
            // Note: adding mapping is considered a modification only if identifier uses will be replaced later
        }

        if (value is FunctionLiteral functionLiteral)
            if (!string.Equals(functionLiteral.Name, letStatement.Name.Value))
            {
                functionLiteral.Name = letStatement.Name.Value;
                modified = true;
            }

        if (modified)
            return (new LetStatement(letStatement.Token, letStatement.Name, value!), true);

        return (letStatement, false);
    }

    public (Statement, bool) Visit(ReturnStatement returnStatement)
    {
        var modified = false;

        var (returnValue, returnValueModified) = returnStatement.ReturnValue.Accept(this);
        modified |= returnValueModified;

        if (returnValue is null)
            throw new InvalidOperationException("Optimizer produced a null return expression.");

        if (modified)
            return (new ReturnStatement(returnStatement.Token, returnValue), true);

        return (returnStatement, false);
    }

    public (Statement, bool) Visit(ExpressionStatement expressionStatement)
    {
        var modified = false;

        var (expression, expressionModified) = expressionStatement.Expression.Accept(this);
        modified |= expressionModified;

        if (modified)
            return (new ExpressionStatement(expressionStatement.Token, expression!), true);

        return (expressionStatement, false);
    }

    public (Statement, bool) Visit(BlockStatement blockStatement)
    {
        var modified = false;

        // new lexical scope
        _scopes.Add(new Dictionary<string, Expression>());

        var statements = new List<Statement>();
        var sawReturn = false;
        foreach (var statement in blockStatement.Statements)
        {
            if (sawReturn)
            {
                // dead code after return -- skip
                modified = true;
                continue;
            }

            var (newStatement, statementModified) = statement.Accept(this);
            modified |= statementModified;

            // NEW: Check for if-expression with constant false condition
            if (newStatement is ExpressionStatement { Expression: IfExpression ifExpr })
                if (
                    ifExpr.Condition is BooleanLiteral { Value: false }
                    && ifExpr.Alternative == null
                )
                {
                    // Skip this statement entirely - it will never execute
                    modified = true;
                    continue;
                }

            statements.Add(newStatement!);

            if (newStatement is ReturnStatement)
                sawReturn = true;
        }

        _scopes.RemoveAt(_scopes.Count - 1);

        if (modified)
            return (new BlockStatement(blockStatement.Token, statements), true);

        return (blockStatement, false);
    }
}

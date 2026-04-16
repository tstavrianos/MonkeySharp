using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree;

/// <summary>
/// Performs code quality analysis to detect unused variables, dead code, and other quality issues.
/// </summary>
public class CodeQualityAnalyzer
{
    private class VariableInfo
    {
        public bool IsRead { get; set; }

        public bool IsParameter { get; init; }
    }

    private class ScopeInfo
    {
        public Dictionary<string, VariableInfo> Variables { get; } = new();
        public bool HasReturned { get; set; }
    }

    private readonly List<string> _warnings = [];
    private readonly Stack<ScopeInfo> _scopes = new();
    private bool _hasUnreachableCode;
    private bool _afterReturn;

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Analyzes the given AST node for code quality issues.
    /// </summary>
    /// <param name="node">The AST node to analyze</param>
    /// <returns>True if analysis succeeded</returns>
    public bool Analyze(Node node)
    {
        _warnings.Clear();
        _scopes.Clear();
        _hasUnreachableCode = false;
        _afterReturn = false;

        EnterScope();
        AnalyzeNode(node);
        LeaveScope();

        return true;
    }

    private void AnalyzeNode(Node node)
    {
        switch (node)
        {
            case ProgramNode program:
                foreach (var statement in program.Statements)
                    AnalyzeNode(statement);
                break;

            case BlockStatement blockStatement:
                AnalyzeBlockStatement(blockStatement);
                break;

            case ExpressionStatement expressionStatement:
                if (_afterReturn && !_hasUnreachableCode)
                {
                    AddWarning("Unreachable code detected after return statement");
                    _hasUnreachableCode = true;
                }

                AnalyzeNode(expressionStatement.Expression);
                break;

            case LetStatement letStatement:
                if (_afterReturn && !_hasUnreachableCode)
                {
                    AddWarning("Unreachable code detected after return statement");
                    _hasUnreachableCode = true;
                }

                AnalyzeLetStatement(letStatement);
                break;

            case ReturnStatement returnStatement:
                if (_afterReturn && !_hasUnreachableCode)
                {
                    AddWarning("Unreachable return statement");
                    _hasUnreachableCode = true;
                }

                _afterReturn = true;
                CurrentScope().HasReturned = true;
                AnalyzeNode(returnStatement.ReturnValue);
                break;

            case IfExpression ifExpression:
                AnalyzeIfExpression(ifExpression);
                break;

            case FunctionLiteral functionLiteral:
                AnalyzeFunctionLiteral(functionLiteral);
                break;

            case CallExpression callExpression:
                AnalyzeCallExpression(callExpression);
                break;

            case Identifier identifier:
                MarkVariableAsRead(identifier.Value);
                break;

            case InfixExpression infixExpression:
                AnalyzeNode(infixExpression.Left);
                AnalyzeNode(infixExpression.Right);
                break;

            case PrefixExpression prefixExpression:
                AnalyzeNode(prefixExpression.Right);
                break;

            case IndexExpression indexExpression:
                AnalyzeNode(indexExpression.Left);
                AnalyzeNode(indexExpression.Index);
                break;

            case ArrayLiteral arrayLiteral:
                foreach (var element in arrayLiteral.Elements)
                    AnalyzeNode(element);
                break;

            case HashLiteral hashLiteral:
                foreach (var (key, value) in hashLiteral.Pairs)
                {
                    AnalyzeNode(key);
                    AnalyzeNode(value);
                }

                break;

            // Literals don't need analysis
            case IntegerLiteral:
            case BooleanLiteral:
            case StringLiteral:
                break;
        }
    }

    private void AnalyzeBlockStatement(BlockStatement blockStatement)
    {
        var previousAfterReturn = _afterReturn;
        _afterReturn = false;

        foreach (var statement in blockStatement.Statements)
            AnalyzeNode(statement);

        _afterReturn = previousAfterReturn || CurrentScope().HasReturned;
    }

    private void AnalyzeLetStatement(LetStatement letStatement)
    {
        // First analyze the value (right side)
        AnalyzeNode(letStatement.Value);

        // Then define the variable
        var currentScope = CurrentScope();
        var varName = letStatement.Name.Value;

        if (currentScope.Variables.ContainsKey(varName))
        {
            // Variable already exists in scope (shadowing - handled by SemanticAnalyzer)
        }

        currentScope.Variables[varName] = new VariableInfo
        {
            //Name = varName,
            //IsWritten = true,
            IsRead = false,
            IsParameter = false,
        };
    }

    private void AnalyzeIfExpression(IfExpression ifExpression)
    {
        AnalyzeNode(ifExpression.Condition);

        // Check for constant conditions
        if (ifExpression.Condition is BooleanLiteral boolLiteral)
        {
            if (boolLiteral.Value)
            {
                AddWarning("If condition is always true; else branch is dead code");
                AnalyzeNode(ifExpression.Consequence);
            }
            else
            {
                AddWarning("If condition is always false; then branch is dead code");
                if (ifExpression.Alternative != null)
                    AnalyzeNode(ifExpression.Alternative);
            }

            return;
        }

        var beforeReturn = _afterReturn;
        _afterReturn = false;

        AnalyzeNode(ifExpression.Consequence);
        var consequenceReturns = _afterReturn;

        _afterReturn = false;

        if (ifExpression.Alternative != null)
            AnalyzeNode(ifExpression.Alternative);

        var alternativeReturns = _afterReturn;

        // Only set after return if both branches return
        _afterReturn =
            beforeReturn
            || (consequenceReturns && alternativeReturns && ifExpression.Alternative != null);
    }

    private void AnalyzeFunctionLiteral(FunctionLiteral functionLiteral)
    {
        EnterScope();
        var previousAfterReturn = _afterReturn;
        var previousUnreachable = _hasUnreachableCode;
        _afterReturn = false;
        _hasUnreachableCode = false;

        // Add parameters to scope
        foreach (var param in functionLiteral.Parameters)
            CurrentScope().Variables[param.Value] = new VariableInfo
            {
                IsRead = false,
                IsParameter = true,
            };

        AnalyzeNode(functionLiteral.Body);

        // Check for unused variables and parameters
        var scope = CurrentScope();
        foreach (var (name, info) in scope.Variables)
            if (!info.IsRead)
            {
                if (info.IsParameter)
                    AddWarning($"Parameter '{name}' is never used");
                else
                    AddWarning($"Variable '{name}' is assigned but never used");
            }

        LeaveScope();
        _afterReturn = previousAfterReturn;
        _hasUnreachableCode = previousUnreachable;
    }

    private void AnalyzeCallExpression(CallExpression callExpression)
    {
        AnalyzeNode(callExpression.Function);

        foreach (var arg in callExpression.Arguments)
            AnalyzeNode(arg);
    }

    private void MarkVariableAsRead(string name)
    {
        // Search from innermost to outermost scope
        foreach (var scope in _scopes)
            if (scope.Variables.TryGetValue(name, out var varInfo))
            {
                varInfo.IsRead = true;
                return;
            }
        // Variable not found in any scope (will be caught by SemanticAnalyzer)
    }

    private ScopeInfo CurrentScope()
    {
        return _scopes.Peek();
    }

    private void EnterScope()
    {
        _scopes.Push(new ScopeInfo());
    }

    private void LeaveScope()
    {
        _scopes.Pop();
    }

    private void AddWarning(string message)
    {
        _warnings.Add($"Code Quality: {message}");
    }
}

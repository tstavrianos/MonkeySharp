using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree.Analyzers;

/// <summary>
/// Performs security analysis on an AST to detect potential security issues.
/// </summary>
internal class SecurityAnalyzer
{
    private class FunctionInfo
    {
        public required string Name { get; set; }
        public bool HasBaseCase { get; set; }
        public bool CallsItself { get; set; }
        public List<string> CallsOthers { get; } = [];
    }

    private readonly List<string> _warnings = [];
    private readonly Dictionary<string, FunctionInfo> _functions = new();
    private FunctionInfo? _currentFunction;

    // Tracks whether the current execution path makes a self-call.
    // Used per-branch in if expressions to detect implicit base cases.
    private bool _currentPathCallsItself;

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Analyzes the given AST node for security issues.
    /// </summary>
    /// <param name="node">The AST node to analyze</param>
    /// <returns>True if analysis completed, false if critical issues found</returns>
    public bool Analyze(Node node)
    {
        _warnings.Clear();
        _functions.Clear();
        _currentFunction = null;
        _currentPathCallsItself = false;

        // First pass: collect all function definitions
        CollectFunctions(node);

        // Second pass: analyze for infinite loops and recursion
        AnalyzeNode(node);

        // Third pass: detect mutual recursion without base cases
        DetectMutualRecursion();

        return true;
    }

    private void CollectFunctions(Node node)
    {
        switch (node)
        {
            case ProgramNode program:
                foreach (var statement in program.Statements)
                    CollectFunctions(statement);
                break;

            case LetStatement { Value: FunctionLiteral } letStatement:
                var funcInfo = new FunctionInfo { Name = letStatement.Name.Value };
                _functions[letStatement.Name.Value] = funcInfo;
                break;

            case BlockStatement blockStatement:
                foreach (var statement in blockStatement.Statements)
                    CollectFunctions(statement);
                break;

            case IfExpression ifExpression:
                CollectFunctions(ifExpression.Consequence);
                if (ifExpression.Alternative != null)
                    CollectFunctions(ifExpression.Alternative);
                break;
        }
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
                AnalyzeNode(expressionStatement.Expression);
                break;

            case LetStatement letStatement:
                AnalyzeLetStatement(letStatement);
                break;

            case ReturnStatement returnStatement:
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

            // Literals and identifiers don't need analysis
            case IntegerLiteral:
            case BooleanLiteral:
            case StringLiteral:
            case Identifier:
                break;
        }
    }

    private void AnalyzeBlockStatement(BlockStatement blockStatement)
    {
        foreach (var statement in blockStatement.Statements)
        {
            AnalyzeNode(statement);

            // Dead code after return is handled by CodeQualityAnalyzer
        }
    }

    private void AnalyzeLetStatement(LetStatement letStatement)
    {
        AnalyzeNode(letStatement.Value);
    }

    private void AnalyzeIfExpression(IfExpression ifExpression)
    {
        AnalyzeNode(ifExpression.Condition);

        var prevCallsItself = _currentPathCallsItself;

        // Analyze consequence branch
        _currentPathCallsItself = false;
        AnalyzeNode(ifExpression.Consequence);
        var consequenceRecurses = _currentPathCallsItself;

        // Analyze alternative branch (or treat missing else as non-recursive path)
        _currentPathCallsItself = false;
        if (ifExpression.Alternative != null)
            AnalyzeNode(ifExpression.Alternative);
        var alternativeRecurses = _currentPathCallsItself;
        var hasAlternative = ifExpression.Alternative != null;

        // A branch that does NOT self-call is an implicit base case
        if (_currentFunction != null)
        {
            if (!consequenceRecurses)
                _currentFunction.HasBaseCase = true;
            if (hasAlternative && !alternativeRecurses)
                _currentFunction.HasBaseCase = true;
            // No else branch means control falls through — also a base case path
            if (!hasAlternative)
                _currentFunction.HasBaseCase = true;
        }

        // The if expression recurses only when every possible branch recurses
        _currentPathCallsItself =
            prevCallsItself
            || (hasAlternative ? consequenceRecurses && alternativeRecurses : consequenceRecurses);
    }

    private void AnalyzeFunctionLiteral(FunctionLiteral functionLiteral)
    {
        var previousFunction = _currentFunction;
        var previousCallsItself = _currentPathCallsItself;

        // Find the function info if this is a named function
        if (!string.IsNullOrEmpty(functionLiteral.Name))
            if (_functions.TryGetValue(functionLiteral.Name, out var funcInfo))
                _currentFunction = funcInfo;

        _currentPathCallsItself = false;
        AnalyzeNode(functionLiteral.Body);

        // Warn about unbounded recursion — HasBaseCase is set incrementally during if-branch analysis
        if (_currentFunction != null)
        {
            if (_currentFunction.CallsItself && !_currentFunction.HasBaseCase)
                AddWarning(
                    $"Function '{_currentFunction.Name}' contains recursion but no detectable base case. "
                        + "This may lead to infinite recursion and stack overflow."
                );
        }

        _currentFunction = previousFunction;
        _currentPathCallsItself = previousCallsItself;
    }

    private void AnalyzeCallExpression(CallExpression callExpression)
    {
        // Check if this is a recursive call
        if (callExpression.Function is Identifier identifier && _currentFunction != null)
        {
            if (identifier.Value == _currentFunction.Name)
            {
                _currentFunction.CallsItself = true;
                _currentPathCallsItself = true;
            }
            else if (_functions.ContainsKey(identifier.Value))
                _currentFunction.CallsOthers.Add(identifier.Value);
        }

        AnalyzeNode(callExpression.Function);
        foreach (var arg in callExpression.Arguments)
            AnalyzeNode(arg);
    }

    private void DetectMutualRecursion()
    {
        // Detect cycles in function call graph that have no base cases
        foreach (var (funcName, funcInfo) in _functions)
        {
            if (funcInfo.CallsItself && !funcInfo.HasBaseCase)
                // Already warned about direct recursion
                continue;

            // Check for mutual recursion
            var visited = new HashSet<string>();
            var path = new List<string>();

            if (HasRecursiveCycle(funcName, visited, path) && !AnyHasBaseCase(path))
            {
                var cycle = string.Join(" -> ", path);
                AddWarning(
                    $"Potential infinite mutual recursion detected: {cycle}. "
                        + "None of these functions have a detectable base case."
                );
            }
        }
    }

    private bool HasRecursiveCycle(string funcName, HashSet<string> visited, List<string> path)
    {
        if (path.Contains(funcName))
            return true; // Found a cycle

        if (!visited.Add(funcName))
            return false; // Already checked this path

        path.Add(funcName);

        if (_functions.TryGetValue(funcName, out var funcInfo))
            foreach (var calledFunc in funcInfo.CallsOthers)
                if (HasRecursiveCycle(calledFunc, visited, path))
                    return true;

        path.Remove(funcName);
        return false;
    }

    private bool AnyHasBaseCase(List<string> functionNames)
    {
        return functionNames.Any(name =>
            _functions.TryGetValue(name, out var info) && info.HasBaseCase
        );
    }

    private void AddWarning(string message)
    {
        _warnings.Add($"Security: {message}");
    }
}

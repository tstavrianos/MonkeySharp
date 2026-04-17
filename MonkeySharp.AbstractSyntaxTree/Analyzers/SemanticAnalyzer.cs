using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree.Analyzers;

/// <summary>
/// Performs static semantic analysis on an AST to detect errors before evaluation.
/// </summary>
internal class SemanticAnalyzer
{
    /// <summary>
    /// Represents a scope during semantic analysis.
    /// </summary>
    private class AnalysisScope
    {
        private readonly Dictionary<string, SymbolKind> _symbols = new();

        public void Define(string name, SymbolKind kind)
        {
            _symbols[name] = kind;
        }

        public bool IsDefined(string name)
        {
            return _symbols.ContainsKey(name);
        }

        public bool TryResolve(string name, out SymbolKind symbolKind)
        {
            return _symbols.TryGetValue(name, out symbolKind);
        }
    }

    private enum SymbolKind
    {
        Variable,
        Function,
        Parameter,
        Builtin,
    }

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly Stack<AnalysisScope> _scopes = new();
    private bool _isInFunction;
    private readonly Dictionary<string, int> _builtinArity = new();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Analyzes the given AST node for semantic errors.
    /// </summary>
    /// <param name="node">The AST node to analyze</param>
    /// <param name="builtinNamesAndArguments">Names and argument count of the built-in functions</param>
    /// <returns>True if analysis succeeded without errors, false otherwise</returns>
    public bool Analyze(Node node, IEnumerable<(string, int)>? builtinNamesAndArguments = null)
    {
        _errors.Clear();
        _warnings.Clear();
        _scopes.Clear();
        _builtinArity.Clear();
        _isInFunction = false;

        // Create global scope
        EnterScope();

        // Add built-in functions to global scope
        foreach (var (builtinName, builtinArity) in builtinNamesAndArguments ?? [])
        {
            _scopes.Peek().Define(builtinName, SymbolKind.Builtin);
            _builtinArity.Add(builtinName, builtinArity);
        }

        AnalyzeNode(node);
        LeaveScope();

        return _errors.Count == 0;
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
                foreach (var statement in blockStatement.Statements)
                    AnalyzeNode(statement);
                break;

            case ExpressionStatement expressionStatement:
                AnalyzeNode(expressionStatement.Expression);
                break;

            case LetStatement letStatement:
                AnalyzeLetStatement(letStatement);
                break;

            case ReturnStatement returnStatement:
                AnalyzeReturnStatement(returnStatement);
                break;

            case IfExpression ifExpression:
                AnalyzeNode(ifExpression.Condition);
                AnalyzeNode(ifExpression.Consequence);
                if (ifExpression.Alternative != null)
                    AnalyzeNode(ifExpression.Alternative);
                break;

            case FunctionLiteral functionLiteral:
                AnalyzeFunctionLiteral(functionLiteral);
                break;

            case CallExpression callExpression:
                AnalyzeCallExpression(callExpression);
                break;

            case Identifier identifier:
                AnalyzeIdentifier(identifier);
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

    private void AnalyzeLetStatement(LetStatement letStatement)
    {
        // Analyze the value expression first
        AnalyzeNode(letStatement.Value);

        // Check if variable is already defined in current scope
        var currentScope = _scopes.Peek();
        if (currentScope.IsDefined(letStatement.Name.Value))
            AddWarning($"Variable '{letStatement.Name.Value}' shadows existing definition");

        // Define the variable
        currentScope.Define(letStatement.Name.Value, SymbolKind.Variable);
    }

    private void AnalyzeReturnStatement(ReturnStatement returnStatement)
    {
        if (!_isInFunction)
            AddWarning("Return statement outside of function");

        AnalyzeNode(returnStatement.ReturnValue);
    }

    private void AnalyzeFunctionLiteral(FunctionLiteral functionLiteral)
    {
        EnterScope();
        var wasInFunction = _isInFunction;
        _isInFunction = true;

        // Add function name to its own scope if named
        if (!string.IsNullOrEmpty(functionLiteral.Name))
            _scopes.Peek().Define(functionLiteral.Name, SymbolKind.Function);

        // Define parameters
        var parameterNames = new HashSet<string>();
        foreach (var param in functionLiteral.Parameters)
        {
            if (!parameterNames.Add(param.Value))
                AddError($"Duplicate parameter name '{param.Value}' in function");
            _scopes.Peek().Define(param.Value, SymbolKind.Parameter);
        }

        // Analyze function body
        AnalyzeNode(functionLiteral.Body);

        _isInFunction = wasInFunction;
        LeaveScope();
    }

    private void AnalyzeCallExpression(CallExpression callExpression)
    {
        // Analyze the function expression
        AnalyzeNode(callExpression.Function);

        // Check if calling an identifier that's known to be a function
        if (callExpression.Function is Identifier identifier)
        {
            var symbolKind = Resolve(identifier.Value);
            if (symbolKind is SymbolKind.Variable)
                AddWarning($"Variable '{identifier.Value}' is being called as a function");
        }

        // Analyze arguments
        foreach (var arg in callExpression.Arguments)
            AnalyzeNode(arg);

        // Could add arity checking for known built-ins
        if (callExpression.Function is Identifier builtinIdentifier)
            CheckBuiltinArity(builtinIdentifier.Value, callExpression.Arguments.Count);
    }

    private void AnalyzeIdentifier(Identifier identifier)
    {
        var symbol = Resolve(identifier.Value);
        if (symbol == null)
            AddError($"Undefined identifier '{identifier.Value}'");
    }

    private void CheckBuiltinArity(string name, int argCount)
    {
        var expectedArity = _builtinArity.GetValueOrDefault(name, -1);

        if (expectedArity >= 0 && argCount != expectedArity)
            AddError($"Function '{name}' expects {expectedArity} argument(s), but got {argCount}");
    }

    private SymbolKind? Resolve(string name)
    {
        // Search from innermost to outermost scope
        foreach (var scope in _scopes)
            if (scope.TryResolve(name, out var symbolKind))
                return symbolKind;

        return null;
    }

    private void EnterScope()
    {
        _scopes.Push(new AnalysisScope());
    }

    private void LeaveScope()
    {
        _scopes.Pop();
    }

    private void AddError(string message)
    {
        _errors.Add(message);
    }

    private void AddWarning(string message)
    {
        _warnings.Add(message);
    }
}

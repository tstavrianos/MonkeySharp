using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;

namespace MonkeySharp.AbstractSyntaxTree.Analyzers;

/// <summary>
/// Performs data flow analysis to detect uninitialized variables and other data flow issues.
/// </summary>
internal class DataFlowAnalyzer
{
    private enum VariableState
    {
        Uninitialized,
        Initialized,
        MaybeInitialized,
    }

    private class DataFlowScope
    {
        public Dictionary<string, VariableState> Variables { get; } = new();
    }

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly Stack<DataFlowScope> _scopes = new();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Analyzes the given AST node for data flow issues.
    /// </summary>
    /// <param name="node">The AST node to analyze</param>
    /// <returns>True if analysis succeeded without errors</returns>
    public bool Analyze(Node node)
    {
        _errors.Clear();
        _warnings.Clear();
        _scopes.Clear();

        EnterScope();
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
                AnalyzeNode(returnStatement.ReturnValue);
                break;

            case IfExpression ifExpression:
                AnalyzeIfExpression(ifExpression);
                break;

            case FunctionLiteral functionLiteral:
                AnalyzeFunctionLiteral(functionLiteral);
                break;

            case CallExpression callExpression:
                AnalyzeNode(callExpression.Function);
                foreach (var arg in callExpression.Arguments)
                    AnalyzeNode(arg);
                break;

            case Identifier identifier:
                CheckVariableInitialization(identifier);
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

            // Literals are always "initialized"
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

        // Mark the variable as initialized
        SetVariableState(letStatement.Name.Value, VariableState.Initialized);
    }

    private void AnalyzeIfExpression(IfExpression ifExpression)
    {
        AnalyzeNode(ifExpression.Condition);

        // Save current state
        var stateBeforeIf = CloneCurrentScopeState();

        // Analyze consequence branch
        AnalyzeNode(ifExpression.Consequence);
        var stateAfterConsequence = CloneCurrentScopeState();

        // Restore state and analyze alternative branch
        RestoreScopeState(stateBeforeIf);

        if (ifExpression.Alternative != null)
        {
            AnalyzeNode(ifExpression.Alternative);
            var stateAfterAlternative = CloneCurrentScopeState();

            // Merge states: variable is initialized only if initialized in both branches
            MergeStates(stateAfterConsequence, stateAfterAlternative);
        }
        else
        {
            // No else branch: variables might not be initialized
            MergeStatesWithMaybe(stateAfterConsequence, stateBeforeIf);
        }
    }

    private void AnalyzeFunctionLiteral(FunctionLiteral functionLiteral)
    {
        EnterScope();

        // Parameters are always initialized
        foreach (var param in functionLiteral.Parameters)
            SetVariableState(param.Value, VariableState.Initialized);

        AnalyzeNode(functionLiteral.Body);

        LeaveScope();
    }

    private void CheckVariableInitialization(Identifier identifier)
    {
        var state = GetVariableState(identifier.Value);

        switch (state)
        {
            case VariableState.Uninitialized:
                AddError($"Variable '{identifier.Value}' is used before being assigned a value");
                break;
            case VariableState.MaybeInitialized:
                AddWarning(
                    $"Variable '{identifier.Value}' may be used before being assigned a value"
                );
                break;
            case VariableState.Initialized:
                // OK
                break;
            case null:
                // Variable not in any scope (semantic error, will be caught by SemanticAnalyzer)
                break;
        }
    }

    private VariableState? GetVariableState(string name)
    {
        foreach (var scope in _scopes)
            if (scope.Variables.TryGetValue(name, out var state))
                return state;

        return null;
    }

    private void SetVariableState(string name, VariableState state)
    {
        CurrentScope().Variables[name] = state;
    }

    private Dictionary<string, VariableState> CloneCurrentScopeState()
    {
        return new Dictionary<string, VariableState>(CurrentScope().Variables);
    }

    private void RestoreScopeState(Dictionary<string, VariableState> state)
    {
        CurrentScope().Variables.Clear();
        foreach (var (name, varState) in state)
            CurrentScope().Variables[name] = varState;
    }

    private void MergeStates(
        Dictionary<string, VariableState> state1,
        Dictionary<string, VariableState> state2
    )
    {
        var currentScope = CurrentScope();
        currentScope.Variables.Clear();

        // Collect all variables from both states
        var allVariables = new HashSet<string>(state1.Keys);
        allVariables.UnionWith(state2.Keys);

        foreach (var varName in allVariables)
        {
            var hasState1 = state1.TryGetValue(varName, out var varState1);
            var hasState2 = state2.TryGetValue(varName, out var varState2);

            if (hasState1 && hasState2)
            {
                // Variable exists in both: take most conservative state
                if (
                    varState1 == VariableState.Initialized
                    && varState2 == VariableState.Initialized
                )
                    currentScope.Variables[varName] = VariableState.Initialized;
                else
                    currentScope.Variables[varName] = VariableState.MaybeInitialized;
            }
            else
            {
                // Variable only in one branch
                currentScope.Variables[varName] = VariableState.MaybeInitialized;
            }
        }
    }

    private void MergeStatesWithMaybe(
        Dictionary<string, VariableState> afterBranch,
        Dictionary<string, VariableState> beforeBranch
    )
    {
        var currentScope = CurrentScope();
        currentScope.Variables.Clear();

        // Start with before state
        foreach (var (name, state) in beforeBranch)
            currentScope.Variables[name] = state;

        // Variables initialized in the branch are maybe initialized overall
        foreach (var (name, state) in afterBranch)
            if (!beforeBranch.ContainsKey(name) || beforeBranch[name] != VariableState.Initialized)
                if (state == VariableState.Initialized)
                    currentScope.Variables[name] = VariableState.MaybeInitialized;
    }

    private DataFlowScope CurrentScope()
    {
        return _scopes.Peek();
    }

    private void EnterScope()
    {
        _scopes.Push(new DataFlowScope());
    }

    private void LeaveScope()
    {
        _scopes.Pop();
    }

    private void AddError(string message)
    {
        _errors.Add($"Data Flow: {message}");
    }

    private void AddWarning(string message)
    {
        _warnings.Add($"Data Flow: {message}");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Objects;
using Expression = System.Linq.Expressions.Expression;
using IndexExpression = MonkeySharp.Core.Ast.Expressions.IndexExpression;

namespace MonkeySharp.Core.VirtualMachine;

public class ILCompiler
{
    private readonly Dictionary<string, int> _builtinIndices = new();

    public ILCompiler()
    {
        foreach (var (name, index) in Builtins.Keys()) _builtinIndices[name] = index;
    }

    public Func<object> CompileProgram(ProgramNode program)
    {
        try
        {
            var globalVars = new Dictionary<string, ParameterExpression>();
            var bodyExpressions = new List<Expression>();

            // Compile each statement
            for (var i = 0; i < program.Statements.Count; i++)
            {
                var statement = program.Statements[i];
                var isLast = i == program.Statements.Count - 1;

                var (expr, err) = CompileNode(statement, globalVars, null);
                if (!string.IsNullOrEmpty(err))
                    throw new InvalidOperationException($"Compilation error: {err}");

                if (expr != null)
                {
                    // For statements that aren't the last, or for let statements, 
                    // we just execute them without returning their value
                    if (!isLast && statement is ExpressionStatement)
                    {
                        // Wrap in a block that ignores the result
                        var temp = Expression.Variable(typeof(IObject), "_temp");
                        bodyExpressions.Add(Expression.Block(
                            new[] {temp},
                            Expression.Assign(temp, expr)));
                    }
                    else
                    {
                        bodyExpressions.Add(expr);
                    }
                }
            }

            // If no expressions, return null
            if (bodyExpressions.Count == 0)
            {
                bodyExpressions.Add(Expression.Field(null, typeof(NullObject), nameof(NullObject.Null)));
            }
            // Ensure the last expression is the return value
            else
            {
                var lastStmt = program.Statements[^1];
                // If last statement is a let, return null
                if (lastStmt is LetStatement)
                    bodyExpressions.Add(Expression.Field(null, typeof(NullObject), nameof(NullObject.Null)));
            }

            var block = Expression.Block(
                typeof(IObject),
                globalVars.Values,
                bodyExpressions);

            var lambda = Expression.Lambda<Func<IObject>>(block);
            return lambda.Compile();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Compilation failed: {ex.Message}", ex);
        }
    }

    private (Expression expr, string error) CompileNode(
        Node node,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        switch (node)
        {
            case ExpressionStatement exprStmt:
                return CompileNode(exprStmt.Expression, globalVars, localVars);

            case LetStatement letStmt:
                var vars = localVars ?? globalVars;

                // For recursive functions, we need to pre-declare the variable
                if (letStmt.Value is FunctionLiteral funcLit1)
                {
                    var funcVar = Expression.Variable(typeof(IObject), letStmt.Name.Value);
                    vars[letStmt.Name.Value] = funcVar;

                    var (funcExpr, funcErr) = CompileFunction(funcLit1, globalVars, localVars, letStmt.Name.Value);
                    if (!string.IsNullOrEmpty(funcErr)) return (null, funcErr);

                    return (Expression.Assign(funcVar, funcExpr), null);
                }
                else
                {
                    var (valueExpr, err) = CompileNode(letStmt.Value, globalVars, localVars);
                    if (!string.IsNullOrEmpty(err)) return (null, err);

                    var varExpr = Expression.Variable(typeof(IObject), letStmt.Name.Value);
                    vars[letStmt.Name.Value] = varExpr;

                    return (Expression.Assign(varExpr, valueExpr), null);
                }

            case ReturnStatement returnStmt:
                var (returnExpr, returnErr) = CompileNode(returnStmt.ReturnValue, globalVars, localVars);
                if (!string.IsNullOrEmpty(returnErr)) return (null, returnErr);

                // Mark that this function has a return statement
                return (Expression.Return(_returnLabel, returnExpr, typeof(IObject)), null);

            case BlockStatement blockStmt:
                return CompileBlock(blockStmt, globalVars, localVars);

            case IntegerLiteral intLit:
                return (Expression.Convert(
                    Expression.Call(
                        typeof(IntegerObject).GetMethod(nameof(IntegerObject.Create))!,
                        Expression.Constant(intLit.Value)),
                    typeof(IObject)), null);

            case BooleanLiteral boolLit:
                var field = boolLit.Value
                    ? typeof(BooleanObject).GetField(nameof(BooleanObject.True))!
                    : typeof(BooleanObject).GetField(nameof(BooleanObject.False))!;
                return (Expression.Convert(Expression.Field(null, field), typeof(IObject)), null);

            case StringLiteral strLit:
                return (Expression.Convert(
                    Expression.Call(
                        typeof(StringObject).GetMethod(nameof(StringObject.Create))!,
                        Expression.Constant(strLit.Value)),
                    typeof(IObject)), null);

            case Identifier identifier:
                if (_builtinIndices.ContainsKey(identifier.Value))
                    return (null, $"builtin '{identifier.Value}' cannot be used as a value");

                if (localVars != null && localVars.TryGetValue(identifier.Value, out var localVar))
                    return (localVar, null);

                if (globalVars.TryGetValue(identifier.Value, out var globalVar))
                    return (globalVar, null);

                return (null, $"undefined variable {identifier.Value}");

            case InfixExpression infix:
                return CompileInfix(infix, globalVars, localVars);

            case PrefixExpression prefix:
                return CompilePrefix(prefix, globalVars, localVars);

            case IfExpression ifExpr:
                return CompileIf(ifExpr, globalVars, localVars);

            case ArrayLiteral arrayLit:
                return CompileArray(arrayLit, globalVars, localVars);

            case HashLiteral hashLit:
                return CompileHash(hashLit, globalVars, localVars);

            case IndexExpression indexExpr:
                return CompileIndex(indexExpr, globalVars, localVars);

            case FunctionLiteral funcLit:
                return CompileFunction(funcLit, globalVars, localVars, null);
            case CallExpression callExpr:
                return CompileCall(callExpr, globalVars, localVars);

            default:
                return (null, $"unsupported node type: {node.GetType().Name}");
        }
    }

    private (Expression expr, string error) CompileBlock(
        BlockStatement blockStmt,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        if (blockStmt.Statements.Count == 0)
            return (Expression.Field(null, typeof(NullObject), nameof(NullObject.Null)), null);

        var expressions = new List<Expression>();

        for (var i = 0; i < blockStmt.Statements.Count; i++)
        {
            var stmt = blockStmt.Statements[i];
            var isLast = i == blockStmt.Statements.Count - 1;

            var (expr, err) = CompileNode(stmt, globalVars, localVars);
            if (!string.IsNullOrEmpty(err)) return (null, err);

            if (expr != null)
            {
                // For non-last statements that are expressions, ignore their result
                if (!isLast && stmt is ExpressionStatement)
                {
                    var temp = Expression.Variable(typeof(IObject), "_temp");
                    expressions.Add(Expression.Block(
                        new[] {temp},
                        Expression.Assign(temp, expr)));
                }
                else
                {
                    expressions.Add(expr);
                }
            }
        }

        if (expressions.Count == 0) return (Expression.Field(null, typeof(NullObject), nameof(NullObject.Null)), null);

        // If last statement was a let, add null return
        if (blockStmt.Statements[^1] is LetStatement)
            expressions.Add(Expression.Field(null, typeof(NullObject), nameof(NullObject.Null)));

        if (expressions.Count == 1) return (expressions[0], null);

        return (Expression.Block(typeof(IObject), expressions), null);
    }

    private (Expression expr, string error) CompileInfix(
        InfixExpression infix,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var (left, leftErr) = CompileNode(infix.Left, globalVars, localVars);
        if (!string.IsNullOrEmpty(leftErr)) return (null, leftErr);

        var (right, rightErr) = CompileNode(infix.Right, globalVars, localVars);
        if (!string.IsNullOrEmpty(rightErr)) return (null, rightErr);

        var methodName = infix.Operator switch
        {
            "+" => nameof(ILRuntime.Add),
            "-" => nameof(ILRuntime.Subtract),
            "*" => nameof(ILRuntime.Multiply),
            "/" => nameof(ILRuntime.Divide),
            ">" => nameof(ILRuntime.GreaterThan),
            "<" => nameof(ILRuntime.LessThan),
            "==" => nameof(ILRuntime.Equal),
            "!=" => nameof(ILRuntime.NotEqual),
            _ => null
        };

        if (methodName == null)
            return (null, $"unknown operator: {infix.Operator}");

        var method = typeof(ILRuntime).GetMethod(methodName)!;
        return (Expression.Call(method, left, right), null);
    }

    private (Expression expr, string error) CompilePrefix(
        PrefixExpression prefix,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var (operand, err) = CompileNode(prefix.Right, globalVars, localVars);
        if (!string.IsNullOrEmpty(err)) return (null, err);

        var method = prefix.Operator switch
        {
            "-" => typeof(ILRuntime).GetMethod(nameof(ILRuntime.Negate))!,
            "!" => typeof(ILRuntime).GetMethod(nameof(ILRuntime.Bang))!,
            _ => null
        };

        if (method == null)
            return (null, $"unknown operator: {prefix.Operator}");

        return (Expression.Call(method, operand), null);
    }

    private (Expression expr, string error) CompileIf(
        IfExpression ifExpr,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var (cond, condErr) = CompileNode(ifExpr.Condition, globalVars, localVars);
        if (!string.IsNullOrEmpty(condErr)) return (null, condErr);

        var isTruthyMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.IsTruthy))!;
        var truthyExpr = Expression.Call(isTruthyMethod, cond);

        var (consequence, consErr) = CompileNode(ifExpr.Consequence, globalVars, localVars);
        if (!string.IsNullOrEmpty(consErr)) return (null, consErr);

        Expression alternative;
        if (ifExpr.Alternative != null)
        {
            var (alt, altErr) = CompileNode(ifExpr.Alternative, globalVars, localVars);
            if (!string.IsNullOrEmpty(altErr)) return (null, altErr);
            alternative = alt;
        }
        else
        {
            alternative = Expression.Field(null, typeof(NullObject), nameof(NullObject.Null));
        }

        return (Expression.Condition(truthyExpr, consequence, alternative, typeof(IObject)), null);
    }

    private (Expression expr, string error) CompileArray(
        ArrayLiteral arrayLit,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var elementExprs = new List<Expression>();
        foreach (var element in arrayLit.Elements)
        {
            var (elemExpr, err) = CompileNode(element, globalVars, localVars);
            if (!string.IsNullOrEmpty(err)) return (null, err);
            elementExprs.Add(elemExpr);
        }

        var arrayMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CreateArray))!;
        var arrayExpr = Expression.NewArrayInit(typeof(IObject), elementExprs);
        return (Expression.Call(arrayMethod, arrayExpr), null);
    }

    private (Expression expr, string error) CompileHash(
        HashLiteral hashLit,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var hashExprs = new List<Expression>();
        foreach (var pair in hashLit.Pairs.OrderBy(p => p.Key.ToString()))
        {
            var (keyExpr, keyErr) = CompileNode(pair.Key, globalVars, localVars);
            if (!string.IsNullOrEmpty(keyErr)) return (null, keyErr);
            hashExprs.Add(keyExpr);

            var (valExpr, valErr) = CompileNode(pair.Value, globalVars, localVars);
            if (!string.IsNullOrEmpty(valErr)) return (null, valErr);
            hashExprs.Add(valExpr);
        }

        var hashMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CreateHash))!;
        var hashArrayExpr = Expression.NewArrayInit(typeof(IObject), hashExprs);
        return (Expression.Call(hashMethod, hashArrayExpr), null);
    }

    private (Expression expr, string error) CompileIndex(
        IndexExpression indexExpr,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        var (leftExpr, leftErr) = CompileNode(indexExpr.Left, globalVars, localVars);
        if (!string.IsNullOrEmpty(leftErr)) return (null, leftErr);

        var (idxExpr, idxErr) = CompileNode(indexExpr.Index, globalVars, localVars);
        if (!string.IsNullOrEmpty(idxErr)) return (null, idxErr);

        var indexMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.IndexOperation))!;
        return (Expression.Call(indexMethod, leftExpr, idxExpr), null);
    }

    private LabelTarget? _returnLabel;

    private (Expression expr, string error) CompileFunction(
        FunctionLiteral funcLit,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression>? outerLocalVars,
        string? functionName = null)
    {
        var funcLocalVars = new Dictionary<string, ParameterExpression>();
        var parameters = new List<ParameterExpression>();

        foreach (var param in funcLit.Parameters)
        {
            var paramExpr = Expression.Variable(typeof(IObject), param.Value);
            funcLocalVars[param.Value] = paramExpr;
            parameters.Add(paramExpr);
        }

        // Collect all referenced variables to determine what needs to be captured
        var referencedVars =
            CollectReferencedVariables(funcLit.Body, funcLit.Parameters.Select(p => p.Value).ToHashSet());

        // Check if this is a recursive function
        var isRecursive = functionName != null && referencedVars.Contains(functionName);

        // Determine which variables need to be captured from outer scopes
        var closureVars = new List<ParameterExpression>();
        var seenClosureVars = new HashSet<ParameterExpression>();

        // Capture outer local variables
        if (outerLocalVars != null)
            foreach (var kvp in outerLocalVars)
                if (!funcLocalVars.ContainsKey(kvp.Key) && referencedVars.Contains(kvp.Key))
                    if (seenClosureVars.Add(kvp.Value))
                        closureVars.Add(kvp.Value);

        // Capture global variables that are referenced
        foreach (var kvp in globalVars)
            if (!funcLocalVars.ContainsKey(kvp.Key) && referencedVars.Contains(kvp.Key))
                if (seenClosureVars.Add(kvp.Value))
                    closureVars.Add(kvp.Value);

        ParameterExpression? selfVar = null;
        if (isRecursive && functionName != null)
        {
            if (funcLocalVars.TryGetValue(functionName, out var localSelf))
                selfVar = localSelf;
            else if (outerLocalVars != null && outerLocalVars.TryGetValue(functionName, out var outerSelf))
                selfVar = outerSelf;
            else if (globalVars.TryGetValue(functionName, out var globalSelf))
                selfVar = globalSelf;

            if (selfVar != null && !seenClosureVars.Contains(selfVar))
            {
                closureVars.Add(selfVar);
                seenClosureVars.Add(selfVar);
            }
        }

        // If there are closure variables OR if this is recursive, create a closure
        if (closureVars.Count > 0)
        {
            var allParameters = new List<ParameterExpression>();
            allParameters.AddRange(closureVars);
            allParameters.AddRange(parameters);

            var availableVars = new Dictionary<string, ParameterExpression>();
            // Map closure parameters
            foreach (var v in closureVars)
                availableVars[v.Name] = v;
            // Map function parameters  
            foreach (var kvp in funcLocalVars)
                availableVars[kvp.Key] = kvp.Value;

            var previousReturnLabel = _returnLabel;
            _returnLabel = Expression.Label(typeof(IObject), "return");

            // Compile body with only the variables that are actually parameters of this lambda
            var (bodyExpr, bodyErr) =
                CompileNode(funcLit.Body, new Dictionary<string, ParameterExpression>(), availableVars);
            if (!string.IsNullOrEmpty(bodyErr))
            {
                _returnLabel = previousReturnLabel;
                return (null, bodyErr);
            }

            if (bodyExpr == null)
                bodyExpr = Expression.Field(null, typeof(NullObject), nameof(NullObject.Null));

            bodyExpr = Expression.Block(
                bodyExpr,
                Expression.Label(_returnLabel, bodyExpr)
            );

            _returnLabel = previousReturnLabel;

            var localVarsToDeclare = availableVars.Values
                .Where(v => !allParameters.Contains(v))
                .ToList();

            if (localVarsToDeclare.Count > 0)
                bodyExpr = Expression.Block(
                    typeof(IObject),
                    localVarsToDeclare,
                    bodyExpr
                );

            var lambda = Expression.Lambda(bodyExpr, allParameters);

            var envArrayVar = Expression.Variable(typeof(IObject[]), "env");
            var blockExprs = new List<Expression>();

            blockExprs.Add(Expression.Assign(envArrayVar,
                Expression.NewArrayBounds(typeof(IObject), Expression.Constant(closureVars.Count))));

            var needsFixup = false;
            var selfVarIndex = -1;

            for (var i = 0; i < closureVars.Count; i++)
            {
                var val = closureVars[i];
                if (selfVar != null && val == selfVar)
                {
                    needsFixup = true;
                    selfVarIndex = i;
                }
                else
                {
                    blockExprs.Add(Expression.Assign(
                        Expression.ArrayAccess(envArrayVar, Expression.Constant(i)),
                        val));
                }
            }

            var createClosureMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CreateClosure))!;
            var closureExpr = Expression.Call(createClosureMethod,
                Expression.Constant(lambda.Compile()),
                Expression.Constant(funcLit.Parameters.Count),
                envArrayVar,
                Expression.Constant(funcLit.TokenLiteral));

            if (needsFixup)
            {
                blockExprs.Add(Expression.Assign(selfVar!, closureExpr));
                blockExprs.Add(Expression.Assign(
                    Expression.ArrayAccess(envArrayVar, Expression.Constant(selfVarIndex)),
                    selfVar!));
                blockExprs.Add(selfVar!);
            }
            else
            {
                blockExprs.Add(closureExpr);
            }

            return (Expression.Block(new[] {envArrayVar}, blockExprs), null);
        }
        else
        {
            // No closure needed - standard function compilation
            var availableVars = new Dictionary<string, ParameterExpression>();
            foreach (var kvp in funcLocalVars) availableVars[kvp.Key] = kvp.Value;

            var previousReturnLabel = _returnLabel;
            _returnLabel = Expression.Label(typeof(IObject), "return");

            var (bodyExpr, bodyErr) =
                CompileNode(funcLit.Body, new Dictionary<string, ParameterExpression>(), availableVars);
            if (!string.IsNullOrEmpty(bodyErr))
            {
                _returnLabel = previousReturnLabel;
                return (null, bodyErr);
            }

            if (bodyExpr == null)
                bodyExpr = Expression.Field(null, typeof(NullObject), nameof(NullObject.Null));

            bodyExpr = Expression.Block(
                bodyExpr,
                Expression.Label(_returnLabel, bodyExpr)
            );

            _returnLabel = previousReturnLabel;

            var localVarsToDeclare = availableVars.Values
                .Where(v => !parameters.Contains(v))
                .ToList();

            if (localVarsToDeclare.Count > 0)
                bodyExpr = Expression.Block(
                    typeof(IObject),
                    localVarsToDeclare,
                    bodyExpr
                );

            var lambda = Expression.Lambda(bodyExpr, parameters);
            var compiledDelegate = lambda.Compile();

            var createFuncMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CreateFunction))!;
            var funcExpr = Expression.Call(
                createFuncMethod,
                Expression.Constant(compiledDelegate),
                Expression.Constant(funcLit.Parameters.Count),
                Expression.Constant(funcLit.TokenLiteral));

            return (funcExpr, null);
        }
    }

    private HashSet<string> CollectReferencedVariables(Node node, HashSet<string>? functionParams = null)
    {
        var referenced = new HashSet<string>();
        var declaredLocals = new HashSet<string>();

        // Function parameters act as declared locals for the function body
        if (functionParams != null)
            foreach (var param in functionParams)
                declaredLocals.Add(param);

        CollectReferencedVariablesRecursive(node, referenced, declaredLocals);
        return referenced;
    }

    private void CollectReferencedVariablesRecursive(Node node, HashSet<string> referenced,
        HashSet<string> declaredLocals)
    {
        switch (node)
        {
            case Identifier identifier:
                // Only add if it's not a builtin and not declared locally in this scope
                if (!_builtinIndices.ContainsKey(identifier.Value) && !declaredLocals.Contains(identifier.Value))
                    referenced.Add(identifier.Value);
                break;

            case LetStatement letStmt:
                // First collect references in the value expression
                CollectReferencedVariablesRecursive(letStmt.Value, referenced, declaredLocals);
                // Then mark this variable as declared locally
                declaredLocals.Add(letStmt.Name.Value);
                break;

            case BlockStatement blockStmt:
                foreach (var stmt in blockStmt.Statements)
                    CollectReferencedVariablesRecursive(stmt, referenced, declaredLocals);
                break;

            case ExpressionStatement exprStmt:
                CollectReferencedVariablesRecursive(exprStmt.Expression, referenced, declaredLocals);
                break;

            case ReturnStatement returnStmt:
                CollectReferencedVariablesRecursive(returnStmt.ReturnValue, referenced, declaredLocals);
                break;

            case InfixExpression infix:
                CollectReferencedVariablesRecursive(infix.Left, referenced, declaredLocals);
                CollectReferencedVariablesRecursive(infix.Right, referenced, declaredLocals);
                break;

            case PrefixExpression prefix:
                CollectReferencedVariablesRecursive(prefix.Right, referenced, declaredLocals);
                break;

            case IfExpression ifExpr:
                CollectReferencedVariablesRecursive(ifExpr.Condition, referenced, declaredLocals);
                CollectReferencedVariablesRecursive(ifExpr.Consequence, referenced, declaredLocals);
                if (ifExpr.Alternative != null)
                    CollectReferencedVariablesRecursive(ifExpr.Alternative, referenced, declaredLocals);
                break;

            case CallExpression callExpr:
                CollectReferencedVariablesRecursive(callExpr.Function, referenced, declaredLocals);
                foreach (var arg in callExpr.Arguments)
                    CollectReferencedVariablesRecursive(arg, referenced, declaredLocals);
                break;

            case FunctionLiteral funcLit:
                // For nested functions, we need to collect what THEY reference from OUR scope
                // Create a new scope with the nested function's parameters
                var nestedDeclared = new HashSet<string>(declaredLocals);
                foreach (var param in funcLit.Parameters)
                    nestedDeclared.Add(param.Value);

                // Collect what the nested function references
                var nestedReferenced = new HashSet<string>();
                CollectReferencedVariablesRecursive(funcLit.Body, nestedReferenced, nestedDeclared);

                // Add those references to our set (they become our closure captures)
                foreach (var varName in nestedReferenced)
                    if (!declaredLocals.Contains(varName))
                        referenced.Add(varName);
                break;

            case ArrayLiteral arrayLit:
                foreach (var element in arrayLit.Elements)
                    CollectReferencedVariablesRecursive(element, referenced, declaredLocals);
                break;

            case HashLiteral hashLit:
                foreach (var pair in hashLit.Pairs)
                {
                    CollectReferencedVariablesRecursive(pair.Key, referenced, declaredLocals);
                    CollectReferencedVariablesRecursive(pair.Value, referenced, declaredLocals);
                }

                break;

            case IndexExpression indexExpr:
                CollectReferencedVariablesRecursive(indexExpr.Left, referenced, declaredLocals);
                CollectReferencedVariablesRecursive(indexExpr.Index, referenced, declaredLocals);
                break;
        }
    }

    private (Expression expr, string error) CompileCall(
        CallExpression callExpr,
        Dictionary<string, ParameterExpression> globalVars,
        Dictionary<string, ParameterExpression> localVars)
    {
        if (callExpr.Function is Identifier identifier &&
            _builtinIndices.TryGetValue(identifier.Value, out var builtinIndex))
        {
            var argExprs = new List<Expression>();
            foreach (var arg in callExpr.Arguments)
            {
                var (argExpr, argErr) = CompileNode(arg, globalVars, localVars);
                if (!string.IsNullOrEmpty(argErr)) return (null, argErr);
                argExprs.Add(argExpr);
            }

            var argsArray = Expression.NewArrayInit(typeof(IObject), argExprs);
            var builtinMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CallBuiltin))!;
            return (Expression.Call(builtinMethod, Expression.Constant(builtinIndex), argsArray), null);
        }

        var (funcExpr, funcErr) = CompileNode(callExpr.Function, globalVars, localVars);
        if (!string.IsNullOrEmpty(funcErr)) return (null, funcErr);

        var callArgExprs = new List<Expression>();
        foreach (var arg in callExpr.Arguments)
        {
            var (argExpr, argErr) = CompileNode(arg, globalVars, localVars);
            if (!string.IsNullOrEmpty(argErr)) return (null, argErr);
            callArgExprs.Add(argExpr);
        }

        var callArgsArray = Expression.NewArrayInit(typeof(IObject), callArgExprs);
        var callMethod = typeof(ILRuntime).GetMethod(nameof(ILRuntime.CallFunction))!;
        return (Expression.Call(callMethod, funcExpr, callArgsArray), null);
    }
}
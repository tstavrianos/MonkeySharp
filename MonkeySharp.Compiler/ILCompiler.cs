using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.Compiler;

/// <summary>
/// Compiles MonkeySharp AST to .NET IL code.
/// </summary>
public sealed class ILCompiler : IStatementVisitor, IExpressionVisitor<object>
{
    private readonly ModuleBuilder _moduleBuilder;
    private ILGenerator _il;
    private TypeBuilder _currentTypeBuilder;
    private Dictionary<string, LocalBuilder> _locals = new();
    private readonly Stack<Dictionary<string, LocalBuilder>> _scopeStack = new();
    private int _labelCounter;
    private int _typeCounter;
    private List<FieldBuilder> _closureFields = new();
    private TypeBuilder _closureTypeBuilder;
    private Label? _returnLabel;
    private LocalBuilder _closureContextLocal;
    private Dictionary<string, FieldBuilder> _closureFieldMap = new();

    public ILCompiler()
    {
        var assemblyName = new AssemblyName($"MonkeySharpCompiled_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// Compiles a MonkeySharp program to an executable delegate.
    /// </summary>
    public Func<MonkeyObject> Compile(ProgramNode program)
    {
        var typeName = $"MonkeyProgram_{_typeCounter++}";
        _currentTypeBuilder = _moduleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class);

        var method = _currentTypeBuilder.DefineMethod(
            "Execute",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(MonkeyObject),
            Type.EmptyTypes);

        _il = method.GetILGenerator();
        _locals.Clear();
        _scopeStack.Clear();
        _returnLabel = null;

        var lastValueLocal = _il.DeclareLocal(typeof(MonkeyObject));

        // Visit all statements and track the last value
        for (var i = 0; i < program.Statements.Count; i++)
        {
            var statement = program.Statements[i];
            var isLast = i == program.Statements.Count - 1;

            if (statement is ExpressionStatement exprStmt && isLast)
            {
                // For the last expression statement, keep the value
                exprStmt.Expression?.Accept(this);
                _il.Emit(OpCodes.Stloc, lastValueLocal);
            }
            else
            {
                statement.Accept(this);
            }
        }

        // Return the last value or null
        _il.Emit(OpCodes.Ldloc, lastValueLocal);
        var nullCheck = _il.DefineLabel();
        var end = _il.DefineLabel();
        _il.Emit(OpCodes.Brtrue, nullCheck);
        EmitNull();
        _il.Emit(OpCodes.Br, end);
        _il.MarkLabel(nullCheck);
        _il.Emit(OpCodes.Ldloc, lastValueLocal);
        _il.MarkLabel(end);
        _il.Emit(OpCodes.Ret);

        var type = _currentTypeBuilder.CreateType();
        var compiledMethod = type.GetMethod("Execute");
        return (Func<MonkeyObject>) Delegate.CreateDelegate(typeof(Func<MonkeyObject>), compiledMethod);
    }

    #region Statement Visitors

    public void Visit(LetStatement letStatement)
    {
        // Evaluate the value expression
        letStatement.Value?.Accept(this);

        // Store in a local variable
        var local = DeclareLocal(letStatement.Name.Value, typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, local);
    }

    public void Visit(ReturnStatement returnStatement)
    {
        if (returnStatement.ReturnValue != null)
            returnStatement.ReturnValue.Accept(this);
        else
            EmitNull();
        _il.Emit(OpCodes.Ret);
    }

    public void Visit(ExpressionStatement expressionStatement)
    {
        if (expressionStatement.Expression != null)
        {
            expressionStatement.Expression.Accept(this);
            // Pop the result since it's not the last statement
            _il.Emit(OpCodes.Pop);
        }
    }

    public void Visit(BlockStatement blockStatement)
    {
        PushScope();

        foreach (var statement in blockStatement.Statements) statement.Accept(this);

        PopScope();
    }

    #endregion

    #region Expression Visitors

    public object Visit(Identifier identifier)
    {
        // Try to load from locals first
        if (TryGetLocal(identifier.Value, out var local))
        {
            _il.Emit(OpCodes.Ldloc, local);
        }
        // Check if it's a captured variable in a closure
        else if (_closureTypeBuilder != null && TryGetCapturedVariable(identifier.Value, out var field))
        {
            _il.Emit(OpCodes.Ldloc, _closureContextLocal);
            _il.Emit(OpCodes.Ldfld, field);
        }
        else
        {
            // Check for built-in functions
            EmitBuiltinFunction(identifier.Value);
        }

        return null;
    }

    public object Visit(IntegerLiteral integerLiteral)
    {
        _il.Emit(OpCodes.Ldc_I8, integerLiteral.Value);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)]));
        return null;
    }

    public object Visit(StringLiteral stringLiteral)
    {
        _il.Emit(OpCodes.Ldstr, stringLiteral.Value);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyString).GetConstructor([typeof(string)]));
        return null;
    }

    public object Visit(BooleanLiteral booleanLiteral)
    {
        var field = booleanLiteral.Value
            ? typeof(MonkeyBoolean).GetField(nameof(MonkeyBoolean.True))
            : typeof(MonkeyBoolean).GetField(nameof(MonkeyBoolean.False));
        _il.Emit(OpCodes.Ldsfld, field);
        return null;
    }

    public object Visit(ArrayLiteral arrayLiteral)
    {
        // Create array
        _il.Emit(OpCodes.Ldc_I4, arrayLiteral.Elements.Count);
        _il.Emit(OpCodes.Newarr, typeof(MonkeyObject));

        // Fill array
        for (var i = 0; i < arrayLiteral.Elements.Count; i++)
        {
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldc_I4, i);
            arrayLiteral.Elements[i].Accept(this);
            _il.Emit(OpCodes.Stelem_Ref);
        }

        _il.Emit(OpCodes.Newobj, typeof(MonkeyArray).GetConstructor([typeof(MonkeyObject[])]));
        return null;
    }

    public object Visit(HashLiteral hashLiteral)
    {
        // Create dictionary
        _il.Emit(OpCodes.Newobj,
            typeof(Dictionary<MonkeyObject, MonkeyObject>).GetConstructor(Type.EmptyTypes));

        foreach (var pair in hashLiteral.Pairs)
        {
            _il.Emit(OpCodes.Dup);
            pair.Key.Accept(this);
            pair.Value.Accept(this);
            _il.Emit(OpCodes.Callvirt,
                typeof(Dictionary<MonkeyObject, MonkeyObject>).GetMethod("Add"));
        }

        _il.Emit(OpCodes.Newobj,
            typeof(MonkeyHash).GetConstructor([typeof(Dictionary<MonkeyObject, MonkeyObject>)]));
        return null;
    }

    public object Visit(PrefixExpression prefixExpression)
    {
        prefixExpression.Right.Accept(this);

        switch (prefixExpression.Operator)
        {
            case "!":
                EmitBangOperator();
                break;
            case "-":
                EmitMinusOperator();
                break;
            default:
                throw new Exception($"Unknown operator: {prefixExpression.Operator}");
        }

        return null;
    }

    public object Visit(InfixExpression infixExpression)
    {
        infixExpression.Left.Accept(this);
        infixExpression.Right.Accept(this);

        switch (infixExpression.Operator)
        {
            case "+":
                EmitAddOperator();
                break;
            case "-":
                EmitSubtractOperator();
                break;
            case "*":
                EmitMultiplyOperator();
                break;
            case "/":
                EmitDivideOperator();
                break;
            case "==":
                EmitEqualsOperator();
                break;
            case "!=":
                EmitNotEqualsOperator();
                break;
            case "<":
                EmitLessThanOperator();
                break;
            case ">":
                EmitGreaterThanOperator();
                break;
            default:
                throw new Exception($"Unknown operator: {infixExpression.Operator}");
        }

        return null;
    }

    public object Visit(IfExpression ifExpression)
    {
        var elseLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();
        var resultLocal = _il.DeclareLocal(typeof(MonkeyObject));

        // Evaluate condition
        ifExpression.Condition.Accept(this);
        EmitIsTruthy();
        _il.Emit(OpCodes.Brfalse, elseLabel);

        // Consequence block - evaluate and store last expression
        PushScope();
        if (ifExpression.Consequence.Statements.Count > 0)
        {
            for (var i = 0; i < ifExpression.Consequence.Statements.Count; i++)
            {
                var stmt = ifExpression.Consequence.Statements[i];
                if (i == ifExpression.Consequence.Statements.Count - 1 && stmt is ExpressionStatement exprStmt)
                {
                    exprStmt.Expression?.Accept(this);
                    _il.Emit(OpCodes.Stloc, resultLocal);
                }
                else
                {
                    stmt.Accept(this);
                }
            }
        }
        else
        {
            EmitNull();
            _il.Emit(OpCodes.Stloc, resultLocal);
        }

        PopScope();
        _il.Emit(OpCodes.Br, endLabel);

        // Alternative block
        _il.MarkLabel(elseLabel);
        if (ifExpression.Alternative != null)
        {
            PushScope();
            if (ifExpression.Alternative.Statements.Count > 0)
            {
                for (var i = 0; i < ifExpression.Alternative.Statements.Count; i++)
                {
                    var stmt = ifExpression.Alternative.Statements[i];
                    if (i == ifExpression.Alternative.Statements.Count - 1 && stmt is ExpressionStatement exprStmt)
                    {
                        exprStmt.Expression?.Accept(this);
                        _il.Emit(OpCodes.Stloc, resultLocal);
                    }
                    else
                    {
                        stmt.Accept(this);
                    }
                }
            }
            else
            {
                EmitNull();
                _il.Emit(OpCodes.Stloc, resultLocal);
            }

            PopScope();
        }
        else
        {
            EmitNull();
            _il.Emit(OpCodes.Stloc, resultLocal);
        }

        _il.MarkLabel(endLabel);
        _il.Emit(OpCodes.Ldloc, resultLocal);
        return null;
    }

    public object Visit(FunctionLiteral functionLiteral)
    {
        // Create a new type for this function
        var funcTypeName = $"Function_{_typeCounter++}";
        var funcTypeBuilder = _moduleBuilder.DefineType(
            funcTypeName,
            TypeAttributes.Public | TypeAttributes.Class);

        // Analyze and capture free variables (closure)
        var freeVars = AnalyzeFreeVariables(functionLiteral);
        var closureFields = new Dictionary<string, FieldBuilder>();

        // Create fields for captured variables
        foreach (var varName in freeVars)
        {
            var field = funcTypeBuilder.DefineField(
                $"_captured_{varName}",
                typeof(MonkeyObject),
                FieldAttributes.Public);
            closureFields[varName] = field;
        }

        // Define constructor if we have captured variables
        ConstructorBuilder constructor = null;
        if (freeVars.Count > 0)
        {
            var ctorParamTypes = Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray();
            constructor = funcTypeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                ctorParamTypes);

            var ctorIL = constructor.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);

            for (var i = 0; i < freeVars.Count; i++)
            {
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Ldarg, i + 1);
                ctorIL.Emit(OpCodes.Stfld, closureFields[freeVars[i]]);
            }

            ctorIL.Emit(OpCodes.Ret);
        }

        // Define the method
        var paramTypes = Enumerable.Repeat(typeof(MonkeyObject), functionLiteral.Parameters.Count).ToArray();
        var method = funcTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public,
            typeof(MonkeyObject),
            paramTypes);

        var oldIL = _il;
        var oldLocals = new Dictionary<string, LocalBuilder>(_locals);
        var oldTypeBuilder = _currentTypeBuilder;
        var oldClosureFields = new List<FieldBuilder>(_closureFields);
        var oldClosureTypeBuilder = _closureTypeBuilder;
        var oldClosureContextLocal = _closureContextLocal;
        var oldClosureFieldMap = new Dictionary<string, FieldBuilder>(_closureFieldMap);

        _il = method.GetILGenerator();
        _locals.Clear();
        _closureFields.Clear();
        _closureTypeBuilder = funcTypeBuilder;
        _closureContextLocal = null;
        _closureFieldMap.Clear();

        // Store 'this' reference for accessing captured variables
        if (freeVars.Count > 0)
        {
            _closureContextLocal = _il.DeclareLocal(funcTypeBuilder);
            _il.Emit(OpCodes.Ldarg_0);
            _il.Emit(OpCodes.Stloc, _closureContextLocal);

            // Map variable names to field builders
            foreach (var kvp in closureFields) _closureFieldMap[kvp.Key] = kvp.Value;
        }

        // Map parameters to locals
        for (var i = 0; i < functionLiteral.Parameters.Count; i++)
        {
            var paramName = functionLiteral.Parameters[i].Value;
            var local = _il.DeclareLocal(typeof(MonkeyObject));
            _locals[paramName] = local;
            _il.Emit(OpCodes.Ldarg, i + 1); // +1 because arg 0 is 'this'
            _il.Emit(OpCodes.Stloc, local);
        }

        // Add closure fields to tracking
        foreach (var kvp in closureFields) _closureFields.Add(kvp.Value);

        // Compile function body - return last expression value
        var hasReturn = false;

        for (var i = 0; i < functionLiteral.Body.Statements.Count; i++)
        {
            var stmt = functionLiteral.Body.Statements[i];
            if (stmt is ReturnStatement)
            {
                hasReturn = true;
                stmt.Accept(this);
            }
            else if (i == functionLiteral.Body.Statements.Count - 1 && stmt is ExpressionStatement exprStmt)
            {
                // Last expression becomes the return value
                if (exprStmt.Expression is CallExpression callExpr)
                {
                    // This is a tail call!
                    //callExpr.Accept(this, isTailPosition: true);
                    Visit(callExpr, true);
                }
                else
                {
                    exprStmt.Expression?.Accept(this);
                    _il.Emit(OpCodes.Ret);
                }

                hasReturn = true;
            }
            else
            {
                stmt.Accept(this);
            }
        }

        // Return null if no explicit return and no last expression
        if (!hasReturn)
        {
            EmitNull();
            _il.Emit(OpCodes.Ret);
        }

        var funcType = funcTypeBuilder.CreateType();

        // Restore previous IL generator
        _il = oldIL;
        _locals = oldLocals;
        _currentTypeBuilder = oldTypeBuilder;
        _closureFields = oldClosureFields;
        _closureTypeBuilder = oldClosureTypeBuilder;
        _closureContextLocal = oldClosureContextLocal;
        _closureFieldMap = oldClosureFieldMap;

        // Check if this is a self-referential function
        var isSelfReferential = !string.IsNullOrEmpty(functionLiteral.Name) &&
                                freeVars.Contains(functionLiteral.Name);

        // Create instance of the function type
        if (freeVars.Count > 0)
        {
            // If self-referential, we need special handling
            if (isSelfReferential)
            {
                // Store the function in a local temporarily
                var tempFuncLocal = _il.DeclareLocal(typeof(MonkeyFunction));

                // Load captured variable values (excluding the self-reference for now)
                var nonSelfFreeVars = freeVars.Where(v => v != functionLiteral.Name).ToList();
                foreach (var varName in nonSelfFreeVars) LoadVariable(varName);

                // For self-reference, we'll pass null initially
                EmitNull();

                // Create the function instance
                _il.Emit(OpCodes.Newobj, funcType.GetConstructor(
                    Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray())!);

                // Create MonkeyFunction wrapper
                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
                _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(functionLiteral.Parameters.Count));
                _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
                _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
                _il.Emit(OpCodes.Newobj,
                    typeof(MonkeyFunction).GetConstructor([typeof(Delegate), typeof(string), typeof(int)])!);

                // Store in temp local
                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Stloc, tempFuncLocal);

                // Now we need to set the self-reference field
                // Load the function instance again, get the Target property (the closure instance), and set the field
                _il.Emit(OpCodes.Callvirt, typeof(MonkeyFunction).GetProperty("CompiledFunction")!.GetGetMethod()!);
                _il.Emit(OpCodes.Callvirt, typeof(Delegate).GetProperty("Target")!.GetGetMethod()!);
                _il.Emit(OpCodes.Castclass, funcType);
                _il.Emit(OpCodes.Ldloc, tempFuncLocal);

                // Get the field for the self-reference
                var selfField = funcType.GetField($"_captured_{functionLiteral.Name}");
                _il.Emit(OpCodes.Stfld, selfField!);

                // Load the function back onto the stack
                _il.Emit(OpCodes.Ldloc, tempFuncLocal);
            }
            else
            {
                // Load captured variable values
                foreach (var varName in freeVars) LoadVariable(varName);
                _il.Emit(OpCodes.Newobj, funcType.GetConstructor(
                    Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray())!);

                // Create MonkeyFunction wrapper
                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
                _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(functionLiteral.Parameters.Count));
                _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
                _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
                _il.Emit(OpCodes.Newobj,
                    typeof(MonkeyFunction).GetConstructor([typeof(Delegate), typeof(string), typeof(int)])!);
            }
        }
        else
        {
            _il.Emit(OpCodes.Newobj, funcType.GetConstructor(Type.EmptyTypes)!);

            // Create MonkeyFunction wrapper
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
            _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(functionLiteral.Parameters.Count));
            _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
            _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
            _il.Emit(OpCodes.Newobj,
                typeof(MonkeyFunction).GetConstructor([typeof(Delegate), typeof(string), typeof(int)])!);
        }

        return null;
    }

    public object Visit(CallExpression callExpression)
    {
        return Visit(callExpression, false);
    }

    private object Visit(CallExpression callExpression, bool isTailPosition = false)
    {
        // Evaluate arguments first
        var argLocals = new LocalBuilder[callExpression.Arguments.Count];
        for (var i = 0; i < callExpression.Arguments.Count; i++)
        {
            callExpression.Arguments[i].Accept(this);
            argLocals[i] = _il.DeclareLocal(typeof(MonkeyObject));
            _il.Emit(OpCodes.Stloc, argLocals[i]);
        }

        // Evaluate the function
        callExpression.Function.Accept(this);

        // Cast to MonkeyFunction
        var funcLocal = _il.DeclareLocal(typeof(MonkeyFunction));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyFunction));
        _il.Emit(OpCodes.Stloc, funcLocal);

        // Load function delegate
        _il.Emit(OpCodes.Ldloc, funcLocal);
        _il.Emit(OpCodes.Callvirt,
            typeof(MonkeyFunction).GetProperty(nameof(MonkeyFunction.CompiledFunction))!.GetGetMethod()!);

        // Load arguments
        for (var i = 0; i < argLocals.Length; i++) _il.Emit(OpCodes.Ldloc, argLocals[i]);

        // If this is a tail call, emit the tail prefix
        if (isTailPosition) _il.Emit(OpCodes.Tailcall);

        // Invoke the function
        var invokeMethod = GetFuncDelegateType(callExpression.Arguments.Count).GetMethod("Invoke");
        _il.Emit(OpCodes.Callvirt, invokeMethod);

        // If tail call, immediately return
        if (isTailPosition) _il.Emit(OpCodes.Ret);

        return null;
    }

    public object Visit(IndexExpression indexExpression)
    {
        indexExpression.Left.Accept(this);
        indexExpression.Index.Accept(this);

        var leftLocal = _il.DeclareLocal(typeof(MonkeyObject));
        var indexLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, indexLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var arrayLabel = _il.DefineLabel();
        var hashLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        // Check if it's an array
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyArray));
        _il.Emit(OpCodes.Brtrue, arrayLabel);

        // Check if it's a hash
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyHash));
        _il.Emit(OpCodes.Brtrue, hashLabel);

        // Invalid type - push null and jump to end
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        // Array indexing
        _il.MarkLabel(arrayLabel);
        var arrayIndexLocal = _il.DeclareLocal(typeof(long));
        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Stloc, arrayIndexLocal);

        // Check if index < 0
        var arrayCheckLengthLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Ldc_I8, 0L);
        _il.Emit(OpCodes.Bge, arrayCheckLengthLabel); // if index >= 0, check length
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        // Check if index < array length
        _il.MarkLabel(arrayCheckLengthLabel);
        var arrayOkLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyArray));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyArray).GetProperty(nameof(MonkeyArray.Elements))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldlen);
        _il.Emit(OpCodes.Conv_I8);
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Bgt, arrayOkLabel); // if length > index, ok

        // Out of bounds
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        // Get array element
        _il.MarkLabel(arrayOkLabel);
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyArray));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyArray).GetProperty(nameof(MonkeyArray.Elements))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Conv_I4);
        _il.Emit(OpCodes.Ldelem_Ref);
        _il.Emit(OpCodes.Br, endLabel);

        // Hash indexing
        _il.MarkLabel(hashLabel);
        var hashDictLocal = _il.DeclareLocal(typeof(Dictionary<MonkeyObject, MonkeyObject>));
        var hashResultLocal = _il.DeclareLocal(typeof(MonkeyObject));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyHash));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyHash).GetProperty(nameof(MonkeyHash.Pairs))!.GetGetMethod()!);
        _il.Emit(OpCodes.Stloc, hashDictLocal);

        _il.Emit(OpCodes.Ldloc, hashDictLocal);
        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Ldloca, hashResultLocal);
        _il.Emit(OpCodes.Callvirt,
            typeof(Dictionary<MonkeyObject, MonkeyObject>).GetMethod("TryGetValue")!);

        var hashFoundLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brtrue, hashFoundLabel);
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(hashFoundLabel);
        _il.Emit(OpCodes.Ldloc, hashResultLocal);

        _il.MarkLabel(endLabel);
        return null;
    }

    #endregion

    #region Helper Methods

    private LocalBuilder DeclareLocal(string name, Type type)
    {
        var local = _il.DeclareLocal(type);
        _locals[name] = local;
        return local;
    }

    private bool TryGetLocal(string name, out LocalBuilder local)
    {
        return _locals.TryGetValue(name, out local);
    }

    private void PushScope()
    {
        _scopeStack.Push(new Dictionary<string, LocalBuilder>(_locals));
    }

    private void PopScope()
    {
        if (_scopeStack.Count > 0)
        {
            _locals.Clear();
            foreach (var kvp in _scopeStack.Pop()) _locals[kvp.Key] = kvp.Value;
        }
    }

    private void EmitNull()
    {
        _il.Emit(OpCodes.Ldsfld, typeof(MonkeyNull).GetField(nameof(MonkeyNull.Instance)));
    }

    private void EmitBangOperator()
    {
        EmitIsTruthy();
        _il.Emit(OpCodes.Ldc_I4_0);
        _il.Emit(OpCodes.Ceq);
        _il.Emit(OpCodes.Call, typeof(MonkeyBoolean).GetMethod(nameof(MonkeyBoolean.From)));
    }

    private void EmitMinusOperator()
    {
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Neg);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)])!);
    }

    private void EmitAddOperator()
    {
        var leftLocal = _il.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        // Check for string concatenation
        var intLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyString));
        _il.Emit(OpCodes.Brfalse, intLabel);

        // String concatenation
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyString));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyString).GetProperty(nameof(MonkeyString.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyString));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyString).GetProperty(nameof(MonkeyString.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", [typeof(string), typeof(string)])!);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyString).GetConstructor([typeof(string)])!);
        _il.Emit(OpCodes.Br, endLabel);

        // Integer addition
        _il.MarkLabel(intLabel);
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Add);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)])!);

        _il.MarkLabel(endLabel);
    }

    private void EmitSubtractOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Sub);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)])!);
    }

    private void EmitMultiplyOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Mul);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)])!);
    }

    private void EmitDivideOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Div);
        _il.Emit(OpCodes.Newobj, typeof(MonkeyInteger).GetConstructor([typeof(long)])!);
    }

    private void EmitEqualsOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        // Call Equals method for value equality
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(object.Equals), [typeof(object)])!);
        _il.Emit(OpCodes.Call, typeof(MonkeyBoolean).GetMethod(nameof(MonkeyBoolean.From))!);
    }

    private void EmitNotEqualsOperator()
    {
        EmitEqualsOperator();
        EmitBangOperator();
    }

    private void EmitLessThanOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Clt);
        _il.Emit(OpCodes.Call, typeof(MonkeyBoolean).GetMethod(nameof(MonkeyBoolean.From))!);
    }

    private void EmitGreaterThanOperator()
    {
        var rightLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyInteger).GetProperty(nameof(MonkeyInteger.Value))!.GetGetMethod()!);
        _il.Emit(OpCodes.Cgt);
        _il.Emit(OpCodes.Call, typeof(MonkeyBoolean).GetMethod(nameof(MonkeyBoolean.From))!);
    }

    private void EmitIsTruthy()
    {
        var objLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, objLocal);

        var boolLabel = _il.DefineLabel();
        var nullLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        // Check if null
        _il.Emit(OpCodes.Ldloc, objLocal);
        _il.Emit(OpCodes.Brfalse, nullLabel);

        // Check if MonkeyBoolean
        _il.Emit(OpCodes.Ldloc, objLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyBoolean));
        _il.Emit(OpCodes.Brtrue, boolLabel);

        // All other non-null objects are truthy
        _il.Emit(OpCodes.Ldc_I4_1);
        _il.Emit(OpCodes.Br, endLabel);

        // Null is falsy
        _il.MarkLabel(nullLabel);
        _il.Emit(OpCodes.Ldc_I4_0);
        _il.Emit(OpCodes.Br, endLabel);

        // Boolean: extract value
        _il.MarkLabel(boolLabel);
        _il.Emit(OpCodes.Ldloc, objLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyBoolean));
        _il.Emit(OpCodes.Callvirt, typeof(MonkeyBoolean).GetProperty(nameof(MonkeyBoolean.Value))!.GetGetMethod()!);

        _il.MarkLabel(endLabel);
    }

    private void EmitBuiltinFunction(string name)
    {
        // Create a wrapper function for the built-in
        var builtins = typeof(BuiltinFunctions);
        var method = builtins.GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        if (method == null) throw new Exception($"Unknown identifier: {name}");

        // Create a dynamic wrapper type for this built-in function
        var wrapperTypeName = $"BuiltinWrapper_{name}_{_typeCounter++}";
        var wrapperTypeBuilder = _moduleBuilder.DefineType(
            wrapperTypeName,
            TypeAttributes.Public | TypeAttributes.Class);

        var parameters = method.GetParameters();
        var paramTypes = Enumerable.Repeat(typeof(MonkeyObject), parameters.Length).ToArray();

        var wrapperMethod = wrapperTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public,
            typeof(MonkeyObject),
            paramTypes);

        var wrapperIL = wrapperMethod.GetILGenerator();

        // Load all arguments
        for (var i = 0; i < parameters.Length; i++) wrapperIL.Emit(OpCodes.Ldarg, i + 1);

        // Call the static built-in function
        wrapperIL.Emit(OpCodes.Call, method);
        wrapperIL.Emit(OpCodes.Ret);

        var wrapperType = wrapperTypeBuilder.CreateType();

        // Create instance and wrap in MonkeyFunction
        _il.Emit(OpCodes.Newobj, wrapperType.GetConstructor(Type.EmptyTypes));
        _il.Emit(OpCodes.Dup);
        _il.Emit(OpCodes.Ldvirtftn, wrapperType.GetMethod("Invoke"));
        _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(parameters.Length));
        _il.Emit(OpCodes.Ldstr, name);
        _il.Emit(OpCodes.Ldc_I4, parameters.Length);
        _il.Emit(OpCodes.Newobj,
            typeof(MonkeyFunction).GetConstructor([typeof(Delegate), typeof(string), typeof(int)]));
    }

    private Type GetFuncDelegateType(int paramCount)
    {
        return paramCount switch
        {
            0 => typeof(Func<MonkeyObject>),
            1 => typeof(Func<MonkeyObject, MonkeyObject>),
            2 => typeof(Func<MonkeyObject, MonkeyObject, MonkeyObject>),
            3 => typeof(Func<MonkeyObject, MonkeyObject, MonkeyObject, MonkeyObject>),
            _ => throw new NotSupportedException($"Functions with {paramCount} parameters not yet supported")
        };
    }

    private ConstructorInfo GetFuncDelegateConstructor(int paramCount)
    {
        var delegateType = GetFuncDelegateType(paramCount);
        return delegateType.GetConstructors()[0];
    }

    private List<string> AnalyzeFreeVariables(FunctionLiteral functionLiteral)
    {
        var freeVars = new HashSet<string>();
        var boundVars = new HashSet<string>();

        // Add parameters as bound variables
        foreach (var param in functionLiteral.Parameters) boundVars.Add(param.Value);

        // Find all identifiers used in the function body
        CollectFreeVariables(functionLiteral.Body, boundVars, freeVars, functionLiteral.Name);

        return freeVars.ToList();
    }

    private void CollectFreeVariables(Node node, HashSet<string> boundVars, HashSet<string> freeVars,
        string functionName = null)
    {
        switch (node)
        {
            case Identifier id:
                // If identifier is not bound, check if it's a free variable
                if (!boundVars.Contains(id.Value))
                {
                    // Check if it's a self-reference (recursive call)
                    if (functionName != null && id.Value == functionName)
                        freeVars.Add(id.Value);
                    // Or if it exists in outer scope
                    else if (_locals.ContainsKey(id.Value)) freeVars.Add(id.Value);
                }

                break;

            case LetStatement let:
                CollectFreeVariables(let.Value, boundVars, freeVars, functionName);
                boundVars.Add(let.Name.Value);
                break;

            case ReturnStatement ret:
                if (ret.ReturnValue != null)
                    CollectFreeVariables(ret.ReturnValue, boundVars, freeVars, functionName);
                break;

            case ExpressionStatement expr:
                if (expr.Expression != null)
                    CollectFreeVariables(expr.Expression, boundVars, freeVars, functionName);
                break;

            case BlockStatement block:
                foreach (var stmt in block.Statements)
                    CollectFreeVariables(stmt, boundVars, freeVars, functionName);
                break;

            case PrefixExpression prefix:
                CollectFreeVariables(prefix.Right, boundVars, freeVars, functionName);
                break;

            case InfixExpression infix:
                CollectFreeVariables(infix.Left, boundVars, freeVars, functionName);
                CollectFreeVariables(infix.Right, boundVars, freeVars, functionName);
                break;

            case IfExpression ifExpr:
                CollectFreeVariables(ifExpr.Condition, boundVars, freeVars, functionName);
                CollectFreeVariables(ifExpr.Consequence, boundVars, freeVars, functionName);
                if (ifExpr.Alternative != null)
                    CollectFreeVariables(ifExpr.Alternative, boundVars, freeVars, functionName);
                break;

            case FunctionLiteral func:
                // Don't recurse into nested functions - they'll analyze their own free vars
                break;

            case CallExpression call:
                CollectFreeVariables(call.Function, boundVars, freeVars, functionName);
                foreach (var arg in call.Arguments)
                    CollectFreeVariables(arg, boundVars, freeVars, functionName);
                break;

            case ArrayLiteral array:
                foreach (var elem in array.Elements)
                    CollectFreeVariables(elem, boundVars, freeVars, functionName);
                break;

            case HashLiteral hash:
                foreach (var pair in hash.Pairs)
                {
                    CollectFreeVariables(pair.Key, boundVars, freeVars, functionName);
                    CollectFreeVariables(pair.Value, boundVars, freeVars, functionName);
                }

                break;

            case IndexExpression index:
                CollectFreeVariables(index.Left, boundVars, freeVars, functionName);
                CollectFreeVariables(index.Index, boundVars, freeVars, functionName);
                break;
        }
    }

    private bool TryGetCapturedVariable(string name, out FieldBuilder field)
    {
        field = null;
        if (_closureTypeBuilder == null)
            return false;

        // Use the tracked field map instead of reflection
        return _closureFieldMap.TryGetValue(name, out field);
    }

    private void LoadVariable(string name)
    {
        if (TryGetLocal(name, out var local))
        {
            _il.Emit(OpCodes.Ldloc, local);
        }
        else if (_closureContextLocal != null && TryGetCapturedVariable(name, out var field))
        {
            _il.Emit(OpCodes.Ldloc, _closureContextLocal);
            _il.Emit(OpCodes.Ldfld, field);
        }
        else
        {
            throw new Exception($"Variable not found: {name}");
        }
    }

    #endregion
}
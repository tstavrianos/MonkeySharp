using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.AbstractSyntaxTree.Visitors;

namespace MonkeySharp.Compiler;

/// <summary>
/// Compiles MonkeySharp AST to .NET IL code with inline optimizations.
/// </summary>
internal sealed class ILCompiler : IStatementVisitor, IExpressionVisitor
{
    #region Cached Reflection Members

    private static class CachedMembers
    {
        // Constructors
        public static readonly ConstructorInfo MonkeyIntegerCtor =
            typeof(MonkeyInteger).GetConstructor([typeof(long)])!;

        public static readonly ConstructorInfo MonkeyStringCtor =
            typeof(MonkeyString).GetConstructor([typeof(string)])!;

        public static readonly ConstructorInfo MonkeyErrorCtor =
            typeof(MonkeyError).GetConstructor([typeof(string)])!;

        public static readonly ConstructorInfo MonkeyArrayCtor =
            typeof(MonkeyArray).GetConstructor([typeof(MonkeyObject[])])!;

        public static readonly ConstructorInfo MonkeyHashCtor = typeof(MonkeyHash).GetConstructor([
            typeof(Dictionary<MonkeyObject, MonkeyObject>),
        ])!;

        public static readonly ConstructorInfo MonkeyFunctionCtor =
            typeof(MonkeyFunction).GetConstructor([typeof(Delegate), typeof(string), typeof(int)])!;

        public static readonly ConstructorInfo DictCtor = typeof(Dictionary<
            MonkeyObject,
            MonkeyObject
        >).GetConstructor(Type.EmptyTypes)!;

        // Fields
        public static readonly FieldInfo MonkeyNullInstance = typeof(MonkeyNull).GetField(
            nameof(MonkeyNull.Instance)
        )!;

        public static readonly FieldInfo MonkeyBooleanTrue = typeof(MonkeyBoolean).GetField(
            nameof(MonkeyBoolean.True)
        )!;

        public static readonly FieldInfo MonkeyBooleanFalse = typeof(MonkeyBoolean).GetField(
            nameof(MonkeyBoolean.False)
        )!;

        // Methods
        public static readonly MethodInfo MonkeyBooleanFrom = typeof(MonkeyBoolean).GetMethod(
            nameof(MonkeyBoolean.From)
        )!;

        public static readonly MethodInfo MonkeyObjectTypeName = typeof(MonkeyObject).GetMethod(
            nameof(MonkeyObject.TypeName)
        )!;

        public static readonly MethodInfo ObjectEquals = typeof(object).GetMethod(
            nameof(object.Equals),
            [typeof(object)]
        )!;

        public static readonly MethodInfo StringConcat2 = typeof(string).GetMethod(
            "Concat",
            [typeof(string), typeof(string)]
        )!;

        public static readonly MethodInfo StringConcat4 = typeof(string).GetMethod(
            "Concat",
            [typeof(string), typeof(string), typeof(string), typeof(string)]
        )!;

        public static readonly MethodInfo DictAdd = typeof(Dictionary<
            MonkeyObject,
            MonkeyObject
        >).GetMethod("Add")!;

        public static readonly MethodInfo DictTryGetValue = typeof(Dictionary<
            MonkeyObject,
            MonkeyObject
        >).GetMethod("TryGetValue")!;

        public static readonly MethodInfo MonkeyFunctionValidateArgumentCount =
            typeof(MonkeyFunction).GetMethod(nameof(MonkeyFunction.ValidateArgumentCount))!;

        // Properties
        public static readonly MethodInfo MonkeyIntegerGetValue = typeof(MonkeyInteger)
            .GetProperty(nameof(MonkeyInteger.Value))!
            .GetGetMethod()!;

        public static readonly MethodInfo MonkeyStringGetValue = typeof(MonkeyString)
            .GetProperty(nameof(MonkeyString.Value))!
            .GetGetMethod()!;

        public static readonly MethodInfo MonkeyBooleanGetValue = typeof(MonkeyBoolean)
            .GetProperty(nameof(MonkeyBoolean.Value))!
            .GetGetMethod()!;

        public static readonly MethodInfo MonkeyArrayGetElements = typeof(MonkeyArray)
            .GetProperty(nameof(MonkeyArray.Elements))!
            .GetGetMethod()!;

        public static readonly MethodInfo MonkeyHashGetPairs = typeof(MonkeyHash)
            .GetProperty(nameof(MonkeyHash.Pairs))!
            .GetGetMethod()!;

        public static readonly MethodInfo MonkeyFunctionGetCompiledFunction = typeof(MonkeyFunction)
            .GetProperty(nameof(MonkeyFunction.CompiledFunction))!
            .GetGetMethod()!;

        public static readonly MethodInfo DelegateGetTarget = typeof(Delegate)
            .GetProperty("Target")!
            .GetGetMethod()!;
    }

    #endregion

    private readonly ModuleBuilder _moduleBuilder;
    private ILGenerator? _il;
    private TypeBuilder? _currentTypeBuilder;
    private Dictionary<string, LocalBuilder> _locals = new();
    private readonly Stack<Dictionary<string, LocalBuilder>> _scopeStack = new();
    private int _typeCounter;
    private List<FieldBuilder> _closureFields = [];
    private TypeBuilder? _closureTypeBuilder;
    private LocalBuilder? _closureContextLocal;
    private Dictionary<string, FieldBuilder> _closureFieldMap = new();

    // Pre-built host-function wrappers keyed by function name.
    private readonly Dictionary<
        string,
        (Type type, ConstructorInfo ctor, MethodInfo invoke, int arity)
    > _hostWrappers = new();

    internal ILCompiler(
        IReadOnlyDictionary<
            string,
            (int arity, Func<MonkeyObject[], MonkeyObject> fn)
        >? hostFunctions = null
    )
    {
        // Use a fixed assembly name so that the InternalsVisibleTo / IgnoresAccessChecksTo
        // pair can grant this dynamic assembly access to MonkeySharp.Compiler internals.
        var assemblyName = new AssemblyName("MonkeySharpDynamic");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run
        );

        // Bypass access checks for internal types in MonkeySharp.Compiler (e.g. MonkeyFunction).
        var ignoresAttrCtor = typeof(IgnoresAccessChecksToAttribute).GetConstructor([
            typeof(string),
        ])!;
        assemblyBuilder.SetCustomAttribute(
            new CustomAttributeBuilder(ignoresAttrCtor, ["MonkeySharp.Compiler"])
        );

        _moduleBuilder = assemblyBuilder.DefineDynamicModule($"Module_{Guid.NewGuid():N}");

        if (hostFunctions != null)
            foreach (var (name, (arity, fn)) in hostFunctions)
                _hostWrappers[name] = CreateHostWrapper(name, arity, fn);
    }

    // Builds a standalone wrapper type whose static delegate field stores the host callback,
    // and whose Invoke method unpacks fixed-arity args and calls the delegate.
    private (Type type, ConstructorInfo ctor, MethodInfo invoke, int arity) CreateHostWrapper(
        string name,
        int arity,
        Func<MonkeyObject[], MonkeyObject> fn
    )
    {
        var wrapperTypeName = $"HostWrapper_{name}_{_typeCounter++}";
        var wrapperTypeBuilder = _moduleBuilder.DefineType(
            wrapperTypeName,
            TypeAttributes.Public | TypeAttributes.Class
        );

        // Static field storing the user-provided delegate.
        var delegateField = wrapperTypeBuilder.DefineField(
            "_delegate",
            typeof(Func<MonkeyObject[], MonkeyObject>),
            FieldAttributes.Public | FieldAttributes.Static
        );

        // Invoke(arg0, ..., argN) method.
        var paramTypes = Enumerable.Repeat(typeof(MonkeyObject), arity).ToArray();
        var invokeMethod = wrapperTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public,
            typeof(MonkeyObject),
            paramTypes
        );
        var wrapperIl = invokeMethod.GetILGenerator();

        wrapperIl.Emit(OpCodes.Ldsfld, delegateField);
        wrapperIl.Emit(OpCodes.Ldc_I4, arity);
        wrapperIl.Emit(OpCodes.Newarr, typeof(MonkeyObject));
        for (var i = 0; i < arity; i++)
        {
            wrapperIl.Emit(OpCodes.Dup);
            wrapperIl.Emit(OpCodes.Ldc_I4, i);
            wrapperIl.Emit(OpCodes.Ldarg, i + 1); // +1: arg 0 is 'this'
            wrapperIl.Emit(OpCodes.Stelem_Ref);
        }

        wrapperIl.Emit(
            OpCodes.Callvirt,
            typeof(Func<MonkeyObject[], MonkeyObject>).GetMethod("Invoke")!
        );
        wrapperIl.Emit(OpCodes.Ret);

        var createdType = wrapperTypeBuilder.CreateType();
        createdType.GetField("_delegate")!.SetValue(null, fn);

        return (
            createdType,
            createdType.GetConstructor(Type.EmptyTypes)!,
            createdType.GetMethod("Invoke")!,
            arity
        );
    }

    /// <summary>
    /// Compiles a MonkeySharp program to an executable delegate.
    /// </summary>
    public Func<MonkeyObject> Compile(ProgramNode program)
    {
        var typeName = $"MonkeyProgram_{_typeCounter++}";
        _currentTypeBuilder = _moduleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class
        );

        var method = _currentTypeBuilder.DefineMethod(
            "Execute",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(MonkeyObject),
            Type.EmptyTypes
        );

        _il = method.GetILGenerator();
        _locals.Clear();
        _scopeStack.Clear();

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
        return (Func<MonkeyObject>)
            Delegate.CreateDelegate(typeof(Func<MonkeyObject>), compiledMethod!);
    }

    #region Statement Visitors

    public void Visit(LetStatement letStatement)
    {
        // Evaluate the value expression
        letStatement.Value.Accept(this);

        // Check if value is an error
        var valueLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, valueLocal);
        _il.Emit(OpCodes.Ldloc, valueLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyError));

        var notErrorLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brfalse, notErrorLabel);

        // It's an error, return it
        _il.Emit(OpCodes.Ldloc, valueLocal);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(notErrorLabel);
        // Store in a local variable
        var local = DeclareLocal(letStatement.Name.Value, typeof(MonkeyObject));
        _il.Emit(OpCodes.Ldloc, valueLocal);
        _il.Emit(OpCodes.Stloc, local);
    }

    public void Visit(ReturnStatement returnStatement)
    {
        returnStatement.ReturnValue.Accept(this);
        _il!.Emit(OpCodes.Ret);
    }

    public void Visit(ExpressionStatement expressionStatement)
    {
        expressionStatement.Expression.Accept(this);

        // Check if the result is an error and propagate it
        var resultLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, resultLocal);
        _il.Emit(OpCodes.Ldloc, resultLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyError));

        var notErrorLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brfalse, notErrorLabel);

        // It's an error, return it immediately
        _il.Emit(OpCodes.Ldloc, resultLocal);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(notErrorLabel);
    }

    public void Visit(BlockStatement blockStatement)
    {
        PushScope();
        foreach (var statement in blockStatement.Statements)
            statement.Accept(this);
        PopScope();
    }

    #endregion

    #region Expression Visitors

    public void Visit(Identifier identifier)
    {
        // Try to load from locals first
        if (TryGetLocal(identifier.Value, out var local))
        {
            _il!.Emit(OpCodes.Ldloc, local!);
        }
        // Check if it's a captured variable in a closure
        else if (
            _closureTypeBuilder != null
            && TryGetCapturedVariable(identifier.Value, out var field)
        )
        {
            _il!.Emit(OpCodes.Ldloc, _closureContextLocal!);
            _il.Emit(OpCodes.Ldfld, field!);
        }
        else
        {
            // Try to load built-in function, or return error
            if (!TryEmitBuiltinFunction(identifier.Value))
                EmitTypeError($"identifier not found: {identifier.Value}");
        }
    }

    public void Visit(IntegerLiteral integerLiteral)
    {
        _il!.Emit(OpCodes.Ldc_I8, integerLiteral.Value);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);
    }

    public void Visit(StringLiteral stringLiteral)
    {
        _il!.Emit(OpCodes.Ldstr, stringLiteral.Value);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyStringCtor);
    }

    public void Visit(BooleanLiteral booleanLiteral)
    {
        var field = booleanLiteral.Value
            ? CachedMembers.MonkeyBooleanTrue
            : CachedMembers.MonkeyBooleanFalse;
        _il!.Emit(OpCodes.Ldsfld, field);
    }

    public void Visit(ArrayLiteral arrayLiteral)
    {
        var elementLocals = new LocalBuilder[arrayLiteral.Elements.Count];
        for (var i = 0; i < arrayLiteral.Elements.Count; i++)
        {
            arrayLiteral.Elements[i].Accept(this);
            elementLocals[i] = IsLiteral(arrayLiteral.Elements[i])
                ? StoreInLocal()
                : EmitErrorCheckAndReturn();
        }

        // Create and fill array
        _il!.Emit(OpCodes.Ldc_I4, arrayLiteral.Elements.Count);
        _il.Emit(OpCodes.Newarr, typeof(MonkeyObject));

        for (var i = 0; i < elementLocals.Length; i++)
        {
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldc_I4, i);
            _il.Emit(OpCodes.Ldloc, elementLocals[i]);
            _il.Emit(OpCodes.Stelem_Ref);
        }

        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyArrayCtor);
    }

    public void Visit(HashLiteral hashLiteral)
    {
        var pairLocals = new List<(LocalBuilder Key, LocalBuilder Value)>();

        foreach (var pair in hashLiteral.Pairs)
        {
            // Evaluate key
            pair.Key.Accept(this);
            var keyLocal = IsLiteral(pair.Key) ? StoreInLocal() : EmitErrorCheckAndReturn();

            // Validate hash-ability
            EmitHashableCheck(keyLocal);

            // Evaluate value
            pair.Value.Accept(this);
            var valueLocal = IsLiteral(pair.Value) ? StoreInLocal() : EmitErrorCheckAndReturn();

            pairLocals.Add((keyLocal, valueLocal));
        }

        // Create dictionary and add pairs
        _il!.Emit(OpCodes.Newobj, CachedMembers.DictCtor);

        foreach (var (keyLocal, valueLocal) in pairLocals)
        {
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldloc, keyLocal);
            _il.Emit(OpCodes.Ldloc, valueLocal);
            _il.Emit(OpCodes.Callvirt, CachedMembers.DictAdd);
        }

        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyHashCtor);
    }

    public void Visit(PrefixExpression prefixExpression)
    {
        // Optimization: Handle !! (double negation) specially
        if (
            prefixExpression is
            { Operator: "!", Right: PrefixExpression { Operator: "!" } innerPrefix }
        )
        {
            // !! is just truthiness conversion
            innerPrefix.Right.Accept(this);

            // Optimize for boolean literals
            if (innerPrefix.Right is BooleanLiteral)
                // Already a boolean, nothing to do
                return;

            var rightLocal = IsLiteral(innerPrefix.Right)
                ? StoreInLocal()
                : EmitErrorCheckAndReturn();

            _il!.Emit(OpCodes.Ldloc, rightLocal);
            EmitIsTruthy();
            _il.Emit(OpCodes.Call, CachedMembers.MonkeyBooleanFrom);
            return;
        }

        // Optimization: Negate integer literals at compile time
        if (prefixExpression is { Operator: "-", Right: IntegerLiteral intLit })
        {
            _il!.Emit(OpCodes.Ldc_I8, -intLit.Value);
            _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);
            return;
        }

        // Optimization: Negate boolean literals at compile time
        if (prefixExpression is { Operator: "!", Right: BooleanLiteral boolLit })
        {
            var field = boolLit.Value
                ? CachedMembers.MonkeyBooleanFalse
                : CachedMembers.MonkeyBooleanTrue;
            _il!.Emit(OpCodes.Ldsfld, field);
            return;
        }

        prefixExpression.Right.Accept(this);

        if (IsLiteral(prefixExpression.Right))
        {
            var rightLocal = _il!.DeclareLocal(typeof(MonkeyObject));
            _il.Emit(OpCodes.Stloc, rightLocal);
            _il.Emit(OpCodes.Ldloc, rightLocal);
        }
        else
        {
            var rightLocal = EmitErrorCheckAndReturn();
            _il!.Emit(OpCodes.Ldloc, rightLocal);
        }

        switch (prefixExpression.Operator)
        {
            case "!":
                EmitBangOperator();
                break;
            case "-":
                EmitMinusOperator();
                break;
            default:
                EmitTypeError($"unknown operator: {prefixExpression.Operator}");
                break;
        }
    }

    public void Visit(InfixExpression infixExpression)
    {
        // Optimization: Constant folding for integer literals
        if (infixExpression is { Left: IntegerLiteral leftInt, Right: IntegerLiteral rightInt })
        {
            long? result = infixExpression.Operator switch
            {
                "+" => leftInt.Value + rightInt.Value,
                "-" => leftInt.Value - rightInt.Value,
                "*" => leftInt.Value * rightInt.Value,
                "/" => rightInt.Value != 0 ? leftInt.Value / rightInt.Value : null,
                _ => null,
            };

            if (result.HasValue)
            {
                _il!.Emit(OpCodes.Ldc_I8, result.Value);
                _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);
                return;
            }

            // For comparison operators
            bool? boolResult = infixExpression.Operator switch
            {
                "<" => leftInt.Value < rightInt.Value,
                ">" => leftInt.Value > rightInt.Value,
                "==" => leftInt.Value == rightInt.Value,
                "!=" => leftInt.Value != rightInt.Value,
                _ => null,
            };

            if (boolResult.HasValue)
            {
                var field = boolResult.Value
                    ? CachedMembers.MonkeyBooleanTrue
                    : CachedMembers.MonkeyBooleanFalse;
                _il!.Emit(OpCodes.Ldsfld, field);
                return;
            }
        }

        // Optimization: Constant folding for boolean literals
        if (infixExpression is { Left: BooleanLiteral leftBool, Right: BooleanLiteral rightBool })
        {
            bool? result = infixExpression.Operator switch
            {
                "==" => leftBool.Value == rightBool.Value,
                "!=" => leftBool.Value != rightBool.Value,
                _ => null,
            };

            if (result.HasValue)
            {
                var field = result.Value
                    ? CachedMembers.MonkeyBooleanTrue
                    : CachedMembers.MonkeyBooleanFalse;
                _il!.Emit(OpCodes.Ldsfld, field);
                return;
            }
        }

        // Optimization: String concatenation of literals
        if (
            infixExpression is
            { Operator: "+", Left: StringLiteral leftStr, Right: StringLiteral rightStr }
        )
        {
            _il!.Emit(OpCodes.Ldstr, leftStr.Value + rightStr.Value);
            _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyStringCtor);
            return;
        }

        infixExpression.Left.Accept(this);
        var leftLocal = IsLiteral(infixExpression.Left)
            ? StoreInLocal()
            : EmitErrorCheckAndReturn();

        infixExpression.Right.Accept(this);
        var rightLocal = IsLiteral(infixExpression.Right)
            ? StoreInLocal()
            : EmitErrorCheckAndReturn();

        // Load both for operator
        _il!.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Ldloc, rightLocal);

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
                EmitTypeError($"unknown operator: {infixExpression.Operator}");
                break;
        }
    }

    public void Visit(IfExpression ifExpression)
    {
        // Optimization: Compile-time evaluation of constant conditions
        if (ifExpression.Condition is BooleanLiteral boolLit)
        {
            if (boolLit.Value)
            {
                // Always take consequence
                PushScope();
                if (ifExpression.Consequence.Statements.Count > 0)
                    for (var i = 0; i < ifExpression.Consequence.Statements.Count; i++)
                    {
                        var stmt = ifExpression.Consequence.Statements[i];
                        if (
                            i == ifExpression.Consequence.Statements.Count - 1
                            && stmt is ExpressionStatement exprStmt
                        )
                            exprStmt.Expression?.Accept(this);
                        else
                            stmt.Accept(this);
                    }
                else
                    EmitNull();

                PopScope();
                return;
            }
            else
            {
                // Always take alternative or null
                if (ifExpression.Alternative != null)
                {
                    PushScope();
                    if (ifExpression.Alternative.Statements.Count > 0)
                        for (var i = 0; i < ifExpression.Alternative.Statements.Count; i++)
                        {
                            var stmt = ifExpression.Alternative.Statements[i];
                            if (
                                i == ifExpression.Alternative.Statements.Count - 1
                                && stmt is ExpressionStatement exprStmt
                            )
                                exprStmt.Expression?.Accept(this);
                            else
                                stmt.Accept(this);
                        }
                    else
                        EmitNull();

                    PopScope();
                }
                else
                {
                    EmitNull();
                }

                return;
            }
        }

        var elseLabel = _il!.DefineLabel();
        var endLabel = _il.DefineLabel();
        var resultLocal = _il.DeclareLocal(typeof(MonkeyObject));

        // Evaluate condition
        ifExpression.Condition.Accept(this);
        var condLocal = IsLiteral(ifExpression.Condition)
            ? StoreInLocal()
            : EmitErrorCheckAndReturn();

        // Evaluate truthiness
        _il.Emit(OpCodes.Ldloc, condLocal);
        EmitIsTruthy();
        _il.Emit(OpCodes.Brfalse, elseLabel);

        // Consequence block
        PushScope();
        if (ifExpression.Consequence.Statements.Count > 0)
        {
            for (var i = 0; i < ifExpression.Consequence.Statements.Count; i++)
            {
                var stmt = ifExpression.Consequence.Statements[i];
                if (
                    i == ifExpression.Consequence.Statements.Count - 1
                    && stmt is ExpressionStatement exprStmt
                )
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
                    if (
                        i == ifExpression.Alternative.Statements.Count - 1
                        && stmt is ExpressionStatement exprStmt
                    )
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
    }

    public void Visit(FunctionLiteral functionLiteral)
    {
        // Create a new type for this function
        var funcTypeName = $"Function_{_typeCounter++}";
        var funcTypeBuilder = _moduleBuilder.DefineType(
            funcTypeName,
            TypeAttributes.Public | TypeAttributes.Class
        );

        // Analyze and capture free variables (closure)
        var freeVars = AnalyzeFreeVariables(functionLiteral);
        var closureFields = new Dictionary<string, FieldBuilder>();

        // Create fields for captured variables
        foreach (var varName in freeVars)
        {
            var field = funcTypeBuilder.DefineField(
                $"_captured_{varName}",
                typeof(MonkeyObject),
                FieldAttributes.Public
            );
            closureFields[varName] = field;
        }

        // Define constructor if we have captured variables
        if (freeVars.Count > 0)
        {
            var ctorParamTypes = Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray();
            var constructor = funcTypeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                ctorParamTypes
            );

            var ctorIl = constructor.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);

            for (var i = 0; i < freeVars.Count; i++)
            {
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldarg, i + 1);
                ctorIl.Emit(OpCodes.Stfld, closureFields[freeVars[i]]);
            }

            ctorIl.Emit(OpCodes.Ret);
        }

        // Define the method
        var paramTypes = Enumerable
            .Repeat(typeof(MonkeyObject), functionLiteral.Parameters.Count)
            .ToArray();
        var method = funcTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public,
            typeof(MonkeyObject),
            paramTypes
        );

        var oldIl = _il;
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

            foreach (var kvp in closureFields)
                _closureFieldMap[kvp.Key] = kvp.Value;
        }

        // Map parameters to locals
        for (var i = 0; i < functionLiteral.Parameters.Count; i++)
        {
            var paramName = functionLiteral.Parameters[i].Value;
            var local = _il.DeclareLocal(typeof(MonkeyObject));
            _locals[paramName] = local;
            _il.Emit(OpCodes.Ldarg, i + 1);
            _il.Emit(OpCodes.Stloc, local);
        }

        foreach (var kvp in closureFields)
            _closureFields.Add(kvp.Value);

        // Compile function body
        var hasReturn = false;

        for (var i = 0; i < functionLiteral.Body.Statements.Count; i++)
        {
            var stmt = functionLiteral.Body.Statements[i];
            if (stmt is ReturnStatement)
            {
                hasReturn = true;
                stmt.Accept(this);
            }
            else if (
                i == functionLiteral.Body.Statements.Count - 1
                && stmt is ExpressionStatement exprStmt
            )
            {
                if (exprStmt.Expression is CallExpression callExpr)
                {
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

        if (!hasReturn)
        {
            EmitNull();
            _il.Emit(OpCodes.Ret);
        }

        var funcType = funcTypeBuilder.CreateType();

        // Restore previous IL generator
        _il = oldIl;
        _locals = oldLocals;
        _currentTypeBuilder = oldTypeBuilder;
        _closureFields = oldClosureFields;
        _closureTypeBuilder = oldClosureTypeBuilder;
        _closureContextLocal = oldClosureContextLocal;
        _closureFieldMap = oldClosureFieldMap;

        var isSelfReferential =
            !string.IsNullOrEmpty(functionLiteral.Name) && freeVars.Contains(functionLiteral.Name);

        // Create instance of the function type
        if (freeVars.Count > 0)
        {
            if (isSelfReferential)
            {
                var tempFuncLocal = _il!.DeclareLocal(typeof(MonkeyFunction));

                var nonSelfFreeVars = freeVars.Where(v => v != functionLiteral.Name).ToList();
                foreach (var varName in nonSelfFreeVars)
                    LoadVariable(varName);

                EmitNull();

                _il.Emit(
                    OpCodes.Newobj,
                    funcType.GetConstructor(
                        Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray()
                    )!
                );

                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
                _il.Emit(
                    OpCodes.Newobj,
                    GetFuncDelegateConstructor(functionLiteral.Parameters.Count)
                );
                _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
                _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
                _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyFunctionCtor);

                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Stloc, tempFuncLocal);

                _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyFunctionGetCompiledFunction);
                _il.Emit(OpCodes.Callvirt, CachedMembers.DelegateGetTarget);
                _il.Emit(OpCodes.Castclass, funcType);
                _il.Emit(OpCodes.Ldloc, tempFuncLocal);

                var selfField = funcType.GetField($"_captured_{functionLiteral.Name}");
                _il.Emit(OpCodes.Stfld, selfField!);

                _il.Emit(OpCodes.Ldloc, tempFuncLocal);
            }
            else
            {
                foreach (var varName in freeVars)
                    LoadVariable(varName);

                _il!.Emit(
                    OpCodes.Newobj,
                    funcType.GetConstructor(
                        Enumerable.Repeat(typeof(MonkeyObject), freeVars.Count).ToArray()
                    )!
                );

                _il.Emit(OpCodes.Dup);
                _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
                _il.Emit(
                    OpCodes.Newobj,
                    GetFuncDelegateConstructor(functionLiteral.Parameters.Count)
                );
                _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
                _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
                _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyFunctionCtor);
            }
        }
        else
        {
            _il!.Emit(OpCodes.Newobj, funcType.GetConstructor(Type.EmptyTypes)!);

            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldvirtftn, funcType.GetMethod("Invoke")!);
            _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(functionLiteral.Parameters.Count));
            _il.Emit(OpCodes.Ldstr, functionLiteral.Name ?? "<anonymous>");
            _il.Emit(OpCodes.Ldc_I4, functionLiteral.Parameters.Count);
            _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyFunctionCtor);
        }
    }

    public void Visit(CallExpression callExpression)
    {
        Visit(callExpression, false);
    }

    private void Visit(CallExpression callExpression, bool isTailPosition)
    {
        // Evaluate arguments
        var argLocals = new LocalBuilder[callExpression.Arguments.Count];
        for (var i = 0; i < callExpression.Arguments.Count; i++)
        {
            callExpression.Arguments[i].Accept(this);
            argLocals[i] = IsLiteral(callExpression.Arguments[i])
                ? StoreInLocal()
                : EmitErrorCheckAndReturn();
        }

        // Evaluate function
        callExpression.Function.Accept(this);
        var funcResult = EmitErrorCheckAndReturn();

        EmitFunctionCallWithValidation(funcResult, argLocals, isTailPosition);
    }

    public void Visit(IndexExpression indexExpression)
    {
        indexExpression.Left.Accept(this);
        var leftLocal = IsLiteral(indexExpression.Left)
            ? StoreInLocal()
            : EmitErrorCheckAndReturn();

        indexExpression.Index.Accept(this);
        var indexLocal = IsLiteral(indexExpression.Index)
            ? StoreInLocal()
            : EmitErrorCheckAndReturn();

        var arrayLabel = _il!.DefineLabel();
        var hashLabel = _il.DefineLabel();
        var errorLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        // Check if it's an array
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyArray));
        _il.Emit(OpCodes.Brtrue, arrayLabel);

        // Check if it's a hash
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyHash));
        _il.Emit(OpCodes.Brtrue, hashLabel);

        // Invalid type
        _il.Emit(OpCodes.Ldstr, "index operator not supported: ");
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
        _il.Emit(OpCodes.Br, endLabel);

        // Array indexing
        _il.MarkLabel(arrayLabel);

        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        var intIndexLocal = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, intIndexLocal);
        _il.Emit(OpCodes.Ldloc, intIndexLocal);
        _il.Emit(OpCodes.Brfalse, errorLabel);

        var arrayIndexLocal = _il.DeclareLocal(typeof(long));
        _il.Emit(OpCodes.Ldloc, intIndexLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Stloc, arrayIndexLocal);

        var arrayCheckLengthLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Ldc_I8, 0L);
        _il.Emit(OpCodes.Bge, arrayCheckLengthLabel);
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(arrayCheckLengthLabel);
        var arrayOkLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyArray));
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyArrayGetElements);
        _il.Emit(OpCodes.Ldlen);
        _il.Emit(OpCodes.Conv_I8);
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Bgt, arrayOkLabel);

        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(arrayOkLabel);
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyArray));
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyArrayGetElements);
        _il.Emit(OpCodes.Ldloc, arrayIndexLocal);
        _il.Emit(OpCodes.Conv_I4);
        _il.Emit(OpCodes.Ldelem_Ref);
        _il.Emit(OpCodes.Br, endLabel);

        // Hash indexing
        _il.MarkLabel(hashLabel);

        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Isinst, typeof(IHashable));
        var hashableKeyLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brtrue, hashableKeyLabel);

        _il.Emit(OpCodes.Ldstr, "unusable as hash key: ");
        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(hashableKeyLabel);

        var hashDictLocal = _il.DeclareLocal(typeof(Dictionary<MonkeyObject, MonkeyObject>));
        var hashResultLocal = _il.DeclareLocal(typeof(MonkeyObject));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyHash));
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyHashGetPairs);
        _il.Emit(OpCodes.Stloc, hashDictLocal);

        _il.Emit(OpCodes.Ldloc, hashDictLocal);
        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Ldloca, hashResultLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.DictTryGetValue);

        var hashFoundLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brtrue, hashFoundLabel);
        EmitNull();
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(hashFoundLabel);
        _il.Emit(OpCodes.Ldloc, hashResultLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(errorLabel);
        _il.Emit(OpCodes.Ldstr, "index must be INTEGER, got ");
        _il.Emit(OpCodes.Ldloc, indexLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);

        _il.MarkLabel(endLabel);
    }

    #endregion

    #region Helper Methods

    private LocalBuilder DeclareLocal(string name, Type type)
    {
        var local = _il!.DeclareLocal(type);
        _locals[name] = local;
        return local;
    }

    private bool TryGetLocal(string name, out LocalBuilder? local)
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
            foreach (var kvp in _scopeStack.Pop())
                _locals[kvp.Key] = kvp.Value;
        }
    }

    private void EmitNull()
    {
        _il!.Emit(OpCodes.Ldsfld, CachedMembers.MonkeyNullInstance);
    }

    private void EmitTypeError(string message)
    {
        _il!.Emit(OpCodes.Ldstr, message);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
    }

    private void EmitBinaryOpTypeError(string op, LocalBuilder leftLocal, LocalBuilder rightLocal)
    {
        _il!.Emit(OpCodes.Ldstr, "unknown operator: ");
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Ldstr, $" {op} ");
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat4);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
    }

    private void EmitUnaryOpTypeError(string op, LocalBuilder operandLocal)
    {
        _il!.Emit(OpCodes.Ldstr, $"unknown operator: {op}");
        _il.Emit(OpCodes.Ldloc, operandLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
    }

    private void EmitBangOperator()
    {
        EmitIsTruthy();
        _il!.Emit(OpCodes.Ldc_I4_0);
        _il.Emit(OpCodes.Ceq);
        _il.Emit(OpCodes.Call, CachedMembers.MonkeyBooleanFrom);
    }

    private void EmitMinusOperator()
    {
        var operandLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, operandLocal);

        var intOperand = _il.DeclareLocal(typeof(MonkeyInteger));
        _il.Emit(OpCodes.Ldloc, operandLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, intOperand);

        var okLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, intOperand);
        _il.Emit(OpCodes.Brtrue, okLabel);

        EmitUnaryOpTypeError("-", operandLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(okLabel);
        _il.Emit(OpCodes.Ldloc, intOperand);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Neg);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);

        _il.MarkLabel(endLabel);
    }

    private void EmitAddOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var intLabel = _il.DefineLabel();
        var errorLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        // Check if left is string
        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyString));
        _il.Emit(OpCodes.Brfalse, intLabel);

        // String concatenation
        var rightIsString = _il.DeclareLocal(typeof(MonkeyString));
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyString));
        _il.Emit(OpCodes.Stloc, rightIsString);
        _il.Emit(OpCodes.Ldloc, rightIsString);
        _il.Emit(OpCodes.Brfalse, errorLabel);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Castclass, typeof(MonkeyString));
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyStringGetValue);
        _il.Emit(OpCodes.Ldloc, rightIsString);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyStringGetValue);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyStringCtor);
        _il.Emit(OpCodes.Br, endLabel);

        // Integer addition
        _il.MarkLabel(intLabel);
        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Add);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError("+", leftLocal, rightLocal);

        _il.MarkLabel(endLabel);
    }

    private void EmitSubtractOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        var errorLabel = _il.DefineLabel();
        var computeLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brtrue, computeLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError("-", leftLocal, rightLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(computeLabel);
        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Sub);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);

        _il.MarkLabel(endLabel);
    }

    private void EmitMultiplyOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        var errorLabel = _il.DefineLabel();
        var computeLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brtrue, computeLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError("*", leftLocal, rightLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(computeLabel);
        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Mul);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);

        _il.MarkLabel(endLabel);
    }

    private void EmitDivideOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        var errorLabel = _il.DefineLabel();
        var computeLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brtrue, computeLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError("/", leftLocal, rightLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(computeLabel);
        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Div);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyIntegerCtor);

        _il.MarkLabel(endLabel);
    }

    private void EmitEqualsOperator()
    {
        var rightLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var leftLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.ObjectEquals);
        _il.Emit(OpCodes.Call, CachedMembers.MonkeyBooleanFrom);
    }

    private void EmitNotEqualsOperator()
    {
        EmitEqualsOperator();
        EmitBangOperator();
    }

    private void EmitLessThanOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        var errorLabel = _il.DefineLabel();
        var computeLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brtrue, computeLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError("<", leftLocal, rightLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(computeLabel);
        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Clt);
        _il.Emit(OpCodes.Call, CachedMembers.MonkeyBooleanFrom);

        _il.MarkLabel(endLabel);
    }

    private void EmitGreaterThanOperator()
    {
        var leftLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        var rightLocal = _il.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, rightLocal);
        _il.Emit(OpCodes.Stloc, leftLocal);

        var leftInt = _il.DeclareLocal(typeof(MonkeyInteger));
        var rightInt = _il.DeclareLocal(typeof(MonkeyInteger));

        _il.Emit(OpCodes.Ldloc, leftLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, leftInt);
        _il.Emit(OpCodes.Ldloc, rightLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyInteger));
        _il.Emit(OpCodes.Stloc, rightInt);

        var errorLabel = _il.DefineLabel();
        var computeLabel = _il.DefineLabel();
        var endLabel = _il.DefineLabel();

        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Brfalse, errorLabel);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Brtrue, computeLabel);

        _il.MarkLabel(errorLabel);
        EmitBinaryOpTypeError(">", leftLocal, rightLocal);
        _il.Emit(OpCodes.Br, endLabel);

        _il.MarkLabel(computeLabel);
        _il.Emit(OpCodes.Ldloc, leftInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Ldloc, rightInt);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyIntegerGetValue);
        _il.Emit(OpCodes.Cgt);
        _il.Emit(OpCodes.Call, CachedMembers.MonkeyBooleanFrom);

        _il.MarkLabel(endLabel);
    }

    private void EmitIsTruthy()
    {
        var objLocal = _il!.DeclareLocal(typeof(MonkeyObject));
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
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyBooleanGetValue);

        _il.MarkLabel(endLabel);
    }

    private bool TryEmitBuiltinFunction(string name)
    {
        // Check host-registered functions first.
        if (_hostWrappers.TryGetValue(name, out var hw))
        {
            _il!.Emit(OpCodes.Newobj, hw.ctor);
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldvirtftn, hw.invoke);
            _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(hw.arity));
            _il.Emit(OpCodes.Ldstr, name);
            _il.Emit(OpCodes.Ldc_I4, hw.arity);
            _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyFunctionCtor);
            return true;
        }

        var builtins = typeof(BuiltinFunctions);
        var method = builtins.GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        if (method == null)
            return false;

        var wrapperTypeName = $"BuiltinWrapper_{name}_{_typeCounter++}";
        var wrapperTypeBuilder = _moduleBuilder.DefineType(
            wrapperTypeName,
            TypeAttributes.Public | TypeAttributes.Class
        );

        var parameters = method.GetParameters();
        var paramTypes = Enumerable.Repeat(typeof(MonkeyObject), parameters.Length).ToArray();

        var wrapperMethod = wrapperTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public,
            typeof(MonkeyObject),
            paramTypes
        );

        var wrapperIl = wrapperMethod.GetILGenerator();

        for (var i = 0; i < parameters.Length; i++)
            wrapperIl.Emit(OpCodes.Ldarg, i + 1);

        wrapperIl.Emit(OpCodes.Call, method);
        wrapperIl.Emit(OpCodes.Ret);

        var wrapperType = wrapperTypeBuilder.CreateType();

        _il!.Emit(OpCodes.Newobj, wrapperType.GetConstructor(Type.EmptyTypes)!);
        _il.Emit(OpCodes.Dup);
        _il.Emit(OpCodes.Ldvirtftn, wrapperType.GetMethod("Invoke")!);
        _il.Emit(OpCodes.Newobj, GetFuncDelegateConstructor(parameters.Length));
        _il.Emit(OpCodes.Ldstr, name);
        _il.Emit(OpCodes.Ldc_I4, parameters.Length);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyFunctionCtor);

        return true;
    }

    private void EmitFunctionCallWithValidation(
        LocalBuilder funcResult,
        LocalBuilder[] argLocals,
        bool isTailPosition
    )
    {
        _il!.Emit(OpCodes.Ldloc, funcResult);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyFunction));
        var funcLocal = _il.DeclareLocal(typeof(MonkeyFunction));
        _il.Emit(OpCodes.Stloc, funcLocal);

        var validFunctionLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Ldloc, funcLocal);
        _il.Emit(OpCodes.Brtrue, validFunctionLabel);

        _il.Emit(OpCodes.Ldstr, "not a function: ");
        _il.Emit(OpCodes.Ldloc, funcResult);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(validFunctionLabel);

        _il.Emit(OpCodes.Ldloc, funcLocal);
        _il.Emit(OpCodes.Ldc_I4, argLocals.Length);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyFunctionValidateArgumentCount);

        var errorLocal = _il.DeclareLocal(typeof(MonkeyError));
        _il.Emit(OpCodes.Stloc, errorLocal);
        _il.Emit(OpCodes.Ldloc, errorLocal);

        var noErrorLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brfalse, noErrorLabel);

        _il.Emit(OpCodes.Ldloc, errorLocal);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(noErrorLabel);
        EmitFunctionCall(funcLocal, argLocals, isTailPosition);
    }

    private void EmitFunctionCall(
        LocalBuilder funcLocal,
        LocalBuilder[] argLocals,
        bool isTailPosition
    )
    {
        _il!.Emit(OpCodes.Ldloc, funcLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyFunctionGetCompiledFunction);

        foreach (var t in argLocals)
            _il.Emit(OpCodes.Ldloc, t);

        if (isTailPosition)
            _il.Emit(OpCodes.Tailcall);

        var invokeMethod = GetFuncDelegateType(argLocals.Length).GetMethod("Invoke");
        _il.Emit(OpCodes.Callvirt, invokeMethod!);

        if (isTailPosition)
            _il.Emit(OpCodes.Ret);
    }

    private static Type GetFuncDelegateType(int paramCount)
    {
        return paramCount switch
        {
            0 => typeof(Func<MonkeyObject>),
            1 => typeof(Func<MonkeyObject, MonkeyObject>),
            2 => typeof(Func<MonkeyObject, MonkeyObject, MonkeyObject>),
            3 => typeof(Func<MonkeyObject, MonkeyObject, MonkeyObject, MonkeyObject>),
            _ => throw new NotSupportedException(
                $"Functions with {paramCount} parameters not yet supported"
            ),
        };
    }

    private static ConstructorInfo GetFuncDelegateConstructor(int paramCount)
    {
        var delegateType = GetFuncDelegateType(paramCount);
        return delegateType.GetConstructors()[0];
    }

    private List<string> AnalyzeFreeVariables(FunctionLiteral functionLiteral)
    {
        var freeVars = new HashSet<string>();
        var boundVars = new HashSet<string>();

        foreach (var param in functionLiteral.Parameters)
            boundVars.Add(param.Value);

        CollectFreeVariables(functionLiteral.Body, boundVars, freeVars, functionLiteral.Name);

        return freeVars.ToList();
    }

    private void CollectFreeVariables(
        Node node,
        HashSet<string> boundVars,
        HashSet<string> freeVars,
        string? functionName = null
    )
    {
        var work = new Stack<(Node? node, string? bindName)>();
        work.Push((node, null));

        while (work.Count > 0)
        {
            var (current, bindName) = work.Pop();

            if (bindName != null)
            {
                boundVars.Add(bindName);
                continue;
            }

            if (current is null)
                continue;

            switch (current)
            {
                case Identifier id:
                    if (!boundVars.Contains(id.Value))
                    {
                        if (functionName != null && id.Value == functionName)
                            freeVars.Add(id.Value);
                        else if (_locals.ContainsKey(id.Value))
                            freeVars.Add(id.Value);
                    }

                    break;

                case LetStatement let:
                    // Preserve original semantics: analyze let.Value before binding let.Name.
                    work.Push((null, let.Name.Value));
                    work.Push((let.Value, null));
                    break;

                case ReturnStatement ret:
                    work.Push((ret.ReturnValue, null));
                    break;

                case ExpressionStatement expr:
                    work.Push((expr.Expression, null));
                    break;

                case BlockStatement block:
                    for (var i = block.Statements.Count - 1; i >= 0; i--)
                        work.Push((block.Statements[i], null));
                    break;

                case PrefixExpression prefix:
                    work.Push((prefix.Right, null));
                    break;

                case InfixExpression infix:
                    work.Push((infix.Right, null));
                    work.Push((infix.Left, null));
                    break;

                case IfExpression ifExpr:
                    if (ifExpr.Alternative != null)
                        work.Push((ifExpr.Alternative, null));
                    work.Push((ifExpr.Consequence, null));
                    work.Push((ifExpr.Condition, null));
                    break;

                case FunctionLiteral:
                    // Nested functions are handled independently by AnalyzeFreeVariables.
                    break;

                case CallExpression call:
                    for (var i = call.Arguments.Count - 1; i >= 0; i--)
                        work.Push((call.Arguments[i], null));
                    work.Push((call.Function, null));
                    break;

                case ArrayLiteral array:
                    for (var i = array.Elements.Count - 1; i >= 0; i--)
                        work.Push((array.Elements[i], null));
                    break;

                case HashLiteral hash:
                {
                    var pairs = hash.Pairs.ToArray();
                    for (var i = pairs.Length - 1; i >= 0; i--)
                    {
                        work.Push((pairs[i].Value, null));
                        work.Push((pairs[i].Key, null));
                    }

                    break;
                }

                case IndexExpression index:
                    work.Push((index.Index, null));
                    work.Push((index.Left, null));
                    break;
            }
        }
    }

    private bool TryGetCapturedVariable(string name, out FieldBuilder? field)
    {
        field = null;
        if (_closureTypeBuilder == null)
            return false;

        return _closureFieldMap.TryGetValue(name, out field);
    }

    private void LoadVariable(string name)
    {
        if (TryGetLocal(name, out var local))
        {
            _il!.Emit(OpCodes.Ldloc, local!);
        }
        else if (_closureContextLocal != null && TryGetCapturedVariable(name, out var field))
        {
            _il!.Emit(OpCodes.Ldloc, _closureContextLocal);
            _il.Emit(OpCodes.Ldfld, field!);
        }
        else
        {
            throw new Exception($"Variable not found: {name}");
        }
    }

    private LocalBuilder EmitErrorCheckAndReturn()
    {
        var valueLocal = _il!.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, valueLocal);
        _il.Emit(OpCodes.Ldloc, valueLocal);
        _il.Emit(OpCodes.Isinst, typeof(MonkeyError));

        var notErrorLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brfalse, notErrorLabel);

        _il.Emit(OpCodes.Ldloc, valueLocal);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(notErrorLabel);

        return valueLocal;
    }

    private static bool IsLiteral(Expression expr)
    {
        return expr is IntegerLiteral or BooleanLiteral or StringLiteral;
    }

    private LocalBuilder StoreInLocal()
    {
        var local = _il!.DeclareLocal(typeof(MonkeyObject));
        _il.Emit(OpCodes.Stloc, local);
        return local;
    }

    private void EmitHashableCheck(LocalBuilder keyLocal)
    {
        _il!.Emit(OpCodes.Ldloc, keyLocal);
        _il.Emit(OpCodes.Isinst, typeof(IHashable));

        var hashableLabel = _il.DefineLabel();
        _il.Emit(OpCodes.Brtrue, hashableLabel);

        _il.Emit(OpCodes.Ldstr, "unusable as hash key: ");
        _il.Emit(OpCodes.Ldloc, keyLocal);
        _il.Emit(OpCodes.Callvirt, CachedMembers.MonkeyObjectTypeName);
        _il.Emit(OpCodes.Call, CachedMembers.StringConcat2);
        _il.Emit(OpCodes.Newobj, CachedMembers.MonkeyErrorCtor);
        _il.Emit(OpCodes.Ret);

        _il.MarkLabel(hashableLabel);
    }

    #endregion
}

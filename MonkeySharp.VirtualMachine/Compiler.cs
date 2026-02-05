using System.Collections.Generic;
using System.Linq;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using MonkeySharp.VirtualMachine.Objects;

namespace MonkeySharp.VirtualMachine;

public class Compiler
{
    private readonly List<Value> _constants;
    private SymbolTable _symbolTable;

    private readonly Stack<CompilationScope> _scopes = new();

    public Compiler()
    {
        _constants = [];
        _symbolTable = new SymbolTable();
        foreach (var (name, i) in Builtins.Keys()) _symbolTable.DefineBuiltin(i, name);
        _scopes.Push(new CompilationScope());
    }

    public Compiler(SymbolTable symbolTable, List<Value> constants)
    {
        _constants = constants;
        _symbolTable = symbolTable;
        _scopes.Push(new CompilationScope());
    }

    private List<byte> CurrentInstructions()
    {
        return _scopes.Peek().Instructions;
    }

    private void EnterScope()
    {
        _scopes.Push(new CompilationScope());
        _symbolTable = new SymbolTable(_symbolTable);
    }

    private List<byte> LeaveScope()
    {
        _symbolTable = _symbolTable.Outer;
        return _scopes.Pop().Instructions;
    }

    public ByteCode ByteCode()
    {
        return new ByteCode(CurrentInstructions().ToArray(), _constants.ToArray());
    }

    public string Compile(Node node)
    {
        return CompileNode(node, false);
    }

    private string CompileNode(Node node, bool isTailPosition)
    {
        switch (node)
        {
            case ProgramNode program:
            {
                foreach (var s in program.Statements)
                {
                    var err = CompileNode(s, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                break;
            }
            case ExpressionStatement expressionStatement:
            {
                var err = CompileNode(expressionStatement.Expression, false);
                if (!string.IsNullOrEmpty(err)) return err;
                Emit(OpCode.Pop);
                break;
            }
            case InfixExpression infixExpression:
            {
                string err;
                if (infixExpression.Operator == "<")
                {
                    err = CompileNode(infixExpression.Right, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                    err = CompileNode(infixExpression.Left, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                    Emit(OpCode.GreaterThan);
                    break;
                }

                err = CompileNode(infixExpression.Left, false);
                if (!string.IsNullOrEmpty(err)) return err;
                err = CompileNode(infixExpression.Right, false);
                if (!string.IsNullOrEmpty(err)) return err;
                switch (infixExpression.Operator)
                {
                    case "+":
                        Emit(OpCode.Add);
                        break;
                    case "-":
                        Emit(OpCode.Subtract);
                        break;
                    case "*":
                        Emit(OpCode.Multiply);
                        break;
                    case "/":
                        Emit(OpCode.Divide);
                        break;
                    case ">":
                        Emit(OpCode.GreaterThan);
                        break;
                    case "==":
                        Emit(OpCode.Equal);
                        break;
                    case "!=":
                        Emit(OpCode.NotEqual);
                        break;
                    default:
                        return $"unknown operator: {infixExpression.Operator}";
                }

                break;
            }
            case IntegerLiteral integerLiteral:
            {
                var integer = Value.Integer(integerLiteral.Value);
                Emit(OpCode.Constant, AddConstant(integer));
                break;
            }
            case BooleanLiteral booleanLiteral:
            {
                Emit(booleanLiteral.Value ? OpCode.True : OpCode.False);
                break;
            }
            case PrefixExpression prefixExpression:
            {
                var err = CompileNode(prefixExpression.Right, false);
                if (!string.IsNullOrEmpty(err)) return err;
                switch (prefixExpression.Operator)
                {
                    case "-":
                        Emit(OpCode.Minus);
                        break;
                    case "!":
                        Emit(OpCode.Bang);
                        break;
                    default:
                        return $"unknown operator: {prefixExpression.Operator}";
                }

                break;
            }
            case IfExpression ifExpression:
            {
                var err = CompileNode(ifExpression.Condition, false);
                if (!string.IsNullOrEmpty(err)) return err;

                var jumpNotTruthyPos = Emit(OpCode.JumpNotTruthy, 9999);

                err = CompileNode(ifExpression.Consequence, isTailPosition);
                if (!string.IsNullOrEmpty(err)) return err;

                if (LastInstructionIs(OpCode.Pop)) RemoveLastPop();

                var jumpPos = Emit(OpCode.Jump, 9999);
                var afterConsequencePos = CurrentInstructions().Count;
                ChangeOperand(jumpNotTruthyPos, afterConsequencePos);


                if (ifExpression.Alternative == null)
                {
                    Emit(OpCode.Null);
                }
                else
                {
                    err = CompileNode(ifExpression.Alternative, isTailPosition);
                    if (!string.IsNullOrEmpty(err)) return err;

                    if (LastInstructionIs(OpCode.Pop)) RemoveLastPop();
                }

                var afterAlternativePos = CurrentInstructions().Count;
                ChangeOperand(jumpPos, afterAlternativePos);

                break;
            }
            case BlockStatement blockStatement:
            {
                for (var i = 0; i < blockStatement.Statements.Count; i++)
                {
                    var statement = blockStatement.Statements[i];
                    var isLastStatement = i == blockStatement.Statements.Count - 1;
                    var err = CompileNode(statement, isTailPosition && isLastStatement);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                break;
            }
            case LetStatement letStatement:
            {
                var symbol = _symbolTable.Define(letStatement.Name.Value);
                var err = CompileNode(letStatement.Value, false);
                if (!string.IsNullOrEmpty(err)) return err;
                Emit(symbol.Scope == SymbolScope.Global ? OpCode.SetGlobal : OpCode.SetLocal, symbol.Index);
                break;
            }
            case Identifier identifier:
            {
                if (!_symbolTable.Resolve(identifier.Value, out var symbol))
                    return $"undefined variable {identifier.Value}";
                LoadSymbol(symbol);
                break;
            }
            case StringLiteral stringLiteral:
            {
                var str = Value.String(stringLiteral.Value);
                Emit(OpCode.Constant, AddConstant(str));
                break;
            }
            case ArrayLiteral arrayLiteral:
            {
                var elements = new List<Value>();
                foreach (var element in arrayLiteral.Elements)
                {
                    var err = CompileNode(element, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                    elements.Add(default);
                }

                Emit(OpCode.Array, elements.Count);
                break;
            }
            case HashLiteral hashLiteral:
            {
                foreach (var key in hashLiteral.Pairs.Keys.OrderBy(k => k.ToString()))
                {
                    var err = CompileNode(key, false);
                    if (!string.IsNullOrEmpty(err)) return err;

                    var value = hashLiteral.Pairs[key];
                    err = CompileNode(value, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                Emit(OpCode.Hash, hashLiteral.Pairs.Count * 2);
                break;
            }
            case IndexExpression indexExpression:
            {
                var err = CompileNode(indexExpression.Left, false);
                if (!string.IsNullOrEmpty(err)) return err;
                err = CompileNode(indexExpression.Index, false);
                if (!string.IsNullOrEmpty(err)) return err;
                Emit(OpCode.Index);
                break;
            }
            case FunctionLiteral functionLiteral:
            {
                EnterScope();
                if (!string.IsNullOrEmpty(functionLiteral.Name))
                    _symbolTable.DefineFunctionName(functionLiteral.Name);
                foreach (var parameter in functionLiteral.Parameters) _symbolTable.Define(parameter.Value);

                // Function body is always in tail position for returns
                var err = CompileNode(functionLiteral.Body, true);
                if (!string.IsNullOrEmpty(err)) return err;

                if (LastInstructionIs(OpCode.Pop))
                    ReplaceLastPopWithReturn();
                if (!LastInstructionIs(OpCode.ReturnValue))
                    Emit(OpCode.Return);

                var freeSymbols = _symbolTable.FreeSymbols;
                var numLocals = _symbolTable.NumDefinitions;
                var instructions = LeaveScope();

                foreach (var symbol in freeSymbols) LoadSymbol(symbol);

                var compiledFn =
                    Value.CompiledFunction(instructions.ToArray(), numLocals, functionLiteral.Parameters.Count);
                var fnIndex = AddConstant(compiledFn);
                Emit(OpCode.Closure, fnIndex, freeSymbols.Count);
                break;
            }
            case ReturnStatement returnStatement:
            {
                // Check if return value is a call expression in tail position
                if (isTailPosition && returnStatement.ReturnValue is CallExpression tailCallExpr)
                {
                    // Compile function and arguments
                    var err = CompileNode(tailCallExpr.Function, false);
                    if (!string.IsNullOrEmpty(err)) return err;

                    foreach (var argument in tailCallExpr.Arguments)
                    {
                        err = CompileNode(argument, false);
                        if (!string.IsNullOrEmpty(err)) return err;
                    }

                    // Emit tail call instead of regular call
                    Emit(OpCode.TailCall, tailCallExpr.Arguments.Count);
                }
                else
                {
                    // Regular return
                    var err = CompileNode(returnStatement.ReturnValue, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                    Emit(OpCode.ReturnValue);
                }

                break;
            }
            case CallExpression callExpression:
            {
                var err = CompileNode(callExpression.Function, false);
                if (!string.IsNullOrEmpty(err)) return err;
                foreach (var argument in callExpression.Arguments)
                {
                    err = CompileNode(argument, false);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                Emit(OpCode.Call, callExpression.Arguments.Count);
                break;
            }
        }

        return null;
    }

    private void LoadSymbol(Symbol symbol)
    {
        switch (symbol.Scope)
        {
            case SymbolScope.Global:
                Emit(OpCode.GetGlobal, symbol.Index);
                break;
            case SymbolScope.Local:
                Emit(OpCode.GetLocal, symbol.Index);
                break;
            case SymbolScope.Builtin:
                Emit(OpCode.GetBuiltin, symbol.Index);
                break;
            case SymbolScope.Free:
                Emit(OpCode.GetFree, symbol.Index);
                break;
            case SymbolScope.Function:
                Emit(OpCode.CurrentClosure);
                break;
        }
    }

    private void ReplaceLastPopWithReturn()
    {
        var lastPos = _scopes.Peek().LastInstruction.Position;
        ReplaceInstruction(lastPos, Code.Make(OpCode.ReturnValue));
        _scopes.Peek().LastInstruction = new EmittedInstruction(OpCode.ReturnValue, lastPos);
    }

    private void RemoveLastPop()
    {
        CurrentInstructions().RemoveRange(_scopes.Peek().LastInstruction.Position,
            CurrentInstructions().Count - _scopes.Peek().LastInstruction.Position);
        _scopes.Peek().LastInstruction = _scopes.Peek().PreviousInstruction;
    }

    private bool LastInstructionIs(OpCode op)
    {
        if (CurrentInstructions().Count == 0) return false;
        return _scopes.Peek().LastInstruction.OpCode == op;
    }

    private void ReplaceInstruction(int pos, byte[] newInstruction)
    {
        for (var i = 0; i < newInstruction.Length; i++)
            CurrentInstructions()[pos + i] = newInstruction[i];
    }

    private void ChangeOperand(int opPos, int operand)
    {
        var op = (OpCode) CurrentInstructions()[opPos];
        var newInstruction = Code.Make(op, operand);
        ReplaceInstruction(opPos, newInstruction);
    }

    private void SetLastInstruction(OpCode op, int operand)
    {
        _scopes.Peek().PreviousInstruction = _scopes.Peek().LastInstruction;
        _scopes.Peek().LastInstruction = new EmittedInstruction(op, operand);
    }

    private int AddConstant(Value obj)
    {
        _constants.Add(obj);
        return _constants.Count - 1;
    }

    private int Emit(OpCode op, params int[] operands)
    {
        var ins = Code.Make(op, operands);
        var pos = AddInstruction(ins);
        SetLastInstruction(op, pos);
        return pos;
    }

    private int AddInstruction(byte[] ins)
    {
        var posNewInstruction = CurrentInstructions().Count;
        CurrentInstructions().AddRange(ins);
        return posNewInstruction;
    }
}
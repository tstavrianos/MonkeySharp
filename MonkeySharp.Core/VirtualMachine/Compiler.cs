using System.Collections.Generic;
using System.Linq;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Objects;

namespace MonkeySharp.Core.VirtualMachine;

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
        switch (node)
        {
            case ProgramNode program:
            {
                foreach (var s in program.Statements)
                {
                    var err = Compile(s);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                break;
            }
            case ExpressionStatement expressionStatement:
            {
                var err = Compile(expressionStatement.Expression);
                if (!string.IsNullOrEmpty(err)) return err;
                Emit(OpCode.Pop);
                break;
            }
            case InfixExpression infixExpression:
            {
                string err;
                if (infixExpression.Operator == "<")
                {
                    err = Compile(infixExpression.Right);
                    if (!string.IsNullOrEmpty(err)) return err;
                    err = Compile(infixExpression.Left);
                    if (!string.IsNullOrEmpty(err)) return err;
                    Emit(OpCode.GreaterThan);
                    break;
                }

                err = Compile(infixExpression.Left);
                if (!string.IsNullOrEmpty(err)) return err;
                err = Compile(infixExpression.Right);
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
                var err = Compile(prefixExpression.Right);
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
                var err = Compile(ifExpression.Condition);
                if (!string.IsNullOrEmpty(err)) return err;

                var jumpNotTruthyPos = Emit(OpCode.JumpNotTruthy, 9999);

                err = Compile(ifExpression.Consequence);
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
                    err = Compile(ifExpression.Alternative);
                    if (!string.IsNullOrEmpty(err)) return err;

                    if (LastInstructionIs(OpCode.Pop)) RemoveLastPop();
                }

                var afterAlternativePos = CurrentInstructions().Count;
                ChangeOperand(jumpPos, afterAlternativePos);

                break;
            }
            case BlockStatement blockStatement:
            {
                foreach (var s in blockStatement.Statements)
                {
                    var err = Compile(s);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                break;
            }
            case LetStatement letStatement:
            {
                var symbol = _symbolTable.Define(letStatement.Name.Value);
                var err = Compile(letStatement.Value);
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
                    var err = Compile(element);
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
                    var err = Compile(key);
                    if (!string.IsNullOrEmpty(err)) return err;

                    var value = hashLiteral.Pairs[key];
                    err = Compile(value);
                    if (!string.IsNullOrEmpty(err)) return err;
                }

                Emit(OpCode.Hash, hashLiteral.Pairs.Count * 2);
                break;
            }
            case IndexExpression indexExpression:
            {
                var err = Compile(indexExpression.Left);
                if (!string.IsNullOrEmpty(err)) return err;
                err = Compile(indexExpression.Index);
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
                var err = Compile(functionLiteral.Body);
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
                var err = Compile(returnStatement.ReturnValue);
                if (!string.IsNullOrEmpty(err)) return err;
                Emit(OpCode.ReturnValue);
                break;
            }
            case CallExpression callExpression:
            {
                var err = Compile(callExpression.Function);
                if (!string.IsNullOrEmpty(err)) return err;
                foreach (var argument in callExpression.Arguments)
                {
                    err = Compile(argument);
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
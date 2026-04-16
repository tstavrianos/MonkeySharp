using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonkeySharp.VirtualMachine;

internal static class Code
{
    private static readonly Dictionary<OpCode, Definition> Definitions = new()
    {
        { OpCode.Constant, new Definition("OpConstant", [2]) },
        { OpCode.Add, new Definition("OpAdd", []) },
        { OpCode.Pop, new Definition("OpPop", []) },
        { OpCode.Subtract, new Definition("OpSub", []) },
        { OpCode.Multiply, new Definition("OpMul", []) },
        { OpCode.Divide, new Definition("OpDiv", []) },
        { OpCode.True, new Definition("OpTrue", []) },
        { OpCode.False, new Definition("OpFalse", []) },
        { OpCode.Equal, new Definition("OpEqual", []) },
        { OpCode.NotEqual, new Definition("OpNotEqual", []) },
        { OpCode.GreaterThan, new Definition("OpGreaterThan", []) },
        { OpCode.Minus, new Definition("OpMinus", []) },
        { OpCode.Bang, new Definition("OpBang", []) },
        { OpCode.JumpNotTruthy, new Definition("OpJumpNotTruthy", [2]) },
        { OpCode.Jump, new Definition("OpJump", [2]) },
        { OpCode.Null, new Definition("OpNull", []) },
        { OpCode.SetGlobal, new Definition("OpSetGlobal", [2]) },
        { OpCode.GetGlobal, new Definition("OpGetGlobal", [2]) },
        { OpCode.Array, new Definition("OpArray", [2]) },
        { OpCode.Hash, new Definition("OpHash", [2]) },
        { OpCode.Index, new Definition("OpIndex", []) },
        { OpCode.Call, new Definition("OpCall", [1]) },
        { OpCode.ReturnValue, new Definition("OpReturnValue", []) },
        { OpCode.Return, new Definition("OpReturn", []) },
        { OpCode.GetLocal, new Definition("OpGetLocal", [1]) },
        { OpCode.SetLocal, new Definition("OpSetLocal", [1]) },
        { OpCode.GetBuiltin, new Definition("OpGetBuiltin", [1]) },
        { OpCode.Closure, new Definition("OpClosure", [2, 1]) },
        { OpCode.GetFree, new Definition("OpGetFree", [1]) },
        { OpCode.CurrentClosure, new Definition("OpCurrentClosure", []) },
        { OpCode.TailCall, new Definition("OpTailCall", [1]) },
    };

    public static string? LookUp(OpCode op, out Definition ret)
    {
        if (!Definitions.TryGetValue(op, out ret))
            return $"opcode {op} undefined";

        return null;
    }

    public static byte[] Make(OpCode opCode, params int[] operands)
    {
        if (!Definitions.TryGetValue(opCode, out var def))
            return [];

        var instructionLen = 1;

        foreach (var w in def.OperandWidths)
            instructionLen += w;

        var instruction = new byte[instructionLen];
        instruction[0] = (byte)opCode;

        var offset = 1;
        for (var i = 0; i < operands.Length; i++)
        {
            var o = operands[i];
            var width = def.OperandWidths[i];
            switch (width)
            {
                case 2:
                    BinaryPrimitives.WriteUInt16BigEndian(instruction.AsSpan(offset), (ushort)o);
                    break;
                case 1:
                    instruction[offset] = (byte)o;
                    break;
            }

            offset += width;
        }

        return instruction;
    }

    public static (int[], int) ReadOperands(Definition def, byte[] instructions)
    {
        var operands = new int[def.OperandWidths.Length];
        var offset = 0;

        for (var i = 0; i < def.OperandWidths.Length; i++)
        {
            var width = def.OperandWidths[i];
            switch (width)
            {
                case 2:
                    operands[i] = BinaryPrimitives.ReadUInt16BigEndian(instructions.AsSpan(offset));
                    break;
                case 1:
                    operands[i] = instructions[offset];
                    break;
            }

            offset += width;
        }

        return (operands, offset);
    }

    public static string ToString(IReadOnlyList<byte> instruction)
    {
        var ss = new StringBuilder();
        var i = 0;
        for (; i < instruction.Count; )
        {
            var err = LookUp((OpCode)instruction[i], out var def);
            if (!string.IsNullOrEmpty(err))
            {
                ss.AppendLine($"ERROR: {err}");
                continue;
            }

            var (operands, read) = ReadOperands(def, instruction.Skip(i + 1).ToArray());
            ss.AppendLine($"{i:D4} {FormatInstruction(def, operands)}");
            i += 1 + read;
        }

        return ss.ToString();
    }

    private static string FormatInstruction(Definition def, int[] operands)
    {
        var operandCount = def.OperandWidths.Length;

        if (operands.Length != operandCount)
            return $"ERROR: operand len {operands.Length} does not match defined {operandCount}";

        switch (operandCount)
        {
            case 0:
                return $"{def.Name}";
            case 1:
                return $"{def.Name} {operands[0]}";
            case 2:
                return $"{def.Name} {operands[0]} {operands[1]}";
        }

        return $"ERROR: unhandled operandCount for {def.Name}";
    }
}

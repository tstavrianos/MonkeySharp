using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Interpreter;
using MonkeySharp.Core.Objects;
using MonkeySharp.Core.VirtualMachine;
using NUnit.Framework;

namespace MonkeySharp.Tests;

internal static class TestCommon
{
    internal static ProgramNode Parse(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!CheckParserErrors(p, out var message))
        {
            Assert.Fail(message);
            return null;
        }

        return program;
    }

    internal static bool TestInstructions(byte[][] expected, IReadOnlyList<byte> actual, out string errorMessage)
    {
        errorMessage = string.Empty;
        var concatted = Concat(expected);

        if (actual.Count != concatted.Length)
        {
            errorMessage =
                $"wrong instructions length. want={Code.ToString(concatted)}, got={Code.ToString(actual)}";
            return false;
        }

        for (var i = 0; i < concatted.Length; i++)
            if (concatted[i] != actual[i])
            {
                errorMessage =
                    $"wrong instruction at pos {i}. want={(int) concatted[i]}, got={(int) actual[i]}. \nwant:\n{Code.ToString(concatted)}\ngot:\n{Code.ToString(actual)}";
                return false;
            }

        return true;
    }

    private static T[] Concat<T>(T[][] source)
    {
        if (source == null || source.Length == 0) return [];
        IEnumerable<T> en = source[0];
        for (var i = 1; i < source.Length; i++) en = en.Concat(source[i]);

        return en.ToArray();
    }

    internal static Value Eval(string input)
    {
        var env = new Environment();
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        var evaluator = new Evaluator();
        return evaluator.Eval(program, env);
    }

    internal static bool CheckParserErrors(Parser p, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (p.Errors.Count == 0) return true;
        var sb = new StringBuilder();
        sb.AppendLine($"parser has {p.Errors.Count} errors");
        foreach (var error in p.Errors)
            sb.AppendLine($"parser error: {error}");
        errorMessage = sb.ToString();
        return false;
    }

    internal static bool TestValue(Value obj, object expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (expected == null)
            return TestNullValue(obj, out errorMessage);
        switch (expected)
        {
            case long l:
                return TestIntegerValue(obj, l, out errorMessage);
            case int i:
                return TestIntegerValue(obj, i, out errorMessage);
            case bool b:
                return TestBooleanValue(obj, b, out errorMessage);
            case string s:
                return TestStringValue(obj, s, out errorMessage);
            case byte[][] instructions:
                return TestCompiledFunctionValue(obj, instructions, out errorMessage);
            case object[] o:
                return TestArrayValue(obj, o, out errorMessage);
            case IReadOnlyDictionary<HashKey, object> d:
                return TestHashValue(obj, d, out errorMessage);
            default:
                errorMessage = $"type of object not handled. got={expected.GetType().Name}";
                return false;
        }
    }

    private static bool TestCompiledFunctionValue(Value obj, byte[][] instructions, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsCompiledFunction)
        {
            errorMessage = $"object is not CompiledFunctionObject. got={obj.Type}";
            return false;
        }

        return TestInstructions(instructions, obj.CompiledFunctionData.Instructions, out errorMessage);
    }

    private static bool TestNullValue(Value obj, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsNull)
        {
            errorMessage = $"object is not NullObject. got={obj.Type}";
            return false;
        }

        return true;
    }

    private static bool TestHashValue(Value obj, IReadOnlyDictionary<HashKey, object> dictionary,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsHash)
        {
            errorMessage = $"object is not HashObject. got={obj.Type}";
            return false;
        }

        if (obj.HashPairs.Count != dictionary.Count)
        {
            errorMessage = $"wrong num of elements. want={dictionary.Count}, got={obj.HashPairs.Count}";
            return false;
        }

        foreach (var (key, value) in dictionary)
            if (!TestValue(obj.HashPairs[key].Value, value, out errorMessage))
                return false;

        return true;
    }

    private static bool TestArrayValue(Value obj, object[] objects, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsArray)
        {
            errorMessage = $"object is not ArrayObject. got={obj.Type}";
            return false;
        }

        if (obj.ArrayElements.Count != objects.Length)
        {
            errorMessage = $"wrong num of elements. want={objects.Length}, got={obj.ArrayElements.Count}";
            return false;
        }

        for (var i = 0; i < obj.ArrayElements.Count; i++)
            if (!TestValue(obj.ArrayElements[i], objects[i], out errorMessage))
                return false;

        return true;
    }

    private static bool TestIntegerValue(Value obj, long expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsInteger)
        {
            errorMessage = $"object is not IntegerObject. got={obj.Type}";
            return false;
        }

        if (obj.IntValue != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={obj.IntValue}";
            return false;
        }

        return true;
    }

    private static bool TestStringValue(Value obj, string expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsString)
        {
            errorMessage = $"object is not StringObject. got={obj.Type}";
            return false;
        }

        if (obj.StringValue != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={obj.StringValue}";
            return false;
        }

        return true;
    }

    private static bool TestBooleanValue(Value obj, bool expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!obj.IsBoolean)
        {
            errorMessage = $"object is not BooleanObject. got={obj.Type}";
            return false;
        }

        if (obj.BooleanValue != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={obj.BooleanValue}";
            return false;
        }

        return true;
    }
}
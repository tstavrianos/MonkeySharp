using System.Collections.Generic;
using System.Text;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.TreeWalk;
using MonkeySharp.TreeWalk.Objects;

namespace MonkeySharp.Tests;

internal static class TestTreeWalkCommon
{
    internal static Value Eval(string input)
    {
        var program = TestCommon.Parse(input);
        var env = new SymbolTable();
        return TreeWalker.Eval(program, env);
    }

    internal static bool CheckParserErrors(Parser p, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (p.Errors.Count == 0)
            return true;
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
            case object[] o:
                return TestArrayValue(obj, o, out errorMessage);
            case IReadOnlyDictionary<object, object> d:
                return TestHashValue(obj, d, out errorMessage);
            default:
                errorMessage = $"type of object not handled. got={expected.GetType().Name}";
                return false;
        }
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

    private static bool TestHashValue(
        Value obj,
        IReadOnlyDictionary<object, object> dictionary,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (!obj.IsHash)
        {
            errorMessage = $"object is not HashObject. got={obj.Type}";
            return false;
        }

        if (obj.HashPairs!.Count != dictionary.Count)
        {
            errorMessage =
                $"wrong num of elements. want={dictionary.Count}, got={obj.HashPairs.Count}";
            return false;
        }

        var notMatched = new List<Value>();
        foreach (var (_, pair) in obj.HashPairs)
        {
            var found = false;
            foreach (var (key, value) in dictionary)
                if (TestValue(pair.Key, key, out _) && TestValue(pair.Value, value, out _))
                {
                    found = true;
                    break;
                }

            if (!found)
                notMatched.Add(pair.Key);
        }

        if (notMatched.Count > 0)
        {
            errorMessage = $"Not matched keys: {string.Join(',', notMatched)}";
            return false;
        }

        /*foreach (var (key, value) in dictionary)
            if (!TestValue(obj.HashPairs[key].Value, value, out errorMessage))
                return false;*/

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

        if (obj.ArrayElements!.Count != objects.Length)
        {
            errorMessage =
                $"wrong num of elements. want={objects.Length}, got={obj.ArrayElements.Count}";
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

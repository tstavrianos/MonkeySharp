using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.ReflectionEmit;

namespace MonkeySharp.Tests;

internal static class TestReflectionEmitCommon
{
    internal static MonkeyObject Eval(string input)
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        var compiler = new ReflectionEmitCompiler();
        var executable = compiler.Compile(program);
        var result = executable();
        return result;
    }

    internal static bool TestValue(MonkeyObject obj, object expected, out string errorMessage)
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

    private static bool TestNullValue(MonkeyObject obj, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyNull)
        {
            errorMessage = $"object is not NullObject. got={obj.TypeName()}";
            return false;
        }

        return true;
    }

    private static bool TestHashValue(
        MonkeyObject obj,
        IReadOnlyDictionary<object, object> dictionary,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyHash hash)
        {
            errorMessage = $"object is not HashObject. got={obj.TypeName()}";
            return false;
        }

        if (hash.Pairs.Count != dictionary.Count)
        {
            errorMessage =
                $"wrong num of elements. want={dictionary.Count}, got={hash.Pairs.Count}";
            return false;
        }

        var notMatched = new List<MonkeyObject>();
        foreach (var (hKey, hValue) in hash.Pairs)
        {
            var found = false;
            foreach (var (key, value) in dictionary)
                if (TestValue(hKey, key, out _) && TestValue(hValue, value, out _))
                {
                    found = true;
                    break;
                }

            if (!found)
                notMatched.Add(hKey);
        }

        if (notMatched.Count > 0)
        {
            errorMessage = $"Not matched keys: {string.Join(',', notMatched)}";
            return false;
        }
        /*foreach (var (key, value) in dictionary)
            if (!TestValue(hash.Pairs[key], value, out errorMessage))
                return false;*/

        return true;
    }

    private static bool TestArrayValue(MonkeyObject obj, object[] objects, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyArray array)
        {
            errorMessage = $"object is not ArrayObject. got={obj.TypeName()}";
            return false;
        }

        if (array.Elements.Length != objects.Length)
        {
            errorMessage =
                $"wrong num of elements. want={objects.Length}, got={array.Elements.Length}";
            return false;
        }

        for (var i = 0; i < array.Elements.Length; i++)
            if (!TestValue(array.Elements[i], objects[i], out errorMessage))
                return false;

        return true;
    }

    private static bool TestIntegerValue(MonkeyObject obj, long expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyInteger integer)
        {
            errorMessage = $"object is not IntegerObject. got={obj.TypeName()}";
            return false;
        }

        if (integer.Value != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={integer.Value}";
            return false;
        }

        return true;
    }

    private static bool TestStringValue(MonkeyObject obj, string expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyString str)
        {
            errorMessage = $"object is not StringObject. got={obj.TypeName()}";
            return false;
        }

        if (str.Value != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={str.Value}";
            return false;
        }

        return true;
    }

    private static bool TestBooleanValue(MonkeyObject obj, bool expected, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (obj is not MonkeyBoolean boolean)
        {
            errorMessage = $"object is not BooleanObject. got={obj.TypeName()}";
            return false;
        }

        if (boolean.Value != expected)
        {
            errorMessage = $"object has wrong value. expected={expected}, got={boolean.Value}";
            return false;
        }

        return true;
    }
}

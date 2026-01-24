using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Interpreter;
using MonkeySharp.Core.Objects;
using MonkeySharp.Core.VirtualMachine;
using NUnit.Framework;
using Environment = MonkeySharp.Core.Interpreter.Environment;

namespace MonkeySharp.Tests
{
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

        internal static IObject Eval(string input)
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

        internal static bool TestObject(IObject obj, object expected, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (expected == null)
                return TestNullObject(obj, out errorMessage);
            switch (expected)
            {
                case long l:
                    return TestIntegerObject(obj, l, out errorMessage);
                case int i:
                    return TestIntegerObject(obj, i, out errorMessage);
                case bool b:
                    return TestBooleanObject(obj, b, out errorMessage);
                case string s:
                    return TestStringObject(obj, s, out errorMessage);
                case byte[][] instructions:
                    return TestCompiledFunctionObject(obj, instructions, out errorMessage);
                case object[] o:
                    return TestArrayObject(obj, o, out errorMessage);
                case IReadOnlyDictionary<HashKey, object> d:
                    return TestHashObject(obj, d, out errorMessage);
                default:
                    errorMessage = $"type of object not handled. got={expected.GetType().Name}";
                    return false;
            }
        }

        private static bool TestCompiledFunctionObject(IObject obj, byte[][] instructions, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not CompiledFunctionObject o)
            {
                errorMessage = $"object is not CompiledFunctionObject. got={obj.Type}";
                return false;
            }

            return TestInstructions(instructions, o.Instructions, out errorMessage);
        }

        private static bool TestNullObject(IObject obj, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not NullObject)
            {
                errorMessage = $"object is not NullObject. got={obj.GetType()}";
                return false;
            }

            return true;
        }

        private static bool TestHashObject(IObject obj, IReadOnlyDictionary<HashKey, object> dictionary,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not HashObject o)
            {
                errorMessage = $"object is not HashObject. got={obj.Type}";
                return false;
            }

            if (o.Pairs.Count != dictionary.Count)
            {
                errorMessage = $"wrong num of elements. want={dictionary.Count}, got={o.Pairs.Count}";
                return false;
            }

            foreach (var (key, value) in dictionary)
                if (!TestObject(o.Pairs[key].Value, value, out errorMessage))
                    return false;

            return true;
        }

        private static bool TestArrayObject(IObject obj, object[] objects, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not ArrayObject o)
            {
                errorMessage = $"object is not ArrayObject. got={obj.Type}";
                return false;
            }

            if (o.Elements.Count != objects.Length)
            {
                errorMessage = $"wrong num of elements. want={objects.Length}, got={o.Elements.Count}";
                return false;
            }

            for (var i = 0; i < o.Elements.Count; i++)
                if (!TestObject(o.Elements[i], objects[i], out errorMessage))
                    return false;

            return true;
        }

        private static bool TestIntegerObject(IObject obj, long expected, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not IntegerObject integerObject)
            {
                errorMessage = $"object is not IntegerObject. got={obj.GetType()}";
                return false;
            }

            if (integerObject.Value != expected)
            {
                errorMessage = $"object has wrong value. expected={expected}, got={integerObject.Value}";
                return false;
            }

            return true;
        }

        private static bool TestStringObject(IObject obj, string expected, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not StringObject stringObject)
            {
                errorMessage = $"object is not StringObject. got={obj.GetType()}";
                return false;
            }

            if (stringObject.Value != expected)
            {
                errorMessage = $"object has wrong value. expected={expected}, got={stringObject.Value}";
                return false;
            }

            return true;
        }

        private static bool TestBooleanObject(IObject obj, bool expected, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (obj is not BooleanObject booleanObject)
            {
                errorMessage = $"object is not BooleanObject. got={obj.GetType()}";
                return false;
            }

            if (booleanObject.Value != expected)
            {
                errorMessage = $"object has wrong value. expected={expected}, got={booleanObject.Value}";
                return false;
            }

            return true;
        }
    }
}
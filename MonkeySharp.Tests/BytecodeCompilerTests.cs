using System;
using System.Collections.Generic;
using MonkeySharp.BytecodeVm;
using MonkeySharp.BytecodeVm.Objects;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class BytecodeCompilerTests
{
    private static readonly object[] TestCompilerConstantCases =
    [
        new object[]
        {
            "1 + 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Add),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1 - 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Subtract),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1 * 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Multiply),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "2 / 1",
            new object[] { 2, 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Divide),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1;2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Pop),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "true",
            new object[] { },
            new[] { Code.Make(OpCode.True), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "false",
            new object[] { },
            new[] { Code.Make(OpCode.False), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "1 > 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.GreaterThan),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1 < 2",
            new object[] { 2, 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.GreaterThan),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1 == 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Equal),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "1 != 2",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.NotEqual),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "true == false",
            new object[] { },
            new[]
            {
                Code.Make(OpCode.True),
                Code.Make(OpCode.False),
                Code.Make(OpCode.Equal),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "true != false",
            new object[] { },
            new[]
            {
                Code.Make(OpCode.True),
                Code.Make(OpCode.False),
                Code.Make(OpCode.NotEqual),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "-1",
            new object[] { 1 },
            new[] { Code.Make(OpCode.Constant, 0), Code.Make(OpCode.Minus), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "!true",
            Array.Empty<object>(),
            new[] { Code.Make(OpCode.True), Code.Make(OpCode.Bang), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "if (true) { 10 }; 3333;",
            new object[] { 10, 3333 },
            new[]
            {
                Code.Make(OpCode.True),
                Code.Make(OpCode.JumpNotTruthy, 10),
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Jump, 11),
                Code.Make(OpCode.Null),
                Code.Make(OpCode.Pop),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "if (true) { 10 } else { 20 }; 3333;",
            new object[] { 10, 20, 3333 },
            new[]
            {
                Code.Make(OpCode.True),
                Code.Make(OpCode.JumpNotTruthy, 10),
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Jump, 13),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Pop),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let one = 1; let two = 2;",
            new object[] { 1, 2 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.SetGlobal, 1),
            },
        },
        new object[]
        {
            "let one = 1; one;",
            new object[] { 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.GetGlobal, 0),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let one = 1; let two = one; two;",
            new object[] { 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.GetGlobal, 0),
                Code.Make(OpCode.SetGlobal, 1),
                Code.Make(OpCode.GetGlobal, 1),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "\"monkey\"",
            new object[] { "monkey" },
            new[] { Code.Make(OpCode.Constant, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "\"mon\"+\"key\"",
            new object[] { "mon", "key" },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Add),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "[]",
            Array.Empty<object>(),
            new[] { Code.Make(OpCode.Array, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "[1, 2, 3]",
            new object[] { 1, 2, 3 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Array, 3),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "[1 + 2, 3 - 4, 5 * 6]",
            new object[] { 1, 2, 3, 4, 5, 6 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Add),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Subtract),
                Code.Make(OpCode.Constant, 4),
                Code.Make(OpCode.Constant, 5),
                Code.Make(OpCode.Multiply),
                Code.Make(OpCode.Array, 3),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "{}",
            Array.Empty<object>(),
            new[] { Code.Make(OpCode.Hash, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "{1: 2, 3: 4, 5: 6}",
            new object[] { 1, 2, 3, 4, 5, 6 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Constant, 4),
                Code.Make(OpCode.Constant, 5),
                Code.Make(OpCode.Hash, 6),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "{1: 2 + 3, 4: 5 * 6}",
            new object[] { 1, 2, 3, 4, 5, 6 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Add),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Constant, 4),
                Code.Make(OpCode.Constant, 5),
                Code.Make(OpCode.Multiply),
                Code.Make(OpCode.Hash, 4),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "[1, 2, 3][1 + 1]",
            new object[] { 1, 2, 3, 1, 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Array, 3),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Constant, 4),
                Code.Make(OpCode.Add),
                Code.Make(OpCode.Index),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "{1: 2}[2 - 1]",
            new object[] { 1, 2, 2, 1 },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Hash, 2),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Subtract),
                Code.Make(OpCode.Index),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "fn() {return 5+10}",
            new object[]
            {
                5,
                10,
                new[]
                {
                    Code.Make(OpCode.Constant, 0),
                    Code.Make(OpCode.Constant, 1),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 2, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn() {5+10}",
            new object[]
            {
                5,
                10,
                new[]
                {
                    Code.Make(OpCode.Constant, 0),
                    Code.Make(OpCode.Constant, 1),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 2, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn() {1;2}",
            new object[]
            {
                1,
                2,
                new[]
                {
                    Code.Make(OpCode.Constant, 0),
                    Code.Make(OpCode.Pop),
                    Code.Make(OpCode.Constant, 1),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 2, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn() {}",
            new object[] { new[] { Code.Make(OpCode.Return) } },
            new[] { Code.Make(OpCode.Closure, 0, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn() {24}();",
            new object[]
            {
                24,
                new[] { Code.Make(OpCode.Constant, 0), Code.Make(OpCode.ReturnValue) },
            },
            new[]
            {
                Code.Make(OpCode.Closure, 1, 0),
                Code.Make(OpCode.Call, 0),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let noArg = fn() { 24 }; noArg();",
            new object[]
            {
                24,
                new[] { Code.Make(OpCode.Constant, 0), Code.Make(OpCode.ReturnValue) },
            },
            new[]
            {
                Code.Make(OpCode.Closure, 1, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.GetGlobal, 0),
                Code.Make(OpCode.Call, 0),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let num = 55; fn() { num }",
            new object[]
            {
                55,
                new[] { Code.Make(OpCode.GetGlobal, 0), Code.Make(OpCode.ReturnValue) },
            },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.Closure, 1, 0),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let num = 55; fn() { num }",
            new object[]
            {
                55,
                new[] { Code.Make(OpCode.GetGlobal, 0), Code.Make(OpCode.ReturnValue) },
            },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.Closure, 1, 0),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "fn() { let num = 55; num }",
            new object[]
            {
                55,
                new[]
                {
                    Code.Make(OpCode.Constant, 0),
                    Code.Make(OpCode.SetLocal, 0),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 1, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn() { let a = 55; let b = 77; a + b }",
            new object[]
            {
                55,
                77,
                new[]
                {
                    Code.Make(OpCode.Constant, 0),
                    Code.Make(OpCode.SetLocal, 0),
                    Code.Make(OpCode.Constant, 1),
                    Code.Make(OpCode.SetLocal, 1),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.GetLocal, 1),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 2, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "let oneArg = fn(a) { a }; oneArg(24);",
            new object[]
            {
                new[] { Code.Make(OpCode.GetLocal, 0), Code.Make(OpCode.ReturnValue) },
                24,
            },
            new[]
            {
                Code.Make(OpCode.Closure, 0, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.GetGlobal, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Call, 1),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "let manyArg = fn(a, b, c) { a; b; c }; manyArg(24, 25, 26);",
            new object[]
            {
                new[]
                {
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Pop),
                    Code.Make(OpCode.GetLocal, 1),
                    Code.Make(OpCode.Pop),
                    Code.Make(OpCode.GetLocal, 2),
                    Code.Make(OpCode.ReturnValue),
                },
                24,
                25,
                26,
            },
            new[]
            {
                Code.Make(OpCode.Closure, 0, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.GetGlobal, 0),
                Code.Make(OpCode.Constant, 1),
                Code.Make(OpCode.Constant, 2),
                Code.Make(OpCode.Constant, 3),
                Code.Make(OpCode.Call, 3),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "len([]); push([], 1);",
            new object[] { 1 },
            new[]
            {
                Code.Make(OpCode.GetBuiltin, 0),
                Code.Make(OpCode.Array, 0),
                Code.Make(OpCode.Call, 1),
                Code.Make(OpCode.Pop),
                Code.Make(OpCode.GetBuiltin, 4),
                Code.Make(OpCode.Array, 0),
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.Call, 2),
                Code.Make(OpCode.Pop),
            },
        },
        new object[]
        {
            "fn() {len([])}",
            new object[]
            {
                new[]
                {
                    Code.Make(OpCode.GetBuiltin, 0),
                    Code.Make(OpCode.Array, 0),
                    Code.Make(OpCode.Call, 1),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 0, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn(a) { fn(b) { a + b } }",
            new object[]
            {
                new[]
                {
                    Code.Make(OpCode.GetFree, 0),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
                new[]
                {
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Closure, 0, 1),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 1, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "fn(a) { fn(b) { fn(c) { a + b + c } } }",
            new object[]
            {
                new[]
                {
                    Code.Make(OpCode.GetFree, 0),
                    Code.Make(OpCode.GetFree, 1),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
                new[]
                {
                    Code.Make(OpCode.GetFree, 0),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Closure, 0, 2),
                    Code.Make(OpCode.ReturnValue),
                },
                new[]
                {
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Closure, 1, 1),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[] { Code.Make(OpCode.Closure, 2, 0), Code.Make(OpCode.Pop) },
        },
        new object[]
        {
            "let global = 55;fn() {let a = 66;fn() {let b = 77;fn() {let c = 88;global + a + b + c;}}}",
            new object[]
            {
                55,
                66,
                77,
                88,
                new[]
                {
                    Code.Make(OpCode.Constant, 3),
                    Code.Make(OpCode.SetLocal, 0),
                    Code.Make(OpCode.GetGlobal, 0),
                    Code.Make(OpCode.GetFree, 0),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.GetFree, 1),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Add),
                    Code.Make(OpCode.ReturnValue),
                },
                new[]
                {
                    Code.Make(OpCode.Constant, 2),
                    Code.Make(OpCode.SetLocal, 0),
                    Code.Make(OpCode.GetFree, 0),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Closure, 4, 2),
                    Code.Make(OpCode.ReturnValue),
                },
                new[]
                {
                    Code.Make(OpCode.Constant, 1),
                    Code.Make(OpCode.SetLocal, 0),
                    Code.Make(OpCode.GetLocal, 0),
                    Code.Make(OpCode.Closure, 5, 1),
                    Code.Make(OpCode.ReturnValue),
                },
            },
            new[]
            {
                Code.Make(OpCode.Constant, 0),
                Code.Make(OpCode.SetGlobal, 0),
                Code.Make(OpCode.Closure, 6, 0),
                Code.Make(OpCode.Pop),
            },
        },
    ];

    private static bool TestConstants(
        object[] expected,
        IReadOnlyList<Value> actual,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (actual.Count != expected.Length)
        {
            errorMessage = $"wrong number of constants. want={expected.Length}, got={actual.Count}";
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
            if (!TestBytecodeVmCommon.TestValue(actual[i], expected[i], out errorMessage))
                return false;

        return true;
    }

    [Test]
    [TestCaseSource(nameof(TestCompilerConstantCases))]
    public void TestCompilerConstant(
        string input,
        object[] expectedConstants,
        byte[][] expectedInstructions
    )
    {
        var program = TestCommon.Parse(input);
        var compiler = new BytecodeCompiler();
        var err = compiler.Compile(program);
        if (!string.IsNullOrEmpty(err))
        {
            Assert.Fail($"compiler error: {err}");
            return;
        }

        var bytecode = compiler.ByteCode();

        if (
            !TestBytecodeVmCommon.TestInstructions(
                expectedInstructions,
                bytecode.Instructions,
                out err
            )
        )
        {
            Assert.Fail(err);
            return;
        }

        if (!TestConstants(expectedConstants, bytecode.Constants, out err))
        {
            Assert.Fail(err);
            return;
        }

        Assert.Pass();
    }
}

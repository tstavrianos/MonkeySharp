using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class TreeWalkTests
{
    private static readonly object[] TestEvalExpressionCases =
    [
        new object[] { "1", 1 },
        new object[] { "2", 2 },
        new object[] { "1 + 2", 3 },
        new object[] { "1 - 2", -1 },
        new object[] { "1 * 2", 2 },
        new object[] { "4 / 2", 2 },
        new object[] { "50 / 2 * 2 + 10 - 5", 55 },
        new object[] { "5 + 5 + 5 + 5 - 10", 10 },
        new object[] { "2 * 2 * 2 * 2 * 2", 32 },
        new object[] { "5 * 2 + 10", 20 },
        new object[] { "5 + 2 * 10", 25 },
        new object[] { "5 * (2 + 10)", 60 },
        new object[] { "true", true },
        new object[] { "false", false },
        new object[] { "1 < 2", true },
        new object[] { "1 > 2", false },
        new object[] { "1 < 1", false },
        new object[] { "1 > 1", false },
        new object[] { "1 == 1", true },
        new object[] { "1 != 1", false },
        new object[] { "1 == 2", false },
        new object[] { "1 != 2", true },
        new object[] { "true == true", true },
        new object[] { "false == false", true },
        new object[] { "true == false", false },
        new object[] { "true != false", true },
        new object[] { "false != true", true },
        new object[] { "(1 < 2) == true", true },
        new object[] { "(1 < 2) == false", false },
        new object[] { "(1 > 2) == true", false },
        new object[] { "(1 > 2) == false", true },
        new object[] { "!true", false },
        new object[] { "!false", true },
        new object[] { "!5", false },
        new object[] { "!!true", true },
        new object[] { "!!false", false },
        new object[] { "!!5", true },
        new object[] { "-5", -5 },
        new object[] { "-10", -10 },
        new object[] { "-50 + 100 + -50", 0 },
        new object[] { "(5 + 10 * 2 + 15 / 3) * 2 + -10", 50 },
        new object[] { "if (true) { 10 }", 10 },
        new object[] { "if (true) { 10 } else { 20 }", 10 },
        new object[] { "if (false) { 10 } else { 20 } ", 20 },
        new object[] { "if (1) { 10 }", 10 },
        new object[] { "if (1 < 2) { 10 }", 10 },
        new object[] { "if (1 < 2) { 10 } else { 20 }", 10 },
        new object[] { "if (1 > 2) { 10 } else { 20 }", 20 },
        new object[] { "if (1 > 2) { 10 }", null },
        new object[] { "if (false) { 10 }", null },
        new object[] { "!(if (false) { 5; })", true },
        new object[] { "if ((if (false) { 10 })) { 10 } else { 20 }", 20 },
        new object[] { "let one = 1; one", 1 },
        new object[] { "let one = 1; let two = 2; one + two", 3 },
        new object[] { "let one = 1; let two = one + one; one + two", 3 },
        new object[] { "\"monkey\"", "monkey" },
        new object[] { "\"mon\"+\"key\"", "monkey" },
        new object[] { "\"mon\"+\"key\"+\"banana\"", "monkeybanana" },
        new object[] { "[]", Array.Empty<object>() },
        new object[] { "[1, 2, 3]", new object[] { 1, 2, 3 } },
        new object[] { "[1 + 2, 3 - 4, 5 * 6]", new object[] { 3, -1, 30 } },
        new object[] { "{}", new Dictionary<object, object>() },
        new object[]
        {
            "{1:2,2:3}",
            new Dictionary<object, object> { { 1, 2 }, { 2, 3 } },
        },
        new object[]
        {
            "{1+1:2*2,3+3:4*4}",
            new Dictionary<object, object> { { 2, 4 }, { 6, 16 } },
        },
        new object[] { "[1, 2, 3][1]", 2 },
        new object[] { "[1, 2, 3][0 + 2]", 3 },
        new object[] { "[[1, 1, 1]][0][0]", 1 },
        new object[] { "[][0]", null },
        new object[] { "[1, 2, 3][99]", null },
        new object[] { "[1][-1]", null },
        new object[] { "{1: 1, 2: 2}[1]", 1 },
        new object[] { "{1: 1, 2: 2}[2]", 2 },
        new object[] { "{1: 1}[0]", null },
        new object[] { "{}[0]", null },
        new object[] { "let fivePlusTen = fn() { 5 + 10; }; fivePlusTen();", 15 },
        new object[] { "let one = fn() { 1; }; let two = fn() { 2; }; one() + two()", 3 },
        new object[] { "let earlyExit = fn() { return 99; 100; }; earlyExit();", 99 },
        new object[] { "let earlyExit = fn() { return 99; return 100; }; earlyExit();", 99 },
        new object[] { "let noReturn = fn() { }; noReturn();", null },
        new object[]
        {
            "let noReturn = fn() { }; let noReturnTwo = fn() { noReturn(); }; noReturn(); noReturnTwo();",
            null,
        },
        new object[]
        {
            "let returnsOne = fn() { 1; }; let returnsOneReturner = fn() { returnsOne; }; returnsOneReturner()();",
            1,
        },
        new object[]
        {
            "let returnsOneReturner = fn() {let returnsOne = fn() { 1; };returnsOne;};returnsOneReturner()();",
            1,
        },
        new object[] { "let one = fn() { let one = 1; one };one();", 1 },
        new object[]
        {
            "let oneAndTwo = fn() { let one = 1; let two = 2; one + two; };oneAndTwo();",
            3,
        },
        new object[]
        {
            "let oneAndTwo = fn() { let one = 1; let two = 2; one + two; };let threeAndFour = fn() { let three = 3; let four = 4; three + four; };oneAndTwo() + threeAndFour();",
            10,
        },
        new object[]
        {
            "let firstFoobar = fn() { let foobar = 50; foobar; };let secondFoobar = fn() { let foobar = 100; foobar; };firstFoobar() + secondFoobar();",
            150,
        },
        new object[]
        {
            "let globalSeed = 50;let minusOne = fn() {let num = 1;globalSeed - num;}let minusTwo = fn() {let num = 2;globalSeed - num;}minusOne() + minusTwo();",
            97,
        },
        new object[] { "let one = fn() { let one = 1; one }; one();", 1 },
        new object[]
        {
            "let oneAndTwo = fn() { let one = 1; let two = 2; one + two; }; oneAndTwo();",
            3,
        },
        new object[]
        {
            "let oneAndTwo = fn() { let one = 1; let two = 2; one + two; }; let threeAndFour = fn() { let three = 3; let four = 4; three + four;} oneAndTwo() + threeAndFour();",
            10,
        },
        new object[]
        {
            "let firstFoobar = fn() { let foobar = 50; foobar; }; let secondFoobar = fn() { let foobar = 100; foobar; }; firstFoobar() + secondFoobar();",
            150,
        },
        new object[]
        {
            "let globalSeed = 50; let minusOne = fn() { let num = 1; globalSeed - num; } let minusTwo = fn() { let num = 2; globalSeed - num; } minusOne() + minusTwo();",
            97,
        },
        new object[]
        {
            "let returnsOneReturner = fn() {let returnsOne = fn() { 1; };returnsOne;};returnsOneReturner()();",
            1,
        },
        new object[] { "let identity = fn(a) { a; }; identity(4);", 4 },
        new object[] { "let sum = fn(a, b) { a + b; }; sum(1, 2);", 3 },
        new object[] { "let sum = fn(a, b) { let c = a + b; c; }; sum(1, 2);", 3 },
        new object[] { "let sum = fn(a, b) { let c = a + b; c; }; sum(1, 2) + sum(3, 4);", 10 },
        new object[]
        {
            "let sum = fn(a, b) { let c = a + b; c; }; let outer = fn() { sum(1, 2) + sum(3, 4); }; outer();",
            10,
        },
        new object[]
        {
            "let globalNum = 10; let sum = fn(a, b) { let c = a + b; c + globalNum; }; let outer = fn() { sum(1, 2) + sum(3, 4) + globalNum; }; outer() + globalNum;",
            50,
        },
        new object[] { "len(\"\")", 0 },
        new object[] { "len(\"four\")", 4 },
        new object[] { "len(\"hello world\")", 11 },
        new object[] { "len([1, 2, 3])", 3 },
        new object[] { "len([])", 0 },
        new object[] { "puts(\"hello\", \"world!\")", null },
        new object[] { "first([1, 2, 3])", 1 },
        new object[] { "first([])", null },
        new object[] { "last([1, 2, 3])", 3 },
        new object[] { "last([])", null },
        new object[] { "rest([1, 2, 3])", new object[] { 2, 3 } },
        new object[] { "rest([])", null },
        new object[] { "push([], 1)", new object[] { 1 } },
        new object[]
        {
            "let newClosure = fn(a) { fn() { a; }; }; let closure = newClosure(99); closure();",
            99,
        },
        new object[]
        {
            "let newAdder = fn(a, b) {fn(c) { a + b + c };};let adder = newAdder(1, 2);adder(8);",
            11,
        },
        new object[]
        {
            "let newAdder = fn(a, b) {let c = a + b;fn(d) { c + d };};let adder = newAdder(1, 2);adder(8);",
            11,
        },
        new object[]
        {
            "let newAdderOuter = fn(a, b) {let c = a + b;fn(d){let e = d + c;fn(f) { e + f; };};};let newAdderInner = newAdderOuter(1, 2)let adder = newAdderInner(3);adder(8);",
            14,
        },
        new object[]
        {
            "let a = 1;let newAdderOuter = fn(b) {fn(c) {fn(d) { a + b + c + d };};};let newAdderInner = newAdderOuter(2)let adder = newAdderInner(3);adder(8);",
            14,
        },
        new object[]
        {
            "let newClosure = fn(a, b) {let one = fn() { a;};let two = fn() { b; };fn() { one() + two(); };};let closure = newClosure(9, 90);closure();",
            99,
        },
        new object[]
        {
            "let countDown = fn(x) {if (x == 0) {return 0;} else {countDown(x - 1);}};countDown(1);",
            0,
        },
        new object[]
        {
            "let countDown = fn(x) {if (x == 0) {return 0;} else {countDown(x - 1);}};let wrapper = fn() {countDown(1);};wrapper();",
            0,
        },
        new object[]
        {
            "let wrapper = fn() {let countDown = fn(x) {if (x == 0) {return 0;} else {countDown(x - 1);}};countDown(1);};wrapper();",
            0,
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestEvalExpressionCases))]
    public void TestEvalExpression(string input, object expected)
    {
        var evaluated = TestTreeWalkCommon.Eval(input);
        if (!TestTreeWalkCommon.TestValue(evaluated, expected, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("5 + true;", "unknown operator: INTEGER + BOOLEAN")]
    [TestCase("5 + true; 5;", "unknown operator: INTEGER + BOOLEAN")]
    [TestCase("-true", "unknown operator: -BOOLEAN")]
    [TestCase("true + false;", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase("5; true + false; 5", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase("if (10 > 1) { true + false; }", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase(
        @"if (10 > 1) {
if (10 > 1) {
return true + false;
}
return 1;
}",
        "unknown operator: BOOLEAN + BOOLEAN"
    )]
    [TestCase("foobar", "identifier not found: foobar")]
    [TestCase("\"foo\" - \"bar\"", "unknown operator: STRING - STRING")]
    [TestCase("len(1)", "argument to 'len' not supported, got INTEGER")]
    [TestCase("len(\"one\", \"two\")", "wrong number of arguments. want=1, got=2")]
    [TestCase("999[1]", "index operator not supported: INTEGER")]
    [TestCase("{\"name\": \"Monkey\"}[fn(x) { x }];", "unusable as hash key: FUNCTION")]
    public void TestErrorHandling(string input, string expected)
    {
        var evaluated = TestTreeWalkCommon.Eval(input);
        if (!evaluated.IsError)
        {
            Assert.Fail($"no error object returned. got={evaluated.Type}");
            return;
        }

        if (evaluated.ErrorMessage != expected)
        {
            Assert.Fail($"wrong error message. expected={expected}, got={evaluated.ErrorMessage}");
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("fn(x) { x + 2; }")]
    public void TestFunction(string input)
    {
        var evaluated = TestTreeWalkCommon.Eval(input);
        if (!evaluated.IsFunction)
        {
            Assert.Fail($"object is not FunctionObject. got={evaluated.Type}");
            return;
        }

        if (evaluated.FunctionData!.Parameters.Count != 1)
        {
            Assert.Fail(
                $"wrong number of parameters. expected=1, got={evaluated.FunctionData.Parameters.Count}"
            );
            return;
        }

        if (evaluated.FunctionData.Parameters[0].ToString() != "x")
        {
            Assert.Fail($"parameter is not 'x'. got={evaluated.FunctionData.Parameters[0]}");
            return;
        }

        var expectedBody = "(x + 2)";
        if (evaluated.FunctionData.Body.ToString() != expectedBody)
        {
            Assert.Fail($"body is not {expectedBody}, got={evaluated.FunctionData.Body}");
            return;
        }

        Assert.Pass();
    }
}

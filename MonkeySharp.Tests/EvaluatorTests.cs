using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class EvaluatorTests
{
    private static readonly object[] TestEvalExpressionCases =
    [
        new object[] {"5", 5},
        new object[] {"10", 10},
        new object[] {"true", true},
        new object[] {"false", false},
        new object[] {"!true", false},
        new object[] {"!false", true},
        new object[] {"!5", false},
        new object[] {"!!true", true},
        new object[] {"!!false", false},
        new object[] {"!!5", true},
        new object[] {"-5", -5},
        new object[] {"-10", -10},
        new object[] {"5 + 5 + 5 + 5 - 10", 10},
        new object[] {"2 * 2 * 2 * 2 * 2", 32},
        new object[] {"-50 + 100 + -50", 0},
        new object[] {"5 * 2 + 10", 20},
        new object[] {"5 + 2 * 10", 25},
        new object[] {"20 + 2 * -10", 0},
        new object[] {"50 / 2 * 2 + 10", 60},
        new object[] {"2 * (5 + 10)", 30},
        new object[] {"3 * 3 * 3 + 10", 37},
        new object[] {"3 * (3 * 3) + 10", 37},
        new object[] {"(5 + 10 * 2 + 15 / 3) * 2 + -10", 50},
        new object[] {"1 < 2", true},
        new object[] {"1 > 2", false},
        new object[] {"1 < 1", false},
        new object[] {"1 > 1", false},
        new object[] {"1 == 1", true},
        new object[] {"1 != 1", false},
        new object[] {"1 == 2", false},
        new object[] {"1 != 2", true},
        new object[] {"true == true", true},
        new object[] {"false == false", true},
        new object[] {"true == false", false},
        new object[] {"true != false", true},
        new object[] {"false != true", true},
        new object[] {"(1 < 2) == true", true},
        new object[] {"(1 < 2) == false", false},
        new object[] {"(1 > 2) == true", false},
        new object[] {"(1 > 2) == false", true},
        new object[] {"if (true) { 10 }", 10},
        new object[] {"if (false) { 10 }", null},
        new object[] {"if (1) { 10 }", 10},
        new object[] {"if (1 < 2) { 10 }", 10},
        new object[] {"if (1 > 2) { 10 }", null},
        new object[] {"if (1 > 2) { 10 } else { 20 }", 20},
        new object[] {"if (1 < 2) { 10 } else { 20 }", 10},
        new object[] {"return 10;", 10},
        new object[] {"return 10; 9;", 10},
        new object[] {"return 2 * 5; 9;", 10},
        new object[] {"9; return 2 * 5; 9;", 10},
        new object[] {"if (10 > 1) { if (10 > 1) { return 10; } return 1; }", 10},
        new object[] {"let a = 5; a;", 5},
        new object[] {"let a = 5 * 5; a;", 25},
        new object[] {"let a = 5; let b = a; b;", 5},
        new object[] {"let a = 5; let b = a; let c = a + b + 5; c;", 15},
        new object[] {"let identity = fn(x) { x; }; identity(5);", 5},
        new object[] {"let identity = fn(x) { return x; }; identity(5);", 5},
        new object[] {"let double = fn(x) { x * 2; }; double(5);", 10},
        new object[] {"let add = fn(x, y) { x + y; }; add(5, 5);", 10},
        new object[] {"let add = fn(x, y) { x + y; }; add(5 + 5, add(5, 5));", 20},
        new object[] {"fn(x) { x; }(5)", 5},
        new object[]
        {
            @"let newAdder = fn(x) {
    fn(y) { x + y };
};
let addTwo = newAdder(2);
addTwo(2);",
            4
        },
        new object[] {"\"foobar\";", "foobar"},
        new object[] {"\"foo bar\";", "foo bar"},
        new object[] {"\"foo\" + \"bar\";", "foobar"},
        new object[] {"len(\"\")", 0},
        new object[] {"len(\"four\")", 4},
        new object[] {"len(\"hello world\")", 11},
        new object[] {"[1, 2, 3][0]", 1},
        new object[] {"[1, 2, 3][1]", 2},
        new object[] {"[1, 2, 3][2]", 3},
        new object[] {"let i = 0; [1][i];", 1},
        new object[] {"[1, 2, 3][1 + 1];", 3},
        new object[] {"let myArray = [1, 2, 3]; myArray[2];", 3},
        new object[] {"let myArray = [1, 2, 3]; myArray[0] + myArray[1] + myArray[2];", 6},
        new object[] {"let myArray = [1, 2, 3]; let i = myArray[0]; myArray[i]", 2},
        new object[] {"[1, 2, 3][3]", null},
        new object[] {"[1, 2, 3][-1]", null},
        new object[] {"{\"foo\": 5}[\"foo\"]", 5},
        new object[] {"{\"foo\": 5}[\"bar\"]", null},
        new object[] {"let key = \"foo\"; {\"foo\": 5}[key]", 5},
        new object[] {"{}[\"foo\"]", null},
        new object[] {"{5: 5}[5]", 5},
        new object[] {"{true: 5}[true]", 5},
        new object[] {"{false: 5}[false]", 5}
    ];

    [Test]
    [TestCaseSource(nameof(TestEvalExpressionCases))]
    public void TestEvalExpression(string input, object expected)
    {
        var evaluated = TestCommon.Eval(input);
        if (!TestCommon.TestValue(evaluated, expected, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("5 + true;", "type mismatch: INTEGER + BOOLEAN")]
    [TestCase("5 + true; 5;", "type mismatch: INTEGER + BOOLEAN")]
    [TestCase("-true", "unknown operator: -BOOLEAN")]
    [TestCase("true + false;", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase("5; true + false; 5", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase("if (10 > 1) { true + false; }", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase(@"if (10 > 1) {
if (10 > 1) {
return true + false;
}
return 1;
}", "unknown operator: BOOLEAN + BOOLEAN")]
    [TestCase("foobar", "identifier not found: foobar")]
    [TestCase("\"foo\" - \"bar\"", "unknown operator: STRING - STRING")]
    [TestCase("len(1)", "argument to 'len' not supported, got INTEGER")]
    [TestCase("len(\"one\", \"two\")", "wrong number of arguments. want=1, got=2")]
    [TestCase("999[1]", "index operator not supported: INTEGER")]
    [TestCase("{\"name\": \"Monkey\"}[fn(x) { x }];", "unusable as hash key: FUNCTION")]
    public void TestErrorHandling(string input, string expected)
    {
        var evaluated = TestCommon.Eval(input);
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
        var evaluated = TestCommon.Eval(input);
        if (!evaluated.IsFunction)
        {
            Assert.Fail($"object is not FunctionObject. got={evaluated.Type}");
            return;
        }

        if (evaluated.FunctionData.Parameters.Count != 1)
        {
            Assert.Fail($"wrong number of parameters. expected=1, got={evaluated.FunctionData.Parameters.Count}");
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
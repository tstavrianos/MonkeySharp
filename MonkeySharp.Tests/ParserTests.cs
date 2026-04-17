using System;
using System.Collections.Generic;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class ParserTests
{
    private static readonly object[] ParseLetStatementsCases =
    [
        new object[] { "let x = 5;", new (string left, object right)[] { ("x", 5) } },
        new object[] { "let y = true;", new (string left, object right)[] { ("y", true) } },
        new object[] { "let foobar = y;", new (string left, object right)[] { ("foobar", "y") } },
    ];

    [Test]
    [TestCaseSource(nameof(ParseLetStatementsCases))]
    public void ParseLetStatements(string input, (string left, object right)[] expectedIdentifiers)
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");

        if (program.Statements.Count != expectedIdentifiers.Length)
            Assert.Fail(
                $"program.Statements does not contain {expectedIdentifiers.Length} statements. got={program.Statements.Count}"
            );

        for (var i = 0; i < expectedIdentifiers.Length; i++)
        {
            var s = program.Statements[i];
            if (!TestLetStatement(s, expectedIdentifiers[i], out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    private static readonly object[] ParseReturnStatementsCases =
    [
        new object[] { "return 5;", 5 },
        new object[] { "return 10;", 10 },
        new object[] { "return 993322;", 993322 },
    ];

    [Test]
    [TestCaseSource(nameof(ParseReturnStatementsCases))]
    public void ParseReturnStatements(string input, object expectedReturnValue)
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");

        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements does not contain 1 statements. got={program.Statements.Count}"
            );

        if (!TestReturnValue(program.Statements[0], expectedReturnValue, out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("5;", new object[] { 5 })]
    [TestCase("foobar;", new object[] { "foobar" })]
    [TestCase("true;", new object[] { true })]
    [TestCase("false;", new object[] { false })]
    public void TestLiteralExpression(string input, object[] expectedValues)
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != expectedValues.Length)
            Assert.Fail(
                $"program.Statements does not contain {expectedValues.Length} statements. got={program.Statements.Count}"
            );

        for (var i = 0; i < expectedValues.Length; i++)
        {
            var s = program.Statements[i];
            if (!TestExpressionStatement(s, expectedValues[i], out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    private static readonly object[] TestParsingPrefixExpressionsCases =
    [
        new object[] { "!5;", new (string oper, object value)[] { ("!", 5) } },
        new object[] { "-15;", new (string oper, object value)[] { ("-", 15) } },
        new object[] { "!true;", new (string oper, object value)[] { ("!", true) } },
        new object[] { "!false;", new (string oper, object value)[] { ("!", false) } },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingPrefixExpressionsCases))]
    public void TestParsingPrefixExpressions(
        string input,
        (string oper, object value)[] expectedValues
    )
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != expectedValues.Length)
            Assert.Fail(
                $"program.Statements does not contain {expectedValues.Length} statements. got={program.Statements.Count}"
            );

        for (var i = 0; i < expectedValues.Length; i++)
        {
            var s = program.Statements[i];
            if (!TestPrefixExpression(s, expectedValues[i], out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    private static readonly object[] TestParsingInfixExpressionsCases =
    [
        new object[] { "5 + 5;", new (object left, string oper, object right)[] { (5, "+", 5) } },
        new object[] { "5 - 5;", new (object left, string oper, object right)[] { (5, "-", 5) } },
        new object[] { "5 * 5;", new (object left, string oper, object right)[] { (5, "*", 5) } },
        new object[] { "5 / 5;", new (object left, string oper, object right)[] { (5, "/", 5) } },
        new object[] { "5 > 5;", new (object left, string oper, object right)[] { (5, ">", 5) } },
        new object[] { "5 < 5;", new (object left, string oper, object right)[] { (5, "<", 5) } },
        new object[] { "5 == 5;", new (object left, string oper, object right)[] { (5, "==", 5) } },
        new object[] { "5 != 5;", new (object left, string oper, object right)[] { (5, "!=", 5) } },
        new object[]
        {
            "true == true;",
            new (object left, string oper, object right)[] { (true, "==", true) },
        },
        new object[]
        {
            "true != false;",
            new (object left, string oper, object right)[] { (true, "!=", false) },
        },
        new object[]
        {
            "false == false;",
            new (object left, string oper, object right)[] { (false, "==", false) },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingInfixExpressionsCases))]
    public void TestParsingInfixExpressions(
        string input,
        (object left, string oper, object right)[] expectedValues
    )
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != expectedValues.Length)
            Assert.Fail(
                $"program.Statements does not contain {expectedValues.Length} statements. got={program.Statements.Count}"
            );

        for (var i = 0; i < expectedValues.Length; i++)
        {
            var s = program.Statements[i];
            if (!TestInfixExpression(s, expectedValues[i], out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("-a * b", "((-a) * b)")]
    [TestCase("!-a", "(!(-a))")]
    [TestCase("a + b + c", "((a + b) + c)")]
    [TestCase("a + b - c", "((a + b) - c)")]
    [TestCase("a * b * c", "((a * b) * c)")]
    [TestCase("a * b / c", "((a * b) / c)")]
    [TestCase("a + b / c", "(a + (b / c))")]
    [TestCase("a + b * c + d / e - f", "(((a + (b * c)) + (d / e)) - f)")]
    [TestCase("3 + 4; -5 * 5", "(3 + 4)((-5) * 5)")]
    [TestCase("5 > 4 == 3 < 4", "((5 > 4) == (3 < 4))")]
    [TestCase("5 < 4 != 3 > 4", "((5 < 4) != (3 > 4))")]
    [TestCase("3 + 4 * 5 == 3 * 1 + 4 * 5", "((3 + (4 * 5)) == ((3 * 1) + (4 * 5)))")]
    [TestCase("true", "true")]
    [TestCase("false", "false")]
    [TestCase("3 > 5 == false", "((3 > 5) == false)")]
    [TestCase("3 < 5 == true", "((3 < 5) == true)")]
    [TestCase("1 + (2 + 3) + 4", "((1 + (2 + 3)) + 4)")]
    [TestCase("(5 + 5) * 2", "((5 + 5) * 2)")]
    [TestCase("2 / (5 + 5)", "(2 / (5 + 5))")]
    [TestCase("-(5 + 5)", "(-(5 + 5))")]
    [TestCase("!(true == true)", "(!(true == true))")]
    [TestCase("a + add(b * c) + d", "((a + add((b * c))) + d)")]
    [TestCase(
        "add(a, b, 1, 2 * 3, 4 + 5, add(6, 7 * 8))",
        "add(a, b, 1, (2 * 3), (4 + 5), add(6, (7 * 8)))"
    )]
    [TestCase("add(a + b + c * d / f + g)", "add((((a + b) + ((c * d) / f)) + g))")]
    [TestCase("a * [1, 2, 3, 4][b * c] * d", "((a * ([1, 2, 3, 4][(b * c)])) * d)")]
    [TestCase("add(a * b[2], b[1], 2 * [1, 2][1])", "add((a * (b[2])), (b[1]), (2 * ([1, 2][1])))")]
    public void TestOperatorPrecedenceParsing(string input, string expectedOutput)
    {
        var lexer = new Lexer(input);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (!TestCommon.CheckParserErrors(parser, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.ToString() != expectedOutput)
        {
            Assert.Fail($"expected={expectedOutput}, got={program}");
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("if (x < y) { x }")]
    public void TestIfExpression(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not IfExpression exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.IfExpression. got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (
            !TestInfixExpression(
                exp.Condition,
                new ValueTuple<object, string, object>("x", "<", "y"),
                out errorMessage
            )
        )
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (exp.Consequence.Statements.Count != 1)
        {
            Assert.Fail($"consequence is not 1 statement. got={exp.Consequence.Statements.Count}");
            return;
        }

        if (exp.Consequence.Statements[0] is not ExpressionStatement consequence)
        {
            Assert.Fail(
                $"consequence.Statements[0] is not ast.ExpressionStatement. got={exp.Consequence.Statements[0].GetType()}"
            );
            return;
        }

        if (!TestLiteralExpression(consequence.Expression, "x", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("if (x < y) { x } else { y }")]
    public void TestIfElseExpression(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not IfExpression exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.IfExpression. got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (
            !TestInfixExpression(
                exp.Condition,
                new ValueTuple<object, string, object>("x", "<", "y"),
                out errorMessage
            )
        )
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (exp.Consequence.Statements.Count != 1)
        {
            Assert.Fail($"consequence is not 1 statement. got={exp.Consequence.Statements.Count}");
            return;
        }

        if (exp.Consequence.Statements[0] is not ExpressionStatement consequence)
        {
            Assert.Fail(
                $"consequence.Statements[0] is not ast.ExpressionStatement. got={exp.Consequence.Statements[0].GetType()}"
            );
            return;
        }

        if (!TestLiteralExpression(consequence.Expression, "x", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (exp.Alternative == null)
        {
            Assert.Fail("exp.Alternative was nil.");
            return;
        }

        if (exp.Alternative.Statements.Count != 1)
        {
            Assert.Fail($"alternative is not 1 statement. got={exp.Alternative.Statements.Count}");
            return;
        }

        if (exp.Alternative.Statements[0] is not ExpressionStatement alternative)
        {
            Assert.Fail(
                $"alternative.Statements[0] is not ast.ExpressionStatement. got={exp.Alternative.Statements[0].GetType()}"
            );
            return;
        }

        if (!TestLiteralExpression(alternative.Expression, "y", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("fn(x, y) { x + y; }")]
    public void TestFunctionLiteralParsing(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not FunctionLiteral function)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.FunctionLiteralExpression. got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (function.Parameters.Count != 2)
        {
            Assert.Fail(
                $"function literal parameters wrong. want 2, got={function.Parameters.Count}"
            );
            return;
        }

        if (!TestLiteralExpression(function.Parameters[0], "x", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestLiteralExpression(function.Parameters[1], "y", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (function.Body.Statements.Count != 1)
        {
            Assert.Fail(
                $"function.Body.Statements has not 1 statements. got={function.Body.Statements.Count}"
            );
            return;
        }

        if (function.Body.Statements[0] is not ExpressionStatement bodyStmt)
        {
            Assert.Fail(
                $"function.Body.Statements[0] is not ast.ExpressionStatement. got={function.Body.Statements[0].GetType()}"
            );
            return;
        }

        if (!TestInfixExpression(bodyStmt.Expression, ("x", "+", "y"), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("fn() {};", new string[0])]
    [TestCase("fn(x) {};", new[] { "x" })]
    [TestCase("fn(x, y, z) {};", new[] { "x", "y", "z" })]
    public void TestFunctionParameterParsing(string input, string[] expectedParameters)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not FunctionLiteral function)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.FunctionLiteralExpression. got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (function.Parameters.Count != expectedParameters.Length)
        {
            Assert.Fail(
                $"function literal parameters wrong. want {expectedParameters.Length}, got={function.Parameters.Count}"
            );
            return;
        }

        for (var i = 0; i < expectedParameters.Length; i++)
            if (
                !TestLiteralExpression(
                    function.Parameters[i],
                    expectedParameters[i],
                    out errorMessage
                )
            )
            {
                Assert.Fail(errorMessage);
                return;
            }

        Assert.Pass();
    }

    [Test]
    [TestCase("add(1, 2 * 3, 4 + 5);")]
    public void TestCallExpressionParsing(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not CallExpression exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.CallExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (!TestLiteralExpression(exp.Function, "add", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (exp.Arguments.Count != 3)
        {
            Assert.Fail($"wrong length of arguments. got={exp.Arguments.Count}");
            return;
        }

        if (!TestLiteralExpression(exp.Arguments[0], 1, out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestInfixExpression(exp.Arguments[1], (2, "*", 3), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestInfixExpression(exp.Arguments[2], (4, "+", 5), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("[1, 2 * 2, 3 + 3]")]
    public void TestParsingArrayLiterals(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not ArrayLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.ArrayLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Elements.Count != 3)
        {
            Assert.Fail($"len(array.Elements) not 3. got={exp.Elements.Count}");
            return;
        }

        if (!TestLiteralExpression(exp.Elements[0], 1, out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestInfixExpression(exp.Elements[1], (2, "*", 2), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestInfixExpression(exp.Elements[2], (3, "+", 3), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("myArray[1 + 1]")]
    public void TestParsingIndexExpressions(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not IndexExpression exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.IndexExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (!TestLiteralExpression(exp.Left, "myArray", out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (!TestInfixExpression(exp.Index, (1, "+", 1), out errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        Assert.Pass();
    }

    public static object[] TestParsingHashLiteralsStringKeysCases =
    [
        new object[]
        {
            "{\"one\": 1, \"two\": 2, \"three\": 3}",
            new Dictionary<string, long>
            {
                { "one", 1 },
                { "two", 2 },
                { "three", 3 },
            },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingHashLiteralsStringKeysCases))]
    public void TestParsingHashLiteralsStringKeys(string input, Dictionary<string, long> expected)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not HashLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.HashLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Pairs.Count != expected.Count)
        {
            Assert.Fail(
                $"hash.Pairs has wrong length. want={expected.Count}, got={exp.Pairs.Count}"
            );
            return;
        }

        foreach (var key in exp.Pairs.Keys)
        {
            if (key is not StringLiteral literal)
            {
                Assert.Fail($"key is not ast.StringLiteralExpression. got={key.GetType()}");
                return;
            }

            if (!expected.TryGetValue(literal.ToString(), out var expectedValue))
            {
                Assert.Fail($"{literal} is not in the expected values hash");
                return;
            }

            var value = exp.Pairs[key];
            if (!TestLiteralExpression(value, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    public static object[] TestParsingHashLiteralsIntKeysCases =
    [
        new object[]
        {
            "{1: \"one\", 2: \"two\", 3: \"three\"}",
            new Dictionary<double, string>
            {
                { 1, "one" },
                { 2, "two" },
                { 3, "three" },
            },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingHashLiteralsIntKeysCases))]
    public void TestParsingHashLiteralsIntKeys(string input, Dictionary<double, string> expected)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not HashLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.HashLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Pairs.Count != expected.Count)
        {
            Assert.Fail(
                $"hash.Pairs has wrong length. want={expected.Count}, got={exp.Pairs.Count}"
            );
            return;
        }

        foreach (var key in exp.Pairs.Keys)
        {
            if (key is not IntegerLiteral literal)
            {
                Assert.Fail($"key is not ast.IntegerLiteralExpression. got={key.GetType()}");
                return;
            }

            if (!expected.TryGetValue(literal.Value, out var expectedValue))
            {
                Assert.Fail($"{literal.Value} is not in the expected values hash");
                return;
            }

            var value = exp.Pairs[key];
            if (!TestStringLiteral(value, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    public static object[] TestParsingHashLiteralsBooleanKeysCases =
    [
        new object[]
        {
            "{true: \"true\", false: \"false\"}",
            new Dictionary<bool, string> { { true, "true" }, { false, "false" } },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingHashLiteralsBooleanKeysCases))]
    public void TestParsingHashLiteralsBooleanKeys(string input, Dictionary<bool, string> expected)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not HashLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.HashLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Pairs.Count != expected.Count)
        {
            Assert.Fail(
                $"hash.Pairs has wrong length. want={expected.Count}, got={exp.Pairs.Count}"
            );
            return;
        }

        foreach (var key in exp.Pairs.Keys)
        {
            if (key is not BooleanLiteral literal)
            {
                Assert.Fail($"key is not ast.BooleanLiteralExpression. got={key.GetType()}");
                return;
            }

            if (!expected.TryGetValue(literal.Value, out var expectedValue))
            {
                Assert.Fail($"{literal.Value} is not in the expected values hash");
                return;
            }

            var value = exp.Pairs[key];
            if (!TestStringLiteral(value, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    [Test]
    [TestCase("{}")]
    public void TestParsingEmptyHashLiteral(string input)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not HashLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.HashLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Pairs.Count != 0)
        {
            Assert.Fail($"hash.Pairs has wrong length. got={exp.Pairs.Count}");
            return;
        }

        Assert.Pass();
    }

    public static object[] TestParsingHashLiteralsWithExpressionsCases =
    [
        new object[]
        {
            "{\"one\": 0 + 1, \"two\": 10 - 8, \"three\": 15 / 5}",
            new Dictionary<string, (long left, string @operator, long right)>
            {
                { "one", (0, "+", 1) },
                { "two", (10, "-", 8) },
                { "three", (15, "/", 5) },
            },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestParsingHashLiteralsWithExpressionsCases))]
    public void TestParsingHashLiteralsWithExpressions(
        string input,
        Dictionary<string, (long left, string @operator, long right)> expected
    )
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!TestCommon.CheckParserErrors(p, out var errorMessage))
        {
            Assert.Fail(errorMessage);
            return;
        }

        if (program == null)
            Assert.Fail("ParseProgram() returned nil");
        if (program.Statements.Count != 1)
            Assert.Fail(
                $"program.Statements has not enough statements. got={program.Statements.Count}"
            );

        if (program.Statements[0] is not ExpressionStatement stmt)
        {
            Assert.Fail(
                $"program.Statements[0] is not ast.ExpressionStatement. got={program.Statements[0].GetType()}"
            );
            return;
        }

        if (stmt.Expression is not HashLiteral exp)
        {
            Assert.Fail(
                $"stmt.Expression is not ast.HashLiteralExpression). got={stmt.Expression.GetType()}"
            );
            return;
        }

        if (exp.Pairs.Count != expected.Count)
        {
            Assert.Fail(
                $"hash.Pairs has wrong length. want={expected.Count}, got={exp.Pairs.Count}"
            );
            return;
        }

        foreach (var key in exp.Pairs.Keys)
        {
            if (key is not StringLiteral literal)
            {
                Assert.Fail($"key is not ast.StringLiteralExpression. got={key.GetType()}");
                return;
            }

            if (!expected.TryGetValue(literal.ToString(), out var expectedValue))
            {
                Assert.Fail($"{literal} is not in the expected values hash");
                return;
            }

            var value = exp.Pairs[key];

            if (value is not InfixExpression)
            {
                Assert.Fail($"value is not ast.InfixExpression. got={value.GetType()}");
                return;
            }

            if (
                !TestInfixExpression(
                    value,
                    (expectedValue.left, expectedValue.@operator, expectedValue.right),
                    out errorMessage
                )
            )
            {
                Assert.Fail(errorMessage);
                return;
            }
        }

        Assert.Pass();
    }

    private static bool TestLetStatement(
        Statement s,
        (string left, object right) p,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (s.TokenLiteral != "let")
        {
            errorMessage = $"s.TokenLiteral not 'let'. got={s.TokenLiteral}";
            return false;
        }

        if (s is not LetStatement letStmt)
        {
            errorMessage = $"s not *ast.LetStatement. got={s.GetType().Name}";
            return false;
        }

        if (letStmt.Name.Value != p.left)
        {
            errorMessage = $"letStmt.Name.Value not '{p.left}'. got={letStmt.Name.Value}";
            return false;
        }

        if (!TestLiteralExpression(letStmt.Value, p.right, out errorMessage))
            return false;

        return true;
    }

    private static bool TestReturnValue(Statement s, object value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (s.TokenLiteral != "return")
        {
            errorMessage = $"s.TokenLiteral not 'let'. got={s.TokenLiteral}";
            return false;
        }

        if (s is not ReturnStatement returnStmt)
        {
            errorMessage = $"s not *ast.ReturnStatement. got={s.GetType().Name}";
            return false;
        }

        if (!TestLiteralExpression(returnStmt.ReturnValue, value, out errorMessage))
            return false;

        return true;
    }

    private static bool TestExpressionStatement(Statement s, object value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (s is not ExpressionStatement stmt)
        {
            errorMessage = $"s not *ast.ExpressionStatement. got={s.GetType().Name}";
            return false;
        }

        if (!TestLiteralExpression(stmt.Expression, value, out errorMessage))
            return false;

        return true;
    }

    private static bool TestLiteralExpression(Expression exp, object value, out string errorMessage)
    {
        errorMessage = string.Empty;
        switch (value)
        {
            case long l:
                return TestIntegerLiteral(exp, l, out errorMessage);
            case int i:
                return TestIntegerLiteral(exp, i, out errorMessage);
            case string s:
                return TestIdentifier(exp, s, out errorMessage);
            case bool b:
                return TestBooleanLiteral(exp, b, out errorMessage);
            default:
                errorMessage = $"type of exp not handled. got={exp.GetType().Name}";
                return false;
        }
    }

    private static bool TestStringLiteral(Expression exp, string value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (exp is not StringLiteral ident)
        {
            errorMessage = $"exp not *ast.StringLiteralExpression. got={exp.GetType()}";
            return false;
        }

        if (ident.Value != value)
        {
            errorMessage = $"exp.Value not {value}. got={ident.Value}";
            return false;
        }

        if (ident.TokenLiteral != value)
        {
            errorMessage = $"exp.TokenLiteral not {value}. got={ident.TokenLiteral}";
            return false;
        }

        return true;
    }

    private static bool TestBooleanLiteral(Expression il, bool value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (il is not BooleanLiteral b)
        {
            errorMessage = $"il not *ast.BooleanLiteral. got={il.GetType().Name}";
            return false;
        }

        if (b.Value != value)
        {
            errorMessage = $"b.Value not '{value}'. got={b.Value}";
            return false;
        }

        return true;
    }

    private static bool TestIntegerLiteral(Expression il, long value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (il is not IntegerLiteral integ)
        {
            errorMessage = $"il not *ast.IntegerLiteral. got={il.GetType().Name}";
            return false;
        }

        if (integ.Value != value)
        {
            errorMessage = $"integ.Value not '{value}'. got={integ.Value}";
            return false;
        }

        return true;
    }

    private static bool TestIdentifier(Expression exp, string value, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (exp is not Identifier ident)
        {
            errorMessage = $"exp not *ast.Identifier. got={exp.GetType().Name}";
            return false;
        }

        if (ident.Value != value)
        {
            errorMessage = $"ident.Value is not '{value}'. got={ident.Value}";
            return false;
        }

        return true;
    }

    private bool TestPrefixExpression(
        Statement s,
        (string oper, object value) expectedValue,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (s is not ExpressionStatement stmt)
        {
            errorMessage = $"s not *ast.ExpressionStatement. got={s.GetType().Name}";
            return false;
        }

        if (stmt.Expression is not PrefixExpression exp)
        {
            errorMessage =
                $"stmt.Expression not *ast.PrefixExpression. got={stmt.Expression.GetType().Name}";
            return false;
        }

        if (exp.Operator != expectedValue.oper)
        {
            errorMessage = $"exp.Operator is not '{expectedValue.oper}'. got={exp.Operator}";
            return false;
        }

        if (!TestLiteralExpression(exp.Right, expectedValue.value, out errorMessage))
            return false;

        return true;
    }

    private bool TestInfixExpression(
        Statement s,
        (object left, string oper, object right) expectedValue,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (s is not ExpressionStatement stmt)
        {
            errorMessage = $"s not *ast.ExpressionStatement. got={s.GetType().Name}";
            return false;
        }

        if (stmt.Expression is not InfixExpression exp)
        {
            errorMessage =
                $"stmt.Expression not *ast.InfixExpression. got={stmt.Expression.GetType().Name}";
            return false;
        }

        if (!TestLiteralExpression(exp.Left, expectedValue.left, out errorMessage))
            return false;

        if (exp.Operator != expectedValue.oper)
        {
            errorMessage = $"exp.Operator is not '{expectedValue.oper}'. got={exp.Operator}";
            return false;
        }

        if (!TestLiteralExpression(exp.Right, expectedValue.right, out errorMessage))
            return false;

        return true;
    }

    private bool TestInfixExpression(
        Expression e,
        (object left, string oper, object right) expectedValue,
        out string errorMessage
    )
    {
        errorMessage = string.Empty;
        if (e is not InfixExpression exp)
        {
            errorMessage = $"e not *ast.InfixExpression. got={e.GetType().Name}";
            return false;
        }

        if (!TestLiteralExpression(exp.Left, expectedValue.left, out errorMessage))
            return false;

        if (exp.Operator != expectedValue.oper)
        {
            errorMessage = $"exp.Operator is not '{expectedValue.oper}'. got={exp.Operator}";
            return false;
        }

        if (!TestLiteralExpression(exp.Right, expectedValue.right, out errorMessage))
            return false;

        return true;
    }
}

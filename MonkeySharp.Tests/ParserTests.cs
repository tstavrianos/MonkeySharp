using System.Text;
using MonkeySharp.Core;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using NUnit.Framework;

namespace MonkeySharp.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private static readonly object[] ParseLetStatementsCases =
        [
            new object[]
            {
                @"
let x = 5;
let y = 10;
let foobar = 838383;",
                new string[] {"x", "y", "foobar"}
            }
        ];

        [Test]
        [TestCaseSource(nameof(ParseLetStatementsCases))]
        public void TestParseLetStatements(string input, string[] expectedIdentifiers)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            Assert.That(program, Is.Not.Null, "ParseProgram() returned nil");

            if (program.Statements.Count != 3)
            {
                Assert.Fail($"program.Statements does not contain 3 statements. got={program.Statements.Count}");
                return;
            }

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

        [Test]
        [TestCase(@"
return 5;
return 10;
return 993322;")]
        public void TestReturnStatements(string input)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 3)
            {
                Assert.Fail($"program.Statements does not contain 3 statements. got={program.Statements.Count}");
                return;
            }

            foreach (var stmt in program.Statements)
            {
                if (stmt is not ReturnStatement returnStmt)
                {
                    Assert.Fail($"stmt not ast.ReturnStatement. got={stmt.GetType().Name}");
                    return;
                }

                if (returnStmt.TokenLiteral != "return")
                {
                    Assert.Fail($"returnStmt.TokenLiteral not 'return', got={returnStmt.TokenLiteral}");
                    return;
                }
            }

            Assert.Pass();
        }

        [Test]
        [TestCase("foobar;", "foobar")]
        public void TestIdentifierExpression(string input, string expectedValue)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 1)
            {
                Assert.Fail($"program has not enough statements. got={program.Statements.Count}");
                return;
            }

            var stmt = program.Statements[0];
            if (stmt is not ExpressionStatement exprStmt)
            {
                Assert.Fail($"program.Statements[0] is not ast.ExpressionStatement. got={stmt.GetType().Name}");
                return;
            }

            if (!TestIdentifier(exprStmt.Expression, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            Assert.Pass();
        }

        [Test]
        [TestCase("true;", true)]
        [TestCase("false;", false)]
        public void TestBooleanExpression(string input, bool expectedValue)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 1)
            {
                Assert.Fail($"program has not enough statements. got={program.Statements.Count}");
                return;
            }

            var stmt = program.Statements[0];
            if (stmt is not ExpressionStatement exprStmt)
            {
                Assert.Fail($"program.Statements[0] is not ast.ExpressionStatement. got={stmt.GetType().Name}");
                return;
            }

            if (!TestBoolean(exprStmt.Expression, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            Assert.Pass();
        }

        [Test]
        [TestCase("5;", 5)]
        public void TestIntegerLiteralExpression(string input, long expectedValue)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 1)
            {
                Assert.Fail($"program has not enough statements. got={program.Statements.Count}");
                return;
            }

            var stmt = program.Statements[0];
            if (stmt is not ExpressionStatement exprStmt)
            {
                Assert.Fail($"program.Statements[0] is not ast.ExpressionStatement. got={stmt.GetType().Name}");
                return;
            }

            if (!TestIntegerLiteral(exprStmt.Expression, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            Assert.Pass();
        }

        [Test]
        [TestCase("!5;", "!", 5)]
        [TestCase("-15;", "-", 15)]
        [TestCase("!true;", "!", true)]
        [TestCase("!false;", "!", false)]
        public void TestParsingPrefixExpressions(string input, string expectedOperator, object expectedValue)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 1)
            {
                Assert.Fail($"program has not enough statements. got={program.Statements.Count}");
                return;
            }

            var stmt = program.Statements[0];
            if (stmt is not ExpressionStatement exprStmt)
            {
                Assert.Fail($"program.Statements[0] is not ast.ExpressionStatement. got={stmt.GetType().Name}");
                return;
            }

            if (exprStmt.Expression is not PrefixExpression exp)
            {
                Assert.Fail($"exp not ast.PrefixExpression. got={exprStmt.Expression.GetType().Name}");
                return;
            }

            if (exp.Operator != expectedOperator)
            {
                Assert.Fail($"exp.Operator is not '{expectedOperator}'. got={exp.Operator}");
                return;
            }

            if (!TestLiteralExpression(exp.Right, expectedValue, out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            Assert.Pass();
        }

        [Test]
        [TestCase("5 + 5;", 5, "+", 5)]
        [TestCase("5 - 5;", 5, "-", 5)]
        [TestCase("5 * 5;", 5, "*", 5)]
        [TestCase("5 / 5;", 5, "/", 5)]
        [TestCase("5 > 5;", 5, ">", 5)]
        [TestCase("5 < 5;", 5, "<", 5)]
        [TestCase("5 == 5;", 5, "==", 5)]
        [TestCase("5 != 5;", 5, "!=", 5)]
        [TestCase("true == true;", true, "==", true)]
        [TestCase("true != false;", true, "!=", false)]
        [TestCase("false == false;", false, "==", false)]
        public void TestParsingInfixExpressions(string input, object expectedLeftValue, string expectedOperator,
            object expectedRightValue)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage)) Assert.Fail(errorMessage);
            if (program.Statements.Count != 1)
            {
                Assert.Fail($"program has not enough statements. got={program.Statements.Count}");
                return;
            }

            var stmt = program.Statements[0];
            if (stmt is not ExpressionStatement exprStmt)
            {
                Assert.Fail($"program.Statements[0] is not ast.ExpressionStatement. got={stmt.GetType().Name}");
                return;
            }

            if (!TestInfixExpression(exprStmt.Expression, expectedLeftValue, expectedOperator, expectedRightValue,
                    out errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
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
        public void TestOperatorPrecedenceParsing(string input, string expected)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            var program = parser.ParseProgram();
            if (!CheckParserErrors(parser, out var errorMessage))
            {
                Assert.Fail(errorMessage);
                return;
            }

            var actual = program.ToString();
            if (actual != expected)
            {
                Assert.Fail($"expected={expected}, got={actual}");
                return;
            }

            Assert.Pass();
        }

        private static bool TestIntegerLiteral(Expression exp, long value, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (exp is not IntegerLiteral integ)
            {
                errorMessage = $"exp not ast.IntegerLiteral. got={exp.GetType().Name}";
                return false;
            }

            if (integ.Value != value)
            {
                errorMessage = $"integ.Value not {value}. got={integ.Value}";
                return false;
            }

            if (integ.TokenLiteral != $"{value}")
            {
                errorMessage = $"integ.TokenLiteral not {value}. got={integ.TokenLiteral}";
                return false;
            }

            return true;
        }

        private static bool TestLetStatement(Statement s, string name, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (s.TokenLiteral != "let")
            {
                errorMessage = $"s.TokenLiteral not 'let'. got={s.TokenLiteral}";
                return false;
            }

            if (s is not LetStatement letStmt)
            {
                errorMessage = $"s not LetStatement. got={s.GetType().Name}";
                return false;
            }

            if (letStmt.Name.Value != name)
            {
                errorMessage = $"letStmt.Name.Value not '{name}'. got={letStmt.Name.Value}";
                return false;
            }

            if (letStmt.Name.TokenLiteral != name)
            {
                errorMessage = $"letStmt.Name.TokenLiteral not '{name}'. got={letStmt.Name.TokenLiteral}";
                return false;
            }

            return true;
        }

        private static bool TestIdentifier(Expression exp, string expectedValue, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (exp is not Identifier ident)
            {
                errorMessage = $"exp not ast.Identifier. got={exp.GetType().Name}";
                return false;
            }

            if (ident.Value != expectedValue)
            {
                errorMessage = $"ident.Value not {expectedValue}. got={ident.Value}";
                return false;
            }

            if (ident.TokenLiteral != expectedValue)
            {
                errorMessage = $"ident.TokenLiteral not {expectedValue}. got={ident.TokenLiteral}";
                return false;
            }

            return true;
        }

        private static bool TestBoolean(Expression exp, bool expectedValue, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (exp is not BooleanLiteral ident)
            {
                errorMessage = $"exp not ast.BooleanLiteral. got={exp.GetType().Name}";
                return false;
            }

            if (ident.Value != expectedValue)
            {
                errorMessage = $"ident.Value not {expectedValue}. got={ident.Value}";
                return false;
            }

            if (ident.TokenLiteral != expectedValue.ToString().ToLower())
            {
                errorMessage = $"ident.TokenLiteral not {expectedValue}. got={ident.TokenLiteral}";
                return false;
            }

            return true;
        }

        private static bool TestLiteralExpression(Expression exp, object expected, out string errorMessage)
        {
            errorMessage = string.Empty;
            switch (expected)
            {
                case long longValue:
                    return TestIntegerLiteral(exp, longValue, out errorMessage);
                case int intValue:
                    return TestIntegerLiteral(exp, intValue, out errorMessage);
                case string strValue:
                    return TestIdentifier(exp, strValue, out errorMessage);
                case bool boolValue:
                    return TestBoolean(exp, boolValue, out errorMessage);
                default:
                    errorMessage = $"type of exp not handled. got={exp.GetType().Name}";
                    return false;
            }
        }

        private static bool TestInfixExpression(Expression exp, object left, string @operator, object right,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (exp is not InfixExpression infixExp)
            {
                errorMessage = $"exp is not ast.InfixExpression. got={exp.GetType().Name}";
                return false;
            }

            if (!TestLiteralExpression(infixExp.Left, left, out errorMessage)) return false;

            if (infixExp.Operator != @operator)
            {
                errorMessage = $"exp.Operator is not '{@operator}'. got={infixExp.Operator}";
                return false;
            }

            if (!TestLiteralExpression(infixExp.Right, right, out errorMessage)) return false;

            return true;
        }

        private static bool CheckParserErrors(Parser parser, out string errorMessage)
        {
            errorMessage = string.Empty;
            var errors = parser.Errors;
            if (errors.Count == 0) return true;

            var errorMessages = new StringBuilder();
            errorMessages.AppendLine($"parser has {errors.Count} errors");
            foreach (var msg in errors) errorMessages.AppendLine($"parser error: {msg}");
            errorMessage = errorMessages.ToString();
            return false;
        }
    }
}
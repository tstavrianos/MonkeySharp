using MonkeySharp.Core;
using NUnit.Framework;

namespace MonkeySharp.Tests
{
    [TestFixture]
    public class LexerTests
    {
        private static readonly object[] TestNextTokenCases =
        [
            new object[]
            {
                "=+(){},;", new (TokenType, string)[]
                {
                    new(TokenType.Assign, "="),
                    new(TokenType.Plus, "+"),
                    new(TokenType.LeftParen, "("),
                    new(TokenType.RightParen, ")"),
                    new(TokenType.LeftBrace, "{"),
                    new(TokenType.RightBrace, "}"),
                    new(TokenType.Comma, ","),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Eof, "")
                }
            },
            new object[]
            {
                @"let five = 5;
let ten = 10;
let add = fn(x, y) {
x + y;
};
let result = add(five, ten);",
                new (TokenType, string)[]
                {
                    new(TokenType.Let, "let"),
                    new(TokenType.Identifier, "five"),
                    new(TokenType.Assign, "="),
                    new(TokenType.Integer, "5"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Let, "let"),
                    new(TokenType.Identifier, "ten"),
                    new(TokenType.Assign, "="),
                    new(TokenType.Integer, "10"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Let, "let"),
                    new(TokenType.Identifier, "add"),
                    new(TokenType.Assign, "="),
                    new(TokenType.Function, "fn"),
                    new(TokenType.LeftParen, "("),
                    new(TokenType.Identifier, "x"),
                    new(TokenType.Comma, ","),
                    new(TokenType.Identifier, "y"),
                    new(TokenType.RightParen, ")"),
                    new(TokenType.LeftBrace, "{"),
                    new(TokenType.Identifier, "x"),
                    new(TokenType.Plus, "+"),
                    new(TokenType.Identifier, "y"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.RightBrace, "}"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Let, "let"),
                    new(TokenType.Identifier, "result"),
                    new(TokenType.Assign, "="),
                    new(TokenType.Identifier, "add"),
                    new(TokenType.LeftParen, "("),
                    new(TokenType.Identifier, "five"),
                    new(TokenType.Comma, ","),
                    new(TokenType.Identifier, "ten"),
                    new(TokenType.RightParen, ")"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Eof, "")
                }
            },
            new object[]
            {
                @"!-/*5;
5 < 10 > 5;",
                new (TokenType, string)[]
                {
                    new(TokenType.Bang, "!"),
                    new(TokenType.Minus, "-"),
                    new(TokenType.Slash, "/"),
                    new(TokenType.Asterisk, "*"),
                    new(TokenType.Integer, "5"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Integer, "5"),
                    new(TokenType.LessThan, "<"),
                    new(TokenType.Integer, "10"),
                    new(TokenType.GreaterThan, ">"),
                    new(TokenType.Integer, "5"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Eof, "")
                }
            },
            new object[]
            {
                "if (5 < 10) { return true; } else { return false; }",
                new (TokenType, string)[]
                {
                    new(TokenType.If, "if"),
                    new(TokenType.LeftParen, "("),
                    new(TokenType.Integer, "5"),
                    new(TokenType.LessThan, "<"),
                    new(TokenType.Integer, "10"),
                    new(TokenType.RightParen, ")"),
                    new(TokenType.LeftBrace, "{"),
                    new(TokenType.Return, "return"),
                    new(TokenType.True, "true"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.RightBrace, "}"),
                    new(TokenType.Else, "else"),
                    new(TokenType.LeftBrace, "{"),
                    new(TokenType.Return, "return"),
                    new(TokenType.False, "false"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.RightBrace, "}"),
                    new(TokenType.Eof, "")
                }
            },
            new object[]
            {
                "10 == 10; 10 != 9;",
                new (TokenType, string)[]
                {
                    new(TokenType.Integer, "10"),
                    new(TokenType.Equal, "=="),
                    new(TokenType.Integer, "10"),
                    new(TokenType.Semicolon, ";"),
                    new(TokenType.Integer, "10"),
                    new(TokenType.NotEqual, "!="),
                    new(TokenType.Integer, "9"),
                    new(TokenType.Semicolon, ";")
                }
            }
        ];

        [Test]
        [TestCaseSource(nameof(TestNextTokenCases))]
        public void TestNextToken(string input, (TokenType, string)[] expectedResult)
        {
            var lexer = new Lexer(input);
            for (var i = 0; i < expectedResult.Length; i++)
            {
                var expectedToken = expectedResult[i].Item1;
                var expectedTokenString = expectedResult[i].Item2;

                var next = lexer.NextToken();

                if (!Equals(next.Type, expectedToken))
                {
                    Assert.Fail($"tests[{i}] - tokentype wrong. expected={expectedToken}, got={next.Type}");
                    return;
                }

                if (!Equals(next.Literal, expectedTokenString))
                {
                    Assert.Fail($"tests[{i}] - literal wrong. expected={expectedTokenString}, got={next.Literal}");
                    return;
                }
            }

            Assert.Pass();
        }
    }
}
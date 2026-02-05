using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.AbstractSyntaxTree.Expressions;
using MonkeySharp.AbstractSyntaxTree.Statements;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class AstTests
{
    private static readonly object[] TestStringCases =
    [
        new object[]
        {
            new ProgramNode(
            [
                new LetStatement(
                    new Token(TokenType.Let, "let"),
                    new Identifier(new Token(TokenType.Identifier, "myVar"), "myVar"),
                    new Identifier(new Token(TokenType.Identifier, "anotherVar"), "anotherVar"))
            ]),
            "let myVar = anotherVar;"
        }
    ];

    [Test]
    [TestCaseSource(nameof(TestStringCases))]
    public void TestString(ProgramNode programNode, string expected)
    {
        if (programNode.ToString() != expected)
            Assert.Fail($"programNode.ToString() wrong. expected={expected}, got={programNode.ToString()}");
        Assert.Pass();
    }
}
using MonkeySharp.AbstractSyntaxTree;
using NUnit.Framework;
using System.Text;

namespace MonkeySharp.Tests;

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
}
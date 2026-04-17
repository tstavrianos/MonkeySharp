using System.Text;
using MonkeySharp.AbstractSyntaxTree;
using NUnit.Framework;

namespace MonkeySharp.Tests;

internal static class TestCommon
{
    internal static ProgramNode Parse(string input, bool optimize = true)
    {
        var l = new Lexer(input);
        var p = new Parser(l);
        var program = p.ParseProgram();
        if (!CheckParserErrors(p, out var message))
        {
            Assert.Fail(message);
            return null;
        }

        if (optimize)
            program = new Optimizer().Optimize(program);

        return program;
    }

    internal static bool CheckParserErrors(Parser p, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (p.Errors.Count == 0)
            return true;
        var sb = new StringBuilder();
        sb.AppendLine($"parser has {p.Errors.Count} errors");
        foreach (var error in p.Errors)
            sb.AppendLine($"parser error: {error}");
        errorMessage = sb.ToString();
        return false;
    }
}

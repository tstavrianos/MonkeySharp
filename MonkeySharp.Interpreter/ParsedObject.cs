using MonkeySharp.AbstractSyntaxTree;

namespace MonkeySharp.Interpreter;

public class ParsedObject
{
    internal readonly ProgramNode ProgramNode;

    internal ParsedObject(ProgramNode programNode)
    {
        ProgramNode = programNode;
    }

    public static ParsedObject? Parse(string code)
    {
        var lexer = new Lexer(code);
        var parser = new Parser(lexer);
        var program = parser.ParseProgram();
        if (parser.Errors.Count != 0)
            return null;
        return new ParsedObject(program);
    }
}

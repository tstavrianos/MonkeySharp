using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Ast.Expressions;
using MonkeySharp.Core.Ast.Statements;
using MonkeySharp.Core.Interpreter;

namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Function = "FUNCTION";
    }

    public class FunctionObject : IObject
    {
        internal FunctionObject(IReadOnlyList<Identifier> parameters, BlockStatement body, Environment environment)
        {
            Parameters = parameters;
            Body = body;
            Environment = environment;
        }

        public IReadOnlyList<Identifier> Parameters { get; }
        public BlockStatement Body { get; }
        public Environment Environment { get; }
        public string Type => ObjectType.Function;

        public string Inspect
        {
            get
            {
                var buffer = new StringBuilder();
                buffer.Append("fn(");
                buffer.Append(string.Join(", ", Parameters.Select(x => x.ToString())));
                buffer.Append(") {\n");
                buffer.Append(Body.ToString());
                buffer.Append("}\n");
                return buffer.ToString();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using MonkeySharp.Core;
using MonkeySharp.Core.VirtualMachine;

namespace MonkeySharp.Cli
{
    internal static class Program
    {
        private const string Prompt = ">> ";

        public static void Main(string[] args)
        {
            Console.WriteLine($"Hello {Environment.UserName}! This is the Monkey programming language!");
            Console.WriteLine("Feel free to type in commands");

            //var env = new Environment();
            var constants = new List<Core.Objects.IObject>();
            var globals = new Core.Objects.IObject[Vm.GlobalsSize];
            var symbolTable = new SymbolTable();
            while (true)
            {
                Console.Write(Prompt);
                var input = Console.ReadLine();
                if (input == null) break;

                var lexer = new Lexer(input);
                var parser = new Parser(lexer);
                var program = parser.ParseProgram();
                if (parser.Errors.Count != 0)
                {
                    PrintParserErrors(parser.Errors);
                    continue;
                }

                /*var evaluator = new Evaluator();
                var evaluated = evaluator.Eval(program, env);

                Console.WriteLine(evaluated.Inspect);*/

                var compiler = new Compiler(symbolTable, constants);
                var err = compiler.Compile(program);
                if (!string.IsNullOrEmpty(err))
                {
                    Console.WriteLine($"Woops! Compilation failed:\n{err}");
                    continue;
                }

                var bytecode = compiler.ByteCode();
                var vm = new Vm(bytecode, globals);
                err = vm.Run();
                if (!string.IsNullOrEmpty(err))
                {
                    Console.WriteLine($"Woops! Executing bytecode failed:\n{err}");
                    continue;
                }

                var stackTop = vm.LastPoppedStackElement;
                Console.WriteLine($"VM Output: {stackTop.Inspect}");
            }
        }

        private const string MonkeyFace = @"            __,__
   .--.  .-""     ""-.  .--.
  / .. \/  .-. .-.  \/ .. \
 | |  '|  /   Y   \  |'  | |
 | \   \  \ 0 | 0 /  /   / |
  \ '- ,\.-""""""""""""""-./, -' /
   ''-' /_   ^ ^   _\ '-''
       |  \._   _./  |
       \   \ '~' /   /
        '._ '-=-' _.'
           '-----'
";

        private static void PrintParserErrors(IReadOnlyList<string> errors)
        {
            Console.Write(MonkeyFace);
            Console.WriteLine("Woops! We ran into some monkey business here!");
            Console.WriteLine(" parser errors:");
            foreach (var error in errors) Console.WriteLine($"\t{error}");
        }
    }
}
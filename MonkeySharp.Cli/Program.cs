using System;
using MonkeySharp.Core;

namespace MonkeySharp.Cli
{
    internal static class Program
    {
        private const string Prompt = ">> ";

        public static void Main(string[] args)
        {
            Console.WriteLine($"Hello {Environment.UserName}! This is the Monkey programming language!");
            Console.WriteLine("Feel free to type in commands");
            while (true)
            {
                Console.Write(Prompt);
                var input = Console.ReadLine();
                if (input == null) break;

                var lexer = new Lexer(input);
                for (var tok = lexer.NextToken(); tok.Type != TokenType.Eof; tok = lexer.NextToken())
                    Console.WriteLine(tok);
            }
        }
    }
}
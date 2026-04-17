using System;
using System.Collections.Generic;
using MonkeySharp.BytecodeVm;

namespace MonkeySharp.Cli;

internal static class Program
{
    private const string Prompt = ">> ";

    public static void Main(string[] args)
    {
        Console.WriteLine(
            $"Hello {Environment.UserName}! This is the Monkey programming language!"
        );
        Console.WriteLine("Feel free to type in commands");

        var vmSession = new Session();
        while (true)
        {
            Console.Write(Prompt);
            var input = Console.ReadLine();
            if (input == null)
                break;

            var compiled = vmSession.Compile(input);
            if (!compiled.IsValid)
            {
                PrintParserErrors(compiled.Diagnostics);
                continue;
            }

            var execution = vmSession.Run(compiled);
            if (!execution.Success)
            {
                Console.WriteLine($"Woops! Executing bytecode failed:\n{execution.Error}");
                continue;
            }

            Console.WriteLine($"VM Output: {execution.Value.Inspect}");
        }
    }

    private const string MonkeyFace =
        @"            __,__
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
        foreach (var error in errors)
            Console.WriteLine($"\t{error}");
    }
}

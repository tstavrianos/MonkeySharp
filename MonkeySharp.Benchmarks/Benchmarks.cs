using System;
using BenchmarkDotNet.Attributes;
using MonkeySharp.Core;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Interpreter;
using MonkeySharp.Core.VirtualMachine;
using Environment = MonkeySharp.Core.Interpreter.Environment;
using BenchmarkDotNet.Jobs;

namespace MonkeySharp.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    public class Benchmarks
    {
        private const string Input = @"
let fibonacci = fn(x) {
    if (x == 0) {
        0
    } else {
        if (x == 1) {
            return 1;
        } else {
            fibonacci(x - 1) + fibonacci(x - 2);
        }
    }
};
fibonacci(20);";

        private ProgramNode _program;
        private ByteCode _byteCode;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var lexer = new Lexer(Input);
            var parser = new Parser(lexer);
            _program = parser.ParseProgram();
            var compiler = new Compiler();
            compiler.Compile(_program);
            _byteCode = compiler.ByteCode();
        }

        [Benchmark]
        public void BenchmarkInterpreter()
        {
            var evaluator = new Evaluator();
            var result = evaluator.Eval(_program, new Environment());
            //Console.WriteLine(result.Inspect);
        }

        [Benchmark]
        public void ScenarioVirtualMachine()
        {
            var vm = new Vm(_byteCode);
            vm.Run();
            var result = vm.LastPoppedStackElement;
            //Console.WriteLine(result.Inspect);
        }
    }
}
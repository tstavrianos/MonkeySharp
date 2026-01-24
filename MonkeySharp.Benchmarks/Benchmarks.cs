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
        private ProgramNode _programOptimized;
        private ByteCode _byteCode;
        private ByteCode _byteCodeOptimized;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var lexer = new Lexer(Input);
            var parser = new Parser(lexer);
            _program = parser.ParseProgram();
            var optimizer = new Optimizer();
            _programOptimized = optimizer.Optimize(_program);
            var compiler = new Compiler();
            compiler.Compile(_program);
            _byteCode = compiler.ByteCode();

            var compilerOptimized = new Compiler();
            compilerOptimized.Compile(_programOptimized);
            _byteCodeOptimized = compilerOptimized.ByteCode();
        }

        [Benchmark]
        public void BenchmarkInterpreter()
        {
            var evaluator = new Evaluator();
            var result = evaluator.Eval(_program, new Environment());
        }

        [Benchmark]
        public void ScenarioVirtualMachine()
        {
            var vm = new Vm(_byteCode);
            vm.Run();
            var result = vm.LastPoppedStackElement;
        }

        [Benchmark]
        public void BenchmarkInterpreterOptimized()
        {
            var evaluator = new Evaluator();
            var result = evaluator.Eval(_programOptimized, new Environment());
        }

        [Benchmark]
        public void ScenarioVirtualMachineOptimized()
        {
            var vm = new Vm(_byteCodeOptimized);
            vm.Run();
            var result = vm.LastPoppedStackElement;
        }
    }
}
using BenchmarkDotNet.Attributes;
using MonkeySharp.Core;
using MonkeySharp.Core.Ast;
using MonkeySharp.Core.Interpreter;
using MonkeySharp.Core.VirtualMachine;
using Environment = MonkeySharp.Core.Interpreter.Environment;
using BenchmarkDotNet.Jobs;
using Microsoft.VSDiagnostics;

namespace MonkeySharp.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
[CPUUsageDiagnoser]
#if CHECK_CACHE_MISSES
    [HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
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
    [BenchmarkCategory(Categories.Evaluator)]
    public void BenchmarkEvaluator()
    {
        var evaluator = new Evaluator();
        var result = evaluator.Eval(_program, new Environment());
    }

    [Benchmark]
    [BenchmarkCategory(Categories.VisitorEvaluator)]
    public void BenchmarkVisitorEvaluator()
    {
        var evaluator = new VisitorEvaluator();
        var result = evaluator.Eval(_program, new Environment());
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Vm)]
    public void BenchmarkVm()
    {
        var vm = new Vm(_byteCode);
        vm.Run();
        var result = vm.LastPoppedStackElement;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Evaluator)]
    [BenchmarkCategory(Categories.Optimized)]
    public void BenchmarkEvaluatorOptimized()
    {
        var evaluator = new Evaluator();
        var result = evaluator.Eval(_programOptimized, new Environment());
    }

    [Benchmark]
    [BenchmarkCategory(Categories.VisitorEvaluator)]
    [BenchmarkCategory(Categories.Optimized)]
    public void BenchmarkVisitorEvaluatorOptimized()
    {
        var evaluator = new VisitorEvaluator();
        var result = evaluator.Eval(_programOptimized, new Environment());
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Vm)]
    [BenchmarkCategory(Categories.Optimized)]
    public void BenchmarkVmOptimized()
    {
        var vm = new Vm(_byteCodeOptimized);
        vm.Run();
        var result = vm.LastPoppedStackElement;
    }
}
using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.VSDiagnostics;
using MonkeySharp.AbstractSyntaxTree;
using MonkeySharp.Compiler;
using MonkeySharp.Interpreter;
using MonkeySharp.VirtualMachine;
using SymbolTable = MonkeySharp.Interpreter.SymbolTable;

namespace MonkeySharp.Benchmarks;

//[SimpleJob(RuntimeMoniker.Net10_0)]
//[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
[CPUUsageDiagnoser]
#if CHECK_CACHE_MISSES
[HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
[ReturnValueValidator(true)]
public class Benchmarks
{
    private const string Input =
        @"
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

    private static int fibonacci(int x)
    {
        if (x == 0)
        {
            return 0;
        }
        else
        {
            if (x == 1)
                return 1;
            else
                return fibonacci(x - 1) + fibonacci(x - 2);
        }
    }

    private ProgramNode _program;

    //private ProgramNode _programOptimized;
    private ByteCode _byteCode;
    private Func<MonkeyObject> _func;

    //private ByteCode _byteCodeOptimized;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var lexer = new Lexer(Input);
        var parser = new Parser(lexer);
        _program = parser.ParseProgram();
        //var optimizer = new Optimizer();
        //_programOptimized = optimizer.Optimize(_program);
        var valueCompiler = new VirtualMachine.Compiler();
        valueCompiler.Compile(_program);
        _byteCode = valueCompiler.ByteCode();

        //var compilerOptimized = new Compiler();
        //compilerOptimized.Compile(_programOptimized);
        //_byteCodeOptimized = compilerOptimized.ByteCode();

        var ilCompiler = new ILCompiler();
        _func = ilCompiler.Compile(_program);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.Native)]
    public long BenchmarkNative()
    {
        return fibonacci(20);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Evaluator)]
    public long BenchmarkEvaluator()
    {
        var result = Evaluator.Eval(_program, new SymbolTable());
        if (!result.IsInteger)
            return long.MinValue;
        return result.IntValue;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Vm)]
    public long BenchmarkVm()
    {
        var vm = new Vm(_byteCode);
        vm.Run();
        var result = vm.LastPoppedStackElement;
        if (!result.IsInteger)
            return long.MinValue;
        return result.IntValue;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.IL)]
    public long BenchmarkIL()
    {
        var result = _func();
        if (result is not MonkeyInteger integer)
            return long.MinValue;
        return integer.Value;
    }

    /*[Benchmark]
    [BenchmarkCategory(Categories.Evaluator)]
    [BenchmarkCategory(Categories.Optimized)]
    public long BenchmarkEvaluatorOptimized()
    {
        var evaluator = new Evaluator();
        var result = evaluator.Eval(_programOptimized, new Environment());
        if (result is not IntegerObject i) return long.MinValue;
        return i.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.VisitorEvaluator)]
    [BenchmarkCategory(Categories.Optimized)]
    public long BenchmarkVisitorEvaluatorOptimized()
    {
        var evaluator = new VisitorEvaluator(false);
        var result = evaluator.Eval(_programOptimized, new Environment());
        if (result is not IntegerObject i) return long.MinValue;
        return i.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.VisitorEvaluator)]
    [BenchmarkCategory(Categories.Optimized)]
    [BenchmarkCategory(Categories.StaticDispatch)]
    public long BenchmarkVisitorEvaluatorOptimized_StaticDispatch()
    {
        var evaluator = new VisitorEvaluator();
        var result = evaluator.Eval(_programOptimized, new Environment());
        if (result is not IntegerObject i) return long.MinValue;
        return i.Value;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Vm)]
    [BenchmarkCategory(Categories.Optimized)]
    public long BenchmarkVmOptimized()
    {
        var vm = new Vm(_byteCodeOptimized);
        vm.Run();
        var result = vm.LastPoppedStackElement;
        if (result is not IntegerObject i) return long.MinValue;
        return i.Value;
    }*/
}

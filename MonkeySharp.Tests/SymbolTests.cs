using MonkeySharp.BytecodeVm;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class SymbolTests
{
    private static readonly object[] TestDefineCases =
    [
        new object[]
        {
            new[] { "a", "b" },
            new (string Name, int Scope, int Index)[]
            {
                ("a", (int)SymbolScope.Global, 0),
                ("b", (int)SymbolScope.Global, 1),
            },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestDefineCases))]
    public void TestDefine(string[] symbols, (string Name, int Scope, int Index)[] expected)
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(symbols.Length));
        for (var i = 0; i < symbols.Length; i++)
        {
            var expectSymbol = expected[i];
            var expectedSymbol = new Symbol(
                expectSymbol.Name,
                (SymbolScope)expectSymbol.Scope,
                expectSymbol.Index
            );

            var symbol = global.Define(symbols[i]);
            if (symbol != expectedSymbol)
                Assert.Fail(
                    $"expected {expectSymbol.Name} to resolve to {expectedSymbol}, got={symbol}"
                );
        }

        Assert.Pass();
    }

    [Test]
    [TestCaseSource(nameof(TestDefineCases))]
    public void TestResolveGlobal(string[] symbols, (string Name, int Scope, int Index)[] expected)
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(symbols.Length));

        foreach (var symbol in symbols)
            global.Define(symbol);

        foreach (var sym in expected)
        {
            var expectedSymbol = new Symbol(sym.Name, (SymbolScope)sym.Scope, sym.Index);
            if (!global.Resolve(sym.Name, out var result))
                Assert.Fail($"name {sym.Name} not resolvable");

            if (result != expectedSymbol)
                Assert.Fail($"expected {sym.Name} to resolve to {expectedSymbol}, got={result}");
        }

        Assert.Pass();
    }

    private static readonly object[] TestResolveLocalCases =
    [
        new object[]
        {
            new[] { "a", "b" },
            new[] { "c", "d" },
            new (string Name, int Scope, int Index)[]
            {
                ("a", (int)SymbolScope.Global, 0),
                ("b", (int)SymbolScope.Global, 1),
                ("c", (int)SymbolScope.Local, 0),
                ("d", (int)SymbolScope.Local, 1),
            },
        },
    ];

    [Test]
    [TestCaseSource(nameof(TestResolveLocalCases))]
    public void TestResolveLocal(
        string[] globalSymbols,
        string[] localSymbols,
        (string Name, int Scope, int Index)[] expected
    )
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(globalSymbols.Length + localSymbols.Length));

        foreach (var symbol in globalSymbols)
            global.Define(symbol);

        var local = new SymbolTable(global);
        foreach (var symbol in localSymbols)
            local.Define(symbol);

        foreach (var sym in expected)
        {
            var expectedSymbol = new Symbol(sym.Name, (SymbolScope)sym.Scope, sym.Index);
            if (!local.Resolve(sym.Name, out var result))
                Assert.Fail($"name {sym.Name} not resolvable");

            if (result != expectedSymbol)
                Assert.Fail($"expected {sym.Name} to resolve to {expectedSymbol}, got={result}");
        }

        Assert.Pass();
    }
}

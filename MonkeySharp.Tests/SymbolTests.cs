using System.Collections.Generic;
using MonkeySharp.VirtualMachine;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class SymbolTests
{
    private static readonly object[] TestDefineCases =
    [
        new object[]
        {
            new[] {"a", "b"},
            new KeyValuePair<string, Symbol>[]
            {
                new("a", new Symbol("a", SymbolScope.Global, 0)),
                new("b", new Symbol("b", SymbolScope.Global, 1))
            }
        }
    ];

    [Test]
    [TestCaseSource(nameof(TestDefineCases))]
    public void TestDefine(string[] symbols, KeyValuePair<string, Symbol>[] expected)
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(symbols.Length));
        for (var i = 0; i < symbols.Length; i++)
        {
            var expectSymbol = expected[i];

            var symbol = global.Define(symbols[i]);
            if (symbol != expectSymbol.Value)
                Assert.Fail($"expected {expectSymbol.Key} to resolve to {expectSymbol.Value}, got={symbol}");
        }

        Assert.Pass();
    }

    [Test]
    [TestCaseSource(nameof(TestDefineCases))]
    public void TestResolveGlobal(string[] symbols, KeyValuePair<string, Symbol>[] expected)
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(symbols.Length));

        foreach (var symbol in symbols) global.Define(symbol);

        foreach (var sym in expected)
        {
            if (!global.Resolve(sym.Key, out var result))
            {
                Assert.Fail($"name {sym.Key} not resolvable");
                continue;
            }

            if (result != sym.Value) Assert.Fail($"expected {sym.Key} to resolve to {sym.Value}, got={result}");
        }

        Assert.Pass();
    }

    private static readonly object[] TestResolveLocalCases =
    [
        new object[]
        {
            new[] {"a", "b"},
            new[] {"c", "d"},
            new KeyValuePair<string, Symbol>[]
            {
                new("a", new Symbol("a", SymbolScope.Global, 0)),
                new("b", new Symbol("b", SymbolScope.Global, 1)),
                new("c", new Symbol("c", SymbolScope.Local, 0)),
                new("d", new Symbol("d", SymbolScope.Local, 1))
            }
        }
    ];

    [Test]
    [TestCaseSource(nameof(TestResolveLocalCases))]
    public void TestResolveLocal(string[] globalSymbols, string[] localSymbols,
        KeyValuePair<string, Symbol>[] expected)
    {
        var global = new SymbolTable();
        Assert.That(expected, Has.Length.EqualTo(globalSymbols.Length + localSymbols.Length));

        foreach (var symbol in globalSymbols) global.Define(symbol);

        var local = new SymbolTable(global);
        foreach (var symbol in localSymbols) local.Define(symbol);

        foreach (var sym in expected)
        {
            if (!local.Resolve(sym.Key, out var result))
            {
                Assert.Fail($"name {sym.Key} not resolvable");
                continue;
            }

            if (result != sym.Value) Assert.Fail($"expected {sym.Key} to resolve to {sym.Value}, got={result}");
        }

        Assert.Pass();
    }
}
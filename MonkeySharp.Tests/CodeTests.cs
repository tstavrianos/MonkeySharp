using System;
using MonkeySharp.VirtualMachine;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class CodeTests
{
    private static readonly object[] TestMakeCases =
    [
        new object[] {OpCode.Constant, new int[] {65534}, new byte[] {(byte) OpCode.Constant, 255, 254}},
        new object[] {OpCode.Add, Array.Empty<int>(), new byte[] {(byte) OpCode.Add}},
        new object[] {OpCode.GetLocal, new[] {255}, new byte[] {(byte) OpCode.GetLocal, 255}}
    ];

    [Test]
    [TestCaseSource(nameof(TestMakeCases))]
    public void TestMake(OpCode op, int[] operands, byte[] expected)
    {
        var instruction = Code.Make(op, operands);

        if (instruction.Length != expected.Length)
        {
            Assert.Fail($"instruction has wrong length. want={expected.Length}, got={instruction.Length}");
            return;
        }

        for (var i = 0; i < instruction.Length; i++)
            if (instruction[i] != expected[i])
                Assert.Fail($"wrong byte at pos {i}. want={(int) expected[i]}, got={(int) instruction[i]}");

        Assert.Pass();
    }
}
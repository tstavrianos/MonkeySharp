using System.Collections.Generic;
using MonkeySharp.Compiler;
using MonkeySharp.Interpreter;
using MonkeySharp.VirtualMachine;
using NUnit.Framework;
using InterpreterKind = MonkeySharp.Interpreter.MonkeyValueKind;
using InterpreterValue = MonkeySharp.Interpreter.MonkeyValue;
using VmKind = MonkeySharp.VirtualMachine.MonkeyValueKind;
using VmValue = MonkeySharp.VirtualMachine.MonkeyValue;

namespace MonkeySharp.Tests;

[TestFixture]
public class EmbeddingApiTests
{
    [Test]
    public void InterpreterSession_HostFunction_CanReturnInteger()
    {
        var session = new InterpreterSession();
        session.RegisterFunction(
            "hostAdd",
            2,
            args =>
                InterpreterValue.Integer(args[0].IntegerValue!.Value + args[1].IntegerValue!.Value)
        );

        var parsed = session.Compile("hostAdd(1, 2);");
        var result = session.Run(parsed);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Kind, Is.EqualTo(InterpreterKind.Integer));
        Assert.That(result.Value.IntegerValue, Is.EqualTo(3));
    }

    [Test]
    public void VmSession_HostFunction_CanReturnInteger()
    {
        var session = new VmSession();
        session.RegisterFunction(
            "hostAdd",
            2,
            args => VmValue.Integer(args[0].IntegerValue!.Value + args[1].IntegerValue!.Value)
        );

        var compiled = session.Compile("hostAdd(1, 2);");
        var result = session.Run(compiled);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Kind, Is.EqualTo(VmKind.Integer));
        Assert.That(result.Value.IntegerValue, Is.EqualTo(3));
    }

    [Test]
    public void InterpreterSession_HostFunction_CanRoundTripCompositeMonkeyValue()
    {
        var session = new InterpreterSession();
        session.RegisterFunction("echo", 1, args => args[0]);

        var parsed = session.Compile("echo({\"nums\": [1, 2, 3], \"ok\": true});");
        var result = session.Run(parsed);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Kind, Is.EqualTo(InterpreterKind.Hash));

        var hash = result.Value.HashPairs;
        Assert.That(hash, Is.Not.Null);

        var nums = hash![InterpreterValue.String("nums")];
        Assert.That(nums.Kind, Is.EqualTo(InterpreterKind.Array));
        Assert.That(nums.ArrayElements, Is.Not.Null);
        Assert.That(nums.ArrayElements!.Count, Is.EqualTo(3));
        Assert.That(nums.ArrayElements[0].IntegerValue, Is.EqualTo(1));
        Assert.That(nums.ArrayElements[1].IntegerValue, Is.EqualTo(2));
        Assert.That(nums.ArrayElements[2].IntegerValue, Is.EqualTo(3));

        var ok = hash[InterpreterValue.String("ok")];
        Assert.That(ok.Kind, Is.EqualTo(InterpreterKind.Boolean));
        Assert.That(ok.BooleanValue, Is.True);
    }

    [Test]
    public void VmSession_HostFunction_CanRoundTripCompositeMonkeyValue()
    {
        var session = new VmSession();
        session.RegisterFunction("echo", 1, args => args[0]);

        var compiled = session.Compile("echo({\"nums\": [1, 2, 3], \"ok\": true});");
        var result = session.Run(compiled);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Value.Kind, Is.EqualTo(VmKind.Hash));

        var hash = result.Value.HashPairs;
        Assert.That(hash, Is.Not.Null);

        var nums = hash![VmValue.String("nums")];
        Assert.That(nums.Kind, Is.EqualTo(VmKind.Array));
        Assert.That(nums.ArrayElements, Is.Not.Null);
        Assert.That(nums.ArrayElements!.Count, Is.EqualTo(3));
        Assert.That(nums.ArrayElements[0].IntegerValue, Is.EqualTo(1));
        Assert.That(nums.ArrayElements[1].IntegerValue, Is.EqualTo(2));
        Assert.That(nums.ArrayElements[2].IntegerValue, Is.EqualTo(3));

        var ok = hash[VmValue.String("ok")];
        Assert.That(ok.Kind, Is.EqualTo(VmKind.Boolean));
        Assert.That(ok.BooleanValue, Is.True);
    }

    [Test]
    public void CompilerSession_HostFunction_CanReturnInteger()
    {
        var session = new ILCompilerSession();
        session.RegisterFunction(
            "hostAdd",
            2,
            args => new MonkeyInteger(
                ((MonkeyInteger)args[0]).Value + ((MonkeyInteger)args[1]).Value
            )
        );

        var result = session.Compile("hostAdd(1, 2);");
        Assert.That(result.IsValid, Is.True);

        var runResult = session.Run(result);
        Assert.That(runResult.Success, Is.True, runResult.Error);
        Assert.That(runResult.Value, Is.InstanceOf<MonkeyInteger>());
        Assert.That(((MonkeyInteger)runResult.Value).Value, Is.EqualTo(3));
    }

    [Test]
    public void CompilerSession_HostFunction_CanRoundTripCompositeValue()
    {
        var session = new ILCompilerSession();
        session.RegisterFunction("echo", 1, args => args[0]);

        var result = session.Compile("echo({\"nums\": [1, 2, 3], \"ok\": true});");
        Assert.That(result.IsValid, Is.True);

        var runResult = session.Run(result);
        Assert.That(runResult.Success, Is.True, runResult.Error);
        Assert.That(runResult.Value, Is.InstanceOf<MonkeyHash>());

        var hash = ((MonkeyHash)runResult.Value).Pairs;
        var numsKey = new MonkeyString("nums");
        Assert.That(hash.ContainsKey(numsKey), Is.True);
        var nums = (MonkeyArray)hash[numsKey];
        Assert.That(nums.Elements.Length, Is.EqualTo(3));
        Assert.That(((MonkeyInteger)nums.Elements[0]).Value, Is.EqualTo(1));
        Assert.That(((MonkeyInteger)nums.Elements[1]).Value, Is.EqualTo(2));
        Assert.That(((MonkeyInteger)nums.Elements[2]).Value, Is.EqualTo(3));

        var okKey = new MonkeyString("ok");
        Assert.That(hash.ContainsKey(okKey), Is.True);
        Assert.That(((MonkeyBoolean)hash[okKey]).Value, Is.True);
    }

    [Test]
    public void CompilerSession_ParseError_ReturnsInvalidResult()
    {
        var session = new ILCompilerSession();
        var result = session.Compile("let = ;");
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Diagnostics.Count, Is.GreaterThan(0));
    }
}

using NUnit.Framework;
using IlKind = MonkeySharp.ReflectionEmit.MonkeyValueKind;
using IlValue = MonkeySharp.ReflectionEmit.MonkeyValue;
using InterpreterKind = MonkeySharp.TreeWalk.MonkeyValueKind;
using InterpreterValue = MonkeySharp.TreeWalk.MonkeyValue;
using Session = MonkeySharp.TreeWalk.Session;
using VmKind = MonkeySharp.BytecodeVm.MonkeyValueKind;
using VmValue = MonkeySharp.BytecodeVm.MonkeyValue;

namespace MonkeySharp.Tests;

[TestFixture]
public class EmbeddingApiTests
{
    [Test]
    public void TreeWalkSession_HostFunction_CanReturnInteger()
    {
        var session = new Session();
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
    public void BytecodeVmSession_HostFunction_CanReturnInteger()
    {
        var session = new BytecodeVm.Session();
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
    public void TreeWalkSession_HostFunction_CanRoundTripCompositeMonkeyValue()
    {
        var session = new Session();
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
    public void BytecodeVmSession_HostFunction_CanRoundTripCompositeMonkeyValue()
    {
        var session = new BytecodeVm.Session();
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
    public void ReflectionEmitSession_HostFunction_CanReturnInteger()
    {
        var session = new ReflectionEmit.Session();
        session.RegisterFunction(
            "hostAdd",
            2,
            args => IlValue.Integer(args[0].IntegerValue!.Value + args[1].IntegerValue!.Value)
        );

        var result = session.Compile("hostAdd(1, 2);");
        Assert.That(result.IsValid, Is.True);

        var runResult = session.Run(result);
        Assert.That(runResult.Success, Is.True, runResult.Error);
        Assert.That(runResult.Value.Kind, Is.EqualTo(IlKind.Integer));
        Assert.That(runResult.Value.IntegerValue, Is.EqualTo(3));
    }

    [Test]
    public void ReflectionEmitSession_HostFunction_CanRoundTripCompositeValue()
    {
        var session = new ReflectionEmit.Session();
        session.RegisterFunction("echo", 1, args => args[0]);

        var result = session.Compile("echo({\"nums\": [1, 2, 3], \"ok\": true});");
        Assert.That(result.IsValid, Is.True);

        var runResult = session.Run(result);
        Assert.That(runResult.Success, Is.True, runResult.Error);
        Assert.That(runResult.Value.Kind, Is.EqualTo(IlKind.Hash));

        var hash = runResult.Value.HashPairs;
        Assert.That(hash, Is.Not.Null);

        var numsKey = IlValue.String("nums");
        Assert.That(hash.ContainsKey(numsKey), Is.True);
        var nums = hash[numsKey];
        Assert.That(nums.Kind, Is.EqualTo(IlKind.Array));
        Assert.That(nums.ArrayElements, Is.Not.Null);
        Assert.That(nums.ArrayElements!.Count, Is.EqualTo(3));
        Assert.That(nums.ArrayElements[0].IntegerValue, Is.EqualTo(1));
        Assert.That(nums.ArrayElements[1].IntegerValue, Is.EqualTo(2));
        Assert.That(nums.ArrayElements[2].IntegerValue, Is.EqualTo(3));

        var okKey = IlValue.String("ok");
        Assert.That(hash.ContainsKey(okKey), Is.True);
        Assert.That(hash[okKey].Kind, Is.EqualTo(IlKind.Boolean));
        Assert.That(hash[okKey].BooleanValue, Is.True);
    }

    [Test]
    public void ReflectionEmitSession_ParseError_ReturnsInvalidResult()
    {
        var session = new ReflectionEmit.Session();
        var result = session.Compile("let = ;");
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Diagnostics.Count, Is.GreaterThan(0));
    }
}

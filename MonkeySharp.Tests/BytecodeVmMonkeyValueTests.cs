using System;
using System.Collections.Generic;
using MonkeySharp.BytecodeVm;
using NUnit.Framework;

namespace MonkeySharp.Tests;

[TestFixture]
public class BytecodeVmMonkeyValueTests
{
    [Test]
    public void DefaultMonkeyValue_IsSafeAndActsAsNull()
    {
        var value = default(MonkeyValue);

        Assert.That(value.Kind, Is.EqualTo(MonkeyValueKind.Null));
        Assert.That(value.Inspect, Is.EqualTo("null"));
        Assert.That(value.IntegerValue, Is.Null);
        Assert.That(value.StringValue, Is.Null);
        Assert.That(value.BooleanValue, Is.Null);
        Assert.That(value.ErrorMessage, Is.Null);

        Assert.DoesNotThrow(() => _ = value.GetHashCode());
        Assert.That(value.Equals(MonkeyValue.Null()), Is.True);
    }

    [Test]
    public void HashFactory_WithDefaultKey_ReturnsParityErrorMessage()
    {
        var pairs = new Dictionary<MonkeyValue, MonkeyValue> { [default] = MonkeyValue.Integer(1) };

        var value = MonkeyValue.Hash(pairs);

        Assert.That(value.Kind, Is.EqualTo(MonkeyValueKind.Error));
        Assert.That(value.ErrorMessage, Is.EqualTo("unusable as hash key in host value: NULL"));
    }

    [Test]
    public void HashFactory_WithArrayKey_ReturnsTypeNameInError()
    {
        var pairs = new Dictionary<MonkeyValue, MonkeyValue>
        {
            [MonkeyValue.Array(Array.Empty<MonkeyValue>())] = MonkeyValue.Integer(1),
        };

        var value = MonkeyValue.Hash(pairs);

        Assert.That(value.Kind, Is.EqualTo(MonkeyValueKind.Error));
        Assert.That(value.ErrorMessage, Is.EqualTo("unusable as hash key in host value: ARRAY"));
    }
}

using System;

namespace MonkeySharp.Compiler;

/// <summary>
/// Provides built-in runtime functions for MonkeySharp.
/// </summary>
public static class BuiltinFunctions
{
    /// <summary>
    /// Returns the length of a string or array.
    /// </summary>
    public static MonkeyObject len(MonkeyObject obj)
    {
        return obj switch
        {
            MonkeyString str => new MonkeyInteger(str.Value.Length),
            MonkeyArray arr => new MonkeyInteger(arr.Elements.Length),
            _ => new MonkeyError($"argument to 'len' not supported, got {obj.TypeName()}")
        };
    }

    /// <summary>
    /// Returns the first element of an array.
    /// </summary>
    public static MonkeyObject first(MonkeyObject obj)
    {
        if (obj is not MonkeyArray arr)
            return new MonkeyError($"argument to 'first' must be ARRAY, got {obj.TypeName()}");

        return arr.Elements.Length > 0 ? arr.Elements[0] : MonkeyNull.Instance;
    }

    /// <summary>
    /// Returns the last element of an array.
    /// </summary>
    public static MonkeyObject last(MonkeyObject obj)
    {
        if (obj is not MonkeyArray arr)
            return new MonkeyError($"argument to 'last' must be ARRAY, got {obj.TypeName()}");

        return arr.Elements.Length > 0 ? arr.Elements[^1] : MonkeyNull.Instance;
    }

    /// <summary>
    /// Returns all elements except the first.
    /// </summary>
    public static MonkeyObject rest(MonkeyObject obj)
    {
        if (obj is not MonkeyArray arr)
            return new MonkeyError($"argument to 'rest' must be ARRAY, got {obj.TypeName()}");

        if (arr.Elements.Length == 0)
            return MonkeyNull.Instance;

        var newElements = new MonkeyObject[arr.Elements.Length - 1];
        Array.Copy(arr.Elements, 1, newElements, 0, newElements.Length);
        return new MonkeyArray(newElements);
    }

    /// <summary>
    /// Appends an element to an array and returns a new array.
    /// </summary>
    public static MonkeyObject push(MonkeyObject arr, MonkeyObject element)
    {
        if (arr is not MonkeyArray array)
            return new MonkeyError($"argument to 'push' must be ARRAY, got {arr.TypeName()}");

        var newElements = new MonkeyObject[array.Elements.Length + 1];
        Array.Copy(array.Elements, newElements, array.Elements.Length);
        newElements[^1] = element;
        return new MonkeyArray(newElements);
    }

    /// <summary>
    /// Prints objects to the console.
    /// </summary>
    public static MonkeyObject puts(params MonkeyObject[] args)
    {
        foreach (var arg in args)
            Console.WriteLine(arg.Inspect());
        return MonkeyNull.Instance;
    }
}
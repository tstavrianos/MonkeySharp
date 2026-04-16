using System;
using System.Collections.Generic;

namespace MonkeySharp.Interpreter.Objects;

public static class Builtins
{
    private static readonly (string name, int arity, Value builtin)[] Data =
    [
        (
            "len",
            1,
            Value.Builtin(args =>
            {
                if (args.Count != 1)
                    return Value.Error($"wrong number of arguments. want=1, got={args.Count}");
                if (args[0].IsString)
                    return Value.Integer(args[0].StringValue!.Length);
                if (args[0].IsArray)
                    return Value.Integer(args[0].ArrayElements!.Count);
                if (args[0].IsHash)
                    return Value.Integer(args[0].HashPairs!.Count);

                return Value.Error($"argument to 'len' not supported, got {args[0].Type}");
            })
        ),
        (
            "first",
            1,
            Value.Builtin(args =>
            {
                if (args.Count != 1)
                    return Value.Error($"wrong number of arguments. want=1, got={args.Count}");
                if (!args[0].IsArray)
                    return Value.Error($"argument to 'first' must be ARRAY, got {args[0].Type}");
                var elements = args[0].ArrayElements!;
                if (elements.Count > 0)
                    return elements[0];
                return Value.NullValue;
            })
        ),
        (
            "last",
            1,
            Value.Builtin(args =>
            {
                if (args.Count != 1)
                    return Value.Error($"wrong number of arguments. want=1, got={args.Count}");
                if (!args[0].IsArray)
                    return Value.Error($"argument to 'last' must be ARRAY, got {args[0].Type}");
                var elements = args[0].ArrayElements!;
                if (elements.Count > 0)
                    return elements[^1];
                return Value.NullValue;
            })
        ),
        (
            "rest",
            1,
            Value.Builtin(args =>
            {
                if (args.Count != 1)
                    return Value.Error($"wrong number of arguments. want=1, got={args.Count}");
                if (!args[0].IsArray)
                    return Value.Error($"argument to 'rest' must be ARRAY, got {args[0].Type}");
                var elements = args[0].ArrayElements!;
                if (elements.Count > 0)
                    return Value.Array(elements.GetRange(1, elements.Count - 1));
                return Value.NullValue;
            })
        ),
        (
            "push",
            2,
            Value.Builtin(args =>
            {
                if (args.Count != 2)
                    return Value.Error($"wrong number of arguments. want=2, got={args.Count}");
                if (!args[0].IsArray)
                    return Value.Error($"argument to 'push' must be ARRAY, got {args[0].Type}");
                var elements = args[0].ArrayElements!;
                var newArray = new List<Value>(elements) { args[1] };
                return Value.Array(newArray);
            })
        ),
        (
            "add",
            3,
            Value.Builtin(args =>
            {
                if (args.Count != 3)
                    return Value.Error($"wrong number of arguments. want=3, got={args.Count}");
                if (!args[0].IsHash)
                    return Value.Error($"argument to 'add' must be HASH, got {args[0].Type}");
                if (!args[1].IsHashable)
                    return Value.Error($"argument to 'add' must be Hashable, got {args[1].Type}");

                var pairs = args[0].HashPairs!;
                var newHash = new Dictionary<HashKey, (Value Key, Value Value)>(pairs)
                {
                    { args[1].GetHashKey(), (args[1], args[2]) },
                };
                return Value.Hash(newHash);
            })
        ),
        (
            "puts",
            -1,
            Value.Builtin(args =>
            {
                foreach (var arg in args)
                    Console.WriteLine(arg.Inspect);
                return Value.NullValue;
            })
        ),
    ];

    public static bool TryGet(string name, out Value ret)
    {
        ret = default;
        for (var i = 0; i < Data.Length; i++)
            if (Data[i].name.Equals(name))
            {
                ret = Data[i].builtin;
                return true;
            }

        return false;
    }

    public static IEnumerable<(string, int)> NamesAndArguments()
    {
        foreach (var (key, arity, _) in Data)
            yield return (key, arity);
    }
}

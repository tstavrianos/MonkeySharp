using System;
using System.Collections.Generic;

namespace MonkeySharp.Core.Objects
{
    public static class Builtins
    {
        private static readonly (string name, BuiltinObject builtin)[] Data =
        {
            (
                "len", new BuiltinObject((args) =>
                {
                    if (args.Count != 1) return new ErrorObject($"wrong number of arguments. want=1, got={args.Count}");
                    switch (args[0])
                    {
                        case StringObject stringObject:
                            return new IntegerObject(stringObject.Value.Length);
                        case ArrayObject arrayObject:
                            return new IntegerObject(arrayObject.Elements.Count);
                        case HashObject hashObject:
                            return new IntegerObject(hashObject.Pairs.Count);
                    }

                    return new ErrorObject($"argument to 'len' not supported, got {args[0].Type}");
                })
            ),
            (
                "first", new BuiltinObject(args =>
                {
                    if (args.Count != 1) return new ErrorObject($"wrong number of arguments. want=1, got={args.Count}");
                    if (args[0] is not ArrayObject arrayObject)
                        return new ErrorObject($"argument to 'first' must be ARRAY, got {args[0].Type}");
                    if (arrayObject.Elements.Count > 0)
                        return arrayObject.Elements[0];
                    return NullObject.Null;
                })
            ),
            (
                "last", new BuiltinObject(args =>
                {
                    if (args.Count != 1) return new ErrorObject($"wrong number of arguments. want=1, got={args.Count}");
                    if (args[0] is not ArrayObject arrayObject)
                        return new ErrorObject($"argument to 'last' must be ARRAY, got {args[0].Type}");
                    if (arrayObject.Elements.Count > 0)
                        return arrayObject.Elements[^1];
                    return NullObject.Null;
                })
            ),
            (
                "rest", new BuiltinObject(args =>
                {
                    if (args.Count != 1) return new ErrorObject($"wrong number of arguments. want=1, got={args.Count}");
                    if (args[0] is not ArrayObject arrayObject)
                        return new ErrorObject($"argument to 'rest' must be ARRAY, got {args[0].Type}");
                    if (arrayObject.Elements.Count > 0)
                        return new ArrayObject(arrayObject.Elements.GetRange(1, arrayObject.Elements.Count - 1));
                    return NullObject.Null;
                })
            ),
            (
                "push", new BuiltinObject(args =>
                {
                    if (args.Count != 2) return new ErrorObject($"wrong number of arguments. want=2, got={args.Count}");
                    if (args[0] is not ArrayObject arrayObject)
                        return new ErrorObject($"argument to 'push' must be ARRAY, got {args[0].Type}");
                    var newArray = new List<IObject>(arrayObject.Elements) {args[1]};
                    return new ArrayObject(newArray);
                })
            ),
            (
                "add", new BuiltinObject(args =>
                {
                    if (args.Count != 3) return new ErrorObject($"wrong number of arguments. want=3, got={args.Count}");
                    if (args[0] is not HashObject hashObject)
                        return new ErrorObject($"argument to 'add' must be HASH, got {args[0].Type}");
                    if (args[1] is not IHashableObject hashableObject)
                        return new ErrorObject(
                            $"argument to 'add' must be Hashable, got {args[1].Type}");

                    var newHash = new Dictionary<HashKey, (IHashableObject, IObject)>(hashObject.Pairs)
                        {{hashableObject.HashKey(), (hashableObject, args[2])}};
                    return new HashObject(newHash);
                })
            ),
            (
                "puts", new BuiltinObject(args =>
                {
                    foreach (var arg in args) Console.WriteLine(arg.Inspect);
                    return NullObject.Null;
                })
            )
        };

        public static bool TryGet(string name, out BuiltinObject ret)
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

        public static IEnumerable<(string, int)> Keys()
        {
            for (var i = 0; i < Data.Length; i++) yield return (Data[i].name, i);
        }

        public static BuiltinObject ByIndex(int index)
        {
            return Data[index].builtin;
        }
    }
}
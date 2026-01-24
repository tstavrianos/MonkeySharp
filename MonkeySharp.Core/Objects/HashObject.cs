using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonkeySharp.Core.Objects
{
    public static partial class ObjectType
    {
        public const string Hash = "HASH";
    }

    public class HashObject : IObject
    {
        internal HashObject(Dictionary<HashKey, (IHashableObject Key, IObject Value)> pairs)
        {
            Pairs = pairs;
        }

        public string Type => ObjectType.Hash;
        public Dictionary<HashKey, (IHashableObject Key, IObject Value)> Pairs { get; }

        public string Inspect
        {
            get
            {
                var ss = new StringBuilder();
                ss.Append('{');
                ss.Append(string.Join(", ",
                    Pairs.OrderBy(x => x.Value.Key.Inspect)
                        .Select(x => $"{x.Value.Key.Inspect}: {x.Value.Value.Inspect}")));
                ss.Append('}');

                return ss.ToString();
            }
        }
    }
}
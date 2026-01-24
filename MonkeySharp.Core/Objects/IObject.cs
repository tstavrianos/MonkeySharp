namespace MonkeySharp.Core.Objects
{
    public interface IObject
    {
        string Type { get; }
        string Inspect { get; }
    }
}
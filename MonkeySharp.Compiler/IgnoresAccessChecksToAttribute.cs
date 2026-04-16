// The CLR recognises this attribute by its full type name and uses it to bypass
// visibility checks when the declaring assembly has the matching InternalsVisibleTo
// entry.  This is the standard pattern for dynamic-IL code that needs to call
// internal members of a host assembly.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class IgnoresAccessChecksToAttribute : Attribute
{
    public IgnoresAccessChecksToAttribute(string assemblyName)
    {
        AssemblyName = assemblyName;
    }

    public string AssemblyName { get; }
}

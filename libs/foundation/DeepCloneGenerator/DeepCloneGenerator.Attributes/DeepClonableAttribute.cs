using System;

namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Marks a class or struct for automatic DeepClone method generation.
    /// The type must be partial and have a parameterless constructor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class DeepClonableAttribute : Attribute
    {
    }
}

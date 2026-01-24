using System;

namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Container class for DeepClone option attributes.
    /// </summary>
    public static class DeepCloneOption
    {
        /// <summary>
        /// Marks a field or property to be ignored during deep cloning.
        /// The member will not be copied to the clone.
        /// </summary>
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        public sealed class IgnoreAttribute : Attribute
        {
        }

        /// <summary>
        /// Marks a field or property for shallow copy instead of deep copy.
        /// Only the reference is copied, not the object itself.
        /// </summary>
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        public sealed class ShallowAttribute : Attribute
        {
        }

        /// <summary>
        /// Marks a field or property as potentially containing circular references.
        /// Enables cycle tracking for this member to prevent stack overflow.
        /// </summary>
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        public sealed class CyclableAttribute : Attribute
        {
        }
    }
}

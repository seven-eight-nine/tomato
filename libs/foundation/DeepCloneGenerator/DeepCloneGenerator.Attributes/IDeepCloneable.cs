namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Interface for types that support deep cloning.
    /// Generated automatically for types marked with [DeepClonable].
    /// </summary>
    /// <typeparam name="T">The type being cloned.</typeparam>
    public interface IDeepCloneable<T> where T : class
    {
        /// <summary>
        /// Creates a deep clone of this instance.
        /// </summary>
        /// <returns>A new instance with all values deeply copied.</returns>
        T DeepClone();
    }
}

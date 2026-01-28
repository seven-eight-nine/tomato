namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Options applied to a member via attributes.
    /// </summary>
    internal enum CloneOption
    {
        /// <summary>
        /// No special option, use default behavior.
        /// </summary>
        None,

        /// <summary>
        /// Skip this member during cloning.
        /// </summary>
        Ignore,

        /// <summary>
        /// Use shallow copy for this member.
        /// </summary>
        Shallow,

        /// <summary>
        /// Enable cycle tracking for this member.
        /// </summary>
        Cyclable
    }

    /// <summary>
    /// Strategy for copying a member's value.
    /// </summary>
    internal enum CopyStrategy
    {
        // Basic strategies
        /// <summary>
        /// Direct value copy (primitives, enums, value types).
        /// </summary>
        ValueCopy,

        /// <summary>
        /// Reference copy for immutable types (string).
        /// </summary>
        ReferenceCopy,

        /// <summary>
        /// Cannot deep clone, use shallow copy with warning.
        /// </summary>
        ShallowWithWarning,

        /// <summary>
        /// Skip this member.
        /// </summary>
        Skip,

        // Nullable
        /// <summary>
        /// Nullable value type handling.
        /// </summary>
        Nullable,

        // Arrays
        /// <summary>
        /// Array type cloning.
        /// </summary>
        Array,

        /// <summary>
        /// Jagged array (T[][]) cloning.
        /// </summary>
        JaggedArray,

        /// <summary>
        /// Two-dimensional array (T[,]) cloning.
        /// </summary>
        MultiDimensionalArray2,

        /// <summary>
        /// Three-dimensional array (T[,,]) cloning.
        /// </summary>
        MultiDimensionalArray3,

        // Standard collections
        /// <summary>
        /// List&lt;T&gt; cloning.
        /// </summary>
        List,

        /// <summary>
        /// Dictionary&lt;K,V&gt; cloning.
        /// </summary>
        Dictionary,

        /// <summary>
        /// HashSet&lt;T&gt; cloning.
        /// </summary>
        HashSet,

        /// <summary>
        /// Queue&lt;T&gt; cloning.
        /// </summary>
        Queue,

        /// <summary>
        /// Stack&lt;T&gt; cloning.
        /// </summary>
        Stack,

        /// <summary>
        /// LinkedList&lt;T&gt; cloning.
        /// </summary>
        LinkedList,

        /// <summary>
        /// SortedList&lt;K,V&gt; cloning.
        /// </summary>
        SortedList,

        /// <summary>
        /// SortedDictionary&lt;K,V&gt; cloning.
        /// </summary>
        SortedDictionary,

        /// <summary>
        /// SortedSet&lt;T&gt; cloning.
        /// </summary>
        SortedSet,

        // Concurrent collections
        /// <summary>
        /// ConcurrentDictionary&lt;K,V&gt; cloning.
        /// </summary>
        ConcurrentDictionary,

        /// <summary>
        /// ConcurrentQueue&lt;T&gt; cloning.
        /// </summary>
        ConcurrentQueue,

        /// <summary>
        /// ConcurrentStack&lt;T&gt; cloning.
        /// </summary>
        ConcurrentStack,

        /// <summary>
        /// ConcurrentBag&lt;T&gt; cloning.
        /// </summary>
        ConcurrentBag,

        // Special collections
        /// <summary>
        /// ReadOnlyCollection&lt;T&gt; cloning.
        /// </summary>
        ReadOnlyCollection,

        /// <summary>
        /// ObservableCollection&lt;T&gt; cloning.
        /// </summary>
        ObservableCollection,

        /// <summary>
        /// Immutable collections (reference copy).
        /// </summary>
        ImmutableReference,

        // Deep Clone types
        /// <summary>
        /// Type has [DeepClonable] attribute - use generated DeepCloneInternal().
        /// </summary>
        DeepCloneable,

        /// <summary>
        /// Type implements IDeepCloneable manually (without [DeepClonable]) - use user-defined DeepClone().
        /// </summary>
        CustomDeepCloneable,

        // Generics
        /// <summary>
        /// Type parameter (T).
        /// </summary>
        TypeParameter,

        // Fallback
        /// <summary>
        /// General ICollection&lt;T&gt; handling.
        /// </summary>
        Collection
    }
}

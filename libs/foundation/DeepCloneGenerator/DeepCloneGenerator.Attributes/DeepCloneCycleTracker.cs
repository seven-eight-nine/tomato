using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Thread-safe cycle tracker for preventing infinite recursion during deep cloning.
    /// Uses ThreadStatic storage to ensure thread safety without locking.
    /// </summary>
    public static class DeepCloneCycleTracker
    {
        [ThreadStatic]
        private static Dictionary<object, object>? _cloneMap;

        private static Dictionary<object, object> CloneMap
        {
            get
            {
                if (_cloneMap == null)
                {
                    _cloneMap = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
                }
                return _cloneMap;
            }
        }

        /// <summary>
        /// Attempts to get an already-cloned object from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="original">The original object to look up.</param>
        /// <param name="clone">The cached clone if found.</param>
        /// <returns>True if a clone was found in the cache.</returns>
        public static bool TryGetClone<T>(T original, out T? clone) where T : class
        {
            if (original == null)
            {
                clone = null;
                return false;
            }

            if (CloneMap.TryGetValue(original, out var cachedClone))
            {
                clone = (T)cachedClone;
                return true;
            }

            clone = null;
            return false;
        }

        /// <summary>
        /// Registers a cloned object in the cache.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="original">The original object.</param>
        /// <param name="clone">The cloned object.</param>
        public static void Register<T>(T original, T clone) where T : class
        {
            if (original == null || clone == null)
            {
                return;
            }

            CloneMap[original] = clone;
        }

        /// <summary>
        /// Clears the cycle tracking cache.
        /// Must be called after a complete deep clone operation.
        /// </summary>
        public static void Clear()
        {
            _cloneMap?.Clear();
        }

        /// <summary>
        /// Reference equality comparer that uses RuntimeHelpers.GetHashCode
        /// and Object.ReferenceEquals for comparison.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer() { }

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}

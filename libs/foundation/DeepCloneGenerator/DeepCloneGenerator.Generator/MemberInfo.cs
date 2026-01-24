using Microsoft.CodeAnalysis;

namespace Tomato.DeepCloneGenerator
{
    /// <summary>
    /// Information about a member to be cloned.
    /// </summary>
    internal readonly struct MemberInfo
    {
        /// <summary>
        /// The name of the member.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The type of the member.
        /// </summary>
        public ITypeSymbol Type { get; }

        /// <summary>
        /// The copy strategy to use for this member.
        /// </summary>
        public CopyStrategy Strategy { get; }

        /// <summary>
        /// The clone option applied via attribute.
        /// </summary>
        public CloneOption Option { get; }

        /// <summary>
        /// Whether this is a field (true) or property (false).
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        /// Whether the member is readonly (for fields only).
        /// </summary>
        public bool IsReadonly { get; }

        /// <summary>
        /// The location of the member declaration for diagnostics.
        /// </summary>
        public Location? Location { get; }

        /// <summary>
        /// For array/list types, the element type.
        /// </summary>
        public ITypeSymbol? ElementType { get; }

        /// <summary>
        /// For dictionary types, the key type.
        /// </summary>
        public ITypeSymbol? KeyType { get; }

        /// <summary>
        /// For dictionary types, the value type.
        /// </summary>
        public ITypeSymbol? ValueType { get; }

        /// <summary>
        /// Whether the property is init-only (for properties only).
        /// </summary>
        public bool IsInitOnly { get; }

        /// <summary>
        /// Whether this member is an event.
        /// </summary>
        public bool IsEvent { get; }

        public MemberInfo(
            string name,
            ITypeSymbol type,
            CopyStrategy strategy,
            CloneOption option,
            bool isField,
            bool isReadonly,
            Location? location,
            ITypeSymbol? elementType = null,
            ITypeSymbol? keyType = null,
            ITypeSymbol? valueType = null,
            bool isInitOnly = false,
            bool isEvent = false)
        {
            Name = name;
            Type = type;
            Strategy = strategy;
            Option = option;
            IsField = isField;
            IsReadonly = isReadonly;
            Location = location;
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
            IsInitOnly = isInitOnly;
            IsEvent = isEvent;
        }
    }
}

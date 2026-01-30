using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tomato.DeepCloneGenerator
{
    internal static class TypeAnalyzer
    {
        private const string DeepClonableAttributeName = "Tomato.DeepCloneGenerator.DeepClonableAttribute";
        private const string IgnoreAttributeName = "Tomato.DeepCloneGenerator.DeepCloneOption.IgnoreAttribute";
        private const string ShallowAttributeName = "Tomato.DeepCloneGenerator.DeepCloneOption.ShallowAttribute";
        private const string CyclableAttributeName = "Tomato.DeepCloneGenerator.DeepCloneOption.CyclableAttribute";
        private const string IDeepCloneableInterfaceName = "Tomato.DeepCloneGenerator.IDeepCloneable`1";

        /// <summary>
        /// Checks if the type has the [DeepClonable] attribute.
        /// </summary>
        public static bool HasDeepClonableAttribute(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == DeepClonableAttributeName);
        }

        /// <summary>
        /// Checks if the type is declared as partial.
        /// </summary>
        public static bool IsPartial(INamedTypeSymbol typeSymbol)
        {
            foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is TypeDeclarationSyntax typeDecl)
                {
                    if (typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the type is file-scoped (C# 11).
        /// </summary>
        public static bool IsFileScoped(INamedTypeSymbol typeSymbol)
        {
            foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is TypeDeclarationSyntax typeDecl)
                {
                    if (typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.FileKeyword)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the type is abstract.
        /// </summary>
        public static bool IsAbstract(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.IsAbstract;
        }

        /// <summary>
        /// Checks if the type has an accessible parameterless constructor.
        /// </summary>
        public static bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
        {
            // Structs always have an implicit parameterless constructor
            if (typeSymbol.TypeKind == TypeKind.Struct)
            {
                return true;
            }

            // Check for explicit parameterless constructor
            foreach (var ctor in typeSymbol.Constructors)
            {
                if (ctor.Parameters.Length == 0 &&
                    !ctor.IsStatic &&
                    (ctor.DeclaredAccessibility == Accessibility.Public ||
                     ctor.DeclaredAccessibility == Accessibility.Internal ||
                     ctor.DeclaredAccessibility == Accessibility.Protected ||
                     ctor.DeclaredAccessibility == Accessibility.ProtectedOrInternal))
                {
                    return true;
                }
            }

            // If no constructors are defined, there's an implicit parameterless constructor
            var hasExplicitInstanceCtor = typeSymbol.Constructors.Any(c => !c.IsStatic);
            return !hasExplicitInstanceCtor;
        }

        /// <summary>
        /// Checks if the type has valid accessibility (public or internal).
        /// </summary>
        public static bool HasValidAccessibility(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.DeclaredAccessibility == Accessibility.Public ||
                   typeSymbol.DeclaredAccessibility == Accessibility.Internal;
        }

        /// <summary>
        /// Checks if a property is init-only.
        /// </summary>
        public static bool IsInitOnly(IPropertySymbol property)
        {
            return property.SetMethod != null && property.SetMethod.IsInitOnly;
        }

        /// <summary>
        /// Gets all clonable members from the type, including inherited members.
        /// </summary>
        public static List<MemberInfo> GetClonableMembers(INamedTypeSymbol typeSymbol, Compilation compilation)
        {
            var members = new List<MemberInfo>();
            var processedNames = new HashSet<string>();
            var currentType = typeSymbol;

            // Walk up the inheritance chain
            while (currentType != null)
            {
                // Process fields
                foreach (var field in currentType.GetMembers().OfType<IFieldSymbol>())
                {
                    if (field.IsStatic || field.IsConst)
                        continue;

                    // Skip backing fields (they're handled via properties)
                    if (field.AssociatedSymbol != null)
                        continue;

                    // Skip if already processed (overridden in derived class)
                    if (!processedNames.Add(field.Name))
                        continue;

                    // Check accessibility for inherited members
                    if (!SymbolEqualityComparer.Default.Equals(currentType, typeSymbol))
                    {
                        if (!IsAccessibleFrom(field, typeSymbol))
                            continue;
                    }

                    var option = GetCloneOption(field.GetAttributes());
                    var strategy = option == CloneOption.Ignore
                        ? CopyStrategy.Skip
                        : DetermineCopyStrategy(field.Type, option, compilation);
                    var location = field.Locations.FirstOrDefault();

                    var (elementType, keyType, valueType) = GetCollectionTypes(field.Type);

                    members.Add(new MemberInfo(
                        field.Name,
                        field.Type,
                        strategy,
                        option,
                        isField: true,
                        isReadonly: field.IsReadOnly,
                        location,
                        elementType,
                        keyType,
                        valueType));
                }

                // Process events
                foreach (var eventSymbol in currentType.GetMembers().OfType<IEventSymbol>())
                {
                    if (eventSymbol.IsStatic)
                        continue;

                    // Skip if already processed
                    if (!processedNames.Add(eventSymbol.Name))
                        continue;

                    // Check accessibility for inherited members
                    if (!SymbolEqualityComparer.Default.Equals(currentType, typeSymbol))
                    {
                        if (!IsAccessibleFrom(eventSymbol, typeSymbol))
                            continue;
                    }

                    var option = GetCloneOption(eventSymbol.GetAttributes());
                    // Events are always shallow copied with warning (unless ignored)
                    var strategy = option == CloneOption.Ignore
                        ? CopyStrategy.Skip
                        : CopyStrategy.ShallowWithWarning;
                    var location = eventSymbol.Locations.FirstOrDefault();

                    members.Add(new MemberInfo(
                        eventSymbol.Name,
                        eventSymbol.Type,
                        strategy,
                        option,
                        isField: false,
                        isReadonly: false,
                        location,
                        isEvent: true));
                }

                // Process properties
                foreach (var property in currentType.GetMembers().OfType<IPropertySymbol>())
                {
                    if (property.IsStatic || property.IsIndexer)
                        continue;

                    // Skip properties without getter or setter
                    if (property.GetMethod == null || property.SetMethod == null)
                        continue;

                    // Skip if already processed (overridden in derived class)
                    if (!processedNames.Add(property.Name))
                        continue;

                    // Check accessibility for inherited members
                    if (!SymbolEqualityComparer.Default.Equals(currentType, typeSymbol))
                    {
                        if (!IsAccessibleFrom(property, typeSymbol))
                            continue;
                    }

                    // Skip non-accessible setters
                    if (property.SetMethod.DeclaredAccessibility == Accessibility.Private)
                        continue;

                    var option = GetCloneOption(property.GetAttributes());
                    var strategy = option == CloneOption.Ignore
                        ? CopyStrategy.Skip
                        : DetermineCopyStrategy(property.Type, option, compilation);
                    var location = property.Locations.FirstOrDefault();

                    var (elementType, keyType, valueType) = GetCollectionTypes(property.Type);

                    // Mark init-only properties
                    var isInitOnly = IsInitOnly(property);

                    members.Add(new MemberInfo(
                        property.Name,
                        property.Type,
                        strategy,
                        option,
                        isField: false,
                        isReadonly: isInitOnly,
                        location,
                        elementType,
                        keyType,
                        valueType,
                        isInitOnly: isInitOnly));
                }

                // Move to base type
                if (currentType.BaseType?.SpecialType == SpecialType.System_Object)
                    break;
                currentType = currentType.BaseType;
            }

            return members;
        }

        /// <summary>
        /// Checks if a member is accessible from the target type.
        /// </summary>
        private static bool IsAccessibleFrom(ISymbol member, INamedTypeSymbol targetType)
        {
            var accessibility = member.DeclaredAccessibility;

            return accessibility switch
            {
                Accessibility.Public => true,
                Accessibility.Internal => true,
                Accessibility.Protected => IsInheritedMember(member, targetType),
                Accessibility.ProtectedOrInternal => true,
                Accessibility.ProtectedAndInternal => IsInheritedMember(member, targetType),
                _ => false
            };
        }

        private static bool IsInheritedMember(ISymbol member, INamedTypeSymbol targetType)
        {
            var containingType = member.ContainingType;
            var current = targetType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, containingType))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        /// <summary>
        /// Determines the copy strategy for a given type.
        /// </summary>
        public static CopyStrategy DetermineCopyStrategy(ITypeSymbol type, CloneOption option, Compilation compilation)
        {
            if (option == CloneOption.Shallow)
            {
                return CopyStrategy.ReferenceCopy;
            }

            // Type parameters
            if (type is ITypeParameterSymbol typeParam)
            {
                // Check for IDeepCloneable<T> constraint
                foreach (var constraint in typeParam.ConstraintTypes)
                {
                    if (constraint.OriginalDefinition.ToDisplayString().StartsWith("Tomato.DeepCloneGenerator.IDeepCloneable<"))
                    {
                        return CopyStrategy.DeepCloneable;
                    }
                }
                return CopyStrategy.TypeParameter;
            }

            // Handle nullable
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var namedType = (INamedTypeSymbol)type;
                var underlyingType = namedType.TypeArguments[0];
                var underlyingStrategy = DetermineCopyStrategy(underlyingType, CloneOption.None, compilation);
                if (underlyingStrategy == CopyStrategy.ValueCopy)
                {
                    return CopyStrategy.ValueCopy;
                }
                return CopyStrategy.Nullable;
            }

            // Primitives and enums
            if (IsPrimitiveOrEnum(type))
            {
                return CopyStrategy.ValueCopy;
            }

            // String (immutable, reference copy is fine)
            if (type.SpecialType == SpecialType.System_String)
            {
                return CopyStrategy.ReferenceCopy;
            }

            // Known immutable types
            if (IsKnownImmutableType(type))
            {
                return type.IsValueType ? CopyStrategy.ValueCopy : CopyStrategy.ReferenceCopy;
            }

            // Tuple (immutable)
            if (type is INamedTypeSymbol tupleType)
            {
                var fullName = tupleType.OriginalDefinition.ToDisplayString();
                if (fullName.StartsWith("System.Tuple<"))
                {
                    return CopyStrategy.ReferenceCopy;
                }
                if (fullName.StartsWith("System.ValueTuple<") || tupleType.IsTupleType)
                {
                    return CopyStrategy.ValueCopy;
                }
            }

            // Delegate types
            if (type.TypeKind == TypeKind.Delegate)
            {
                return CopyStrategy.ShallowWithWarning;
            }

            // Immutable collections
            if (type.ContainingNamespace?.ToDisplayString() == "System.Collections.Immutable")
            {
                return CopyStrategy.ImmutableReference;
            }

            // Value types (structs) without reference type fields can be value copied
            if (type.IsValueType)
            {
                if (type is INamedTypeSymbol namedStruct && namedStruct.IsReadOnly)
                {
                    return CopyStrategy.ValueCopy;
                }
                if (!ContainsReferenceTypeFields(type))
                {
                    return CopyStrategy.ValueCopy;
                }
            }

            // Arrays
            if (type is IArrayTypeSymbol arrayType)
            {
                // Jagged array
                if (arrayType.ElementType is IArrayTypeSymbol)
                {
                    return CopyStrategy.JaggedArray;
                }
                // Multi-dimensional array
                if (arrayType.Rank == 2)
                {
                    return CopyStrategy.MultiDimensionalArray2;
                }
                if (arrayType.Rank == 3)
                {
                    return CopyStrategy.MultiDimensionalArray3;
                }
                if (arrayType.Rank > 3)
                {
                    return CopyStrategy.ShallowWithWarning;
                }
                return CopyStrategy.Array;
            }

            // Generic collections
            if (type is INamedTypeSymbol namedType2)
            {
                var fullName = namedType2.OriginalDefinition.ToDisplayString();

                // Standard collections
                if (fullName == "System.Collections.Generic.List<T>")
                    return CopyStrategy.List;
                if (fullName == "System.Collections.Generic.Dictionary<TKey, TValue>")
                    return CopyStrategy.Dictionary;
                if (fullName == "System.Collections.Generic.HashSet<T>")
                    return CopyStrategy.HashSet;
                if (fullName == "System.Collections.Generic.Queue<T>")
                    return CopyStrategy.Queue;
                if (fullName == "System.Collections.Generic.Stack<T>")
                    return CopyStrategy.Stack;
                if (fullName == "System.Collections.Generic.LinkedList<T>")
                    return CopyStrategy.LinkedList;
                if (fullName == "System.Collections.Generic.SortedList<TKey, TValue>")
                    return CopyStrategy.SortedList;
                if (fullName == "System.Collections.Generic.SortedDictionary<TKey, TValue>")
                    return CopyStrategy.SortedDictionary;
                if (fullName == "System.Collections.Generic.SortedSet<T>")
                    return CopyStrategy.SortedSet;

                // Concurrent collections
                if (fullName == "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>")
                    return CopyStrategy.ConcurrentDictionary;
                if (fullName == "System.Collections.Concurrent.ConcurrentQueue<T>")
                    return CopyStrategy.ConcurrentQueue;
                if (fullName == "System.Collections.Concurrent.ConcurrentStack<T>")
                    return CopyStrategy.ConcurrentStack;
                if (fullName == "System.Collections.Concurrent.ConcurrentBag<T>")
                    return CopyStrategy.ConcurrentBag;

                // Special collections
                if (fullName == "System.Collections.ObjectModel.ReadOnlyCollection<T>")
                    return CopyStrategy.ReadOnlyCollection;
                if (fullName == "System.Collections.ObjectModel.ObservableCollection<T>")
                    return CopyStrategy.ObservableCollection;
            }

            // Check if type has [DeepClonable] attribute or implements IDeepCloneable<T>
            if (type is INamedTypeSymbol namedType3)
            {
                // [DeepClonable] attribute → use generated DeepCloneInternal()
                if (HasDeepClonableAttribute(namedType3))
                {
                    return CopyStrategy.DeepCloneable;
                }

                // IDeepCloneable<T> without [DeepClonable] → use user-defined DeepClone()
                if (ImplementsIDeepCloneable(namedType3))
                {
                    return CopyStrategy.CustomDeepCloneable;
                }
            }

            // Cannot deep clone, use shallow copy with warning
            return CopyStrategy.ShallowWithWarning;
        }

        /// <summary>
        /// Checks if type implements IDeepCloneable&lt;T&gt;.
        /// </summary>
        private static bool ImplementsIDeepCloneable(INamedTypeSymbol typeSymbol)
        {
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                // Check using multiple methods for robustness
                var originalDef = iface.OriginalDefinition;

                // Method 1: Check full display string
                if (originalDef.ToDisplayString() == IDeepCloneableInterfaceName)
                {
                    return true;
                }

                // Method 2: Check metadata name and namespace
                if (originalDef.MetadataName == "IDeepCloneable`1" &&
                    originalDef.ContainingNamespace?.ToDisplayString() == "Tomato.DeepCloneGenerator")
                {
                    return true;
                }

                // Method 3: Check by name pattern (for source-defined interfaces)
                if (originalDef.Name == "IDeepCloneable" &&
                    originalDef.Arity == 1 &&
                    originalDef.ContainingNamespace?.Name == "DeepCloneGenerator")
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if type is a primitive or enum.
        /// </summary>
        private static bool IsPrimitiveOrEnum(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
                return true;

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_Char:
                case SpecialType.System_DateTime:
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks if type is a known immutable type.
        /// </summary>
        private static bool IsKnownImmutableType(ITypeSymbol type)
        {
            var fullName = type.ToDisplayString();
            return fullName switch
            {
                "System.DateTime" => true,
                "System.DateTimeOffset" => true,
                "System.TimeSpan" => true,
                "System.Guid" => true,
                "System.Uri" => true,
                "System.Version" => true,
                "System.Text.RegularExpressions.Regex" => true,
                "System.Numerics.BigInteger" => true,
                "System.Numerics.Complex" => true,
                "System.Net.IPAddress" => true,
                "System.Type" => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a value type contains reference type fields.
        /// </summary>
        private static bool ContainsReferenceTypeFields(ITypeSymbol type)
        {
            if (!type.IsValueType)
                return true;

            if (type is INamedTypeSymbol namedType)
            {
                foreach (var member in namedType.GetMembers())
                {
                    if (member is IFieldSymbol field && !field.IsStatic)
                    {
                        if (!field.Type.IsValueType && field.Type.SpecialType != SpecialType.System_String)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the clone option from member attributes.
        /// </summary>
        private static CloneOption GetCloneOption(ImmutableArray<AttributeData> attributes)
        {
            foreach (var attr in attributes)
            {
                var attrName = attr.AttributeClass?.ToDisplayString();
                if (attrName == IgnoreAttributeName)
                    return CloneOption.Ignore;
                if (attrName == ShallowAttributeName)
                    return CloneOption.Shallow;
                if (attrName == CyclableAttributeName)
                    return CloneOption.Cyclable;
            }
            return CloneOption.None;
        }

        /// <summary>
        /// Gets collection element/key/value types.
        /// </summary>
        private static (ITypeSymbol? elementType, ITypeSymbol? keyType, ITypeSymbol? valueType) GetCollectionTypes(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                return (arrayType.ElementType, null, null);
            }

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var fullName = namedType.OriginalDefinition.ToDisplayString();

                // Single element collections
                if (fullName == "System.Collections.Generic.List<T>" ||
                    fullName == "System.Collections.Generic.HashSet<T>" ||
                    fullName == "System.Collections.Generic.Queue<T>" ||
                    fullName == "System.Collections.Generic.Stack<T>" ||
                    fullName == "System.Collections.Generic.LinkedList<T>" ||
                    fullName == "System.Collections.Generic.SortedSet<T>" ||
                    fullName == "System.Collections.Concurrent.ConcurrentQueue<T>" ||
                    fullName == "System.Collections.Concurrent.ConcurrentStack<T>" ||
                    fullName == "System.Collections.Concurrent.ConcurrentBag<T>" ||
                    fullName == "System.Collections.ObjectModel.ReadOnlyCollection<T>" ||
                    fullName == "System.Collections.ObjectModel.ObservableCollection<T>")
                {
                    return (namedType.TypeArguments[0], null, null);
                }

                // Key-value collections
                if (fullName == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
                    fullName == "System.Collections.Generic.SortedList<TKey, TValue>" ||
                    fullName == "System.Collections.Generic.SortedDictionary<TKey, TValue>" ||
                    fullName == "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>")
                {
                    return (null, namedType.TypeArguments[0], namedType.TypeArguments[1]);
                }
            }

            return (null, null, null);
        }

        /// <summary>
        /// Checks if any member has the Cyclable option.
        /// </summary>
        public static bool HasAnyCyclable(List<MemberInfo> members)
        {
            return members.Any(m => m.Option == CloneOption.Cyclable);
        }
    }
}

using Microsoft.CodeAnalysis;

namespace Tomato.DeepCloneGenerator
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "DeepCloneGenerator";

        /// <summary>
        /// DCG001: Type must be partial.
        /// </summary>
        public static readonly DiagnosticDescriptor PartialRequired = new DiagnosticDescriptor(
            id: "DCG001",
            title: "Type must be partial",
            messageFormat: "Type '{0}' must be declared as partial to use [DeepClonable]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG002: Type must have a parameterless constructor.
        /// </summary>
        public static readonly DiagnosticDescriptor ParameterlessConstructorRequired = new DiagnosticDescriptor(
            id: "DCG002",
            title: "Parameterless constructor required",
            messageFormat: "Type '{0}' must have an accessible parameterless constructor to use [DeepClonable]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG003: Type must be public or internal.
        /// </summary>
        public static readonly DiagnosticDescriptor InvalidAccessibility = new DiagnosticDescriptor(
            id: "DCG003",
            title: "Invalid type accessibility",
            messageFormat: "Type '{0}' must be public or internal to use [DeepClonable]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG101: Member cannot be deep cloned, shallow copy will be used.
        /// </summary>
        public static readonly DiagnosticDescriptor ShallowCopyWarning = new DiagnosticDescriptor(
            id: "DCG101",
            title: "Shallow copy used",
            messageFormat: "Member '{0}' of type '{1}' cannot be deep cloned; shallow copy will be used",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG102: Readonly field will be skipped.
        /// </summary>
        public static readonly DiagnosticDescriptor ReadonlyFieldSkipped = new DiagnosticDescriptor(
            id: "DCG102",
            title: "Readonly field skipped",
            messageFormat: "Readonly field '{0}' will be skipped during deep cloning",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG004: Abstract class is not supported.
        /// </summary>
        public static readonly DiagnosticDescriptor AbstractClassNotSupported = new DiagnosticDescriptor(
            id: "DCG004",
            title: "Abstract class not supported",
            messageFormat: "Abstract class '{0}' cannot use [DeepClonable]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG005: Init-only property is not supported.
        /// </summary>
        public static readonly DiagnosticDescriptor InitOnlyPropertyNotSupported = new DiagnosticDescriptor(
            id: "DCG005",
            title: "Init-only property not supported",
            messageFormat: "Init-only property '{0}' cannot be cloned; use a regular setter or [DeepCloneOption.Ignore]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG006: File-scoped type is not supported.
        /// </summary>
        public static readonly DiagnosticDescriptor FileScopedTypeNotSupported = new DiagnosticDescriptor(
            id: "DCG006",
            title: "File-scoped type not supported",
            messageFormat: "File-scoped type '{0}' cannot use [DeepClonable]",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG103: Delegate type is shallow copied.
        /// </summary>
        public static readonly DiagnosticDescriptor DelegateShallowCopy = new DiagnosticDescriptor(
            id: "DCG103",
            title: "Delegate shallow copy",
            messageFormat: "Delegate type '{0}' will be shallow copied",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// DCG104: Event is shallow copied.
        /// </summary>
        public static readonly DiagnosticDescriptor EventShallowCopy = new DiagnosticDescriptor(
            id: "DCG104",
            title: "Event shallow copy",
            messageFormat: "Event '{0}' will be shallow copied; handler registrations will be shared",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}

using Microsoft.CodeAnalysis;

namespace Tomato.CommandGenerator;

/// <summary>
/// コンパイル時診断メッセージの定義
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "CommandGenerator";

    public static readonly DiagnosticDescriptor CommandQueueMustBePartial = new DiagnosticDescriptor(
        id: "CG0001",
        title: "CommandQueue must be a partial class",
        messageFormat: "Class '{0}' with [CommandQueue] attribute must be declared as partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandMethodMustBePartial = new DiagnosticDescriptor(
        id: "CG0002",
        title: "CommandMethod must be a partial method",
        messageFormat: "Method '{0}' with [CommandMethod] attribute must be declared as partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandMustBePartial = new DiagnosticDescriptor(
        id: "CG0003",
        title: "Command must be a partial class",
        messageFormat: "Class '{0}' with [Command<T>] attribute must be declared as partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeArgumentMustBeCommandQueue = new DiagnosticDescriptor(
        id: "CG0004",
        title: "Type argument must be a CommandQueue",
        messageFormat: "Type argument '{0}' in [Command<T>] must be a class decorated with [CommandQueue]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandMustImplementMethod = new DiagnosticDescriptor(
        id: "CG0005",
        title: "Command must implement interface method",
        messageFormat: "Command '{0}' must implement method '{1}' for queue '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateCommandAttribute = new DiagnosticDescriptor(
        id: "CG0006",
        title: "Duplicate Command attribute for same queue",
        messageFormat: "Class '{0}' has duplicate [Command<{1}>] attributes",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnableToAnalyzeFieldInitializer = new DiagnosticDescriptor(
        id: "CG0007",
        title: "Unable to analyze field initializer",
        messageFormat: "Unable to analyze field initializer for field '{0}' in class '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

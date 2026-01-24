using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tomato.DeepCloneGenerator
{
    [Generator]
    public sealed class DeepCloneSourceGenerator : IIncrementalGenerator
    {
        private const string DeepClonableAttributeName = "Tomato.DeepCloneGenerator.DeepClonableAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all type declarations with [DeepClonable] attribute
            var typeDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
                DeepClonableAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => GetTypeToGenerate(ctx))
                .Where(static t => t is not null);

            // Combine with compilation
            var compilationAndTypes = context.CompilationProvider.Combine(typeDeclarations.Collect());

            // Generate source
            context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => Execute(source.Left, source.Right!, spc));
        }

        private static INamedTypeSymbol? GetTypeToGenerate(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return null;

            return typeSymbol;
        }

        private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol?> types, SourceProductionContext context)
        {
            if (types.IsDefaultOrEmpty)
                return;

            foreach (var typeSymbol in types.Distinct(SymbolEqualityComparer.Default))
            {
                if (typeSymbol is null)
                    continue;

                ProcessType((INamedTypeSymbol)typeSymbol, compilation, context);
            }
        }

        private static void ProcessType(INamedTypeSymbol typeSymbol, Compilation compilation, SourceProductionContext context)
        {
            // Validate: must be partial
            if (!TypeAnalyzer.IsPartial(typeSymbol))
            {
                var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.PartialRequired,
                    location,
                    typeSymbol.Name));
                return;
            }

            // Validate: must not be abstract
            if (TypeAnalyzer.IsAbstract(typeSymbol))
            {
                var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AbstractClassNotSupported,
                    location,
                    typeSymbol.Name));
                return;
            }

            // Validate: must not be file-scoped
            if (TypeAnalyzer.IsFileScoped(typeSymbol))
            {
                var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.FileScopedTypeNotSupported,
                    location,
                    typeSymbol.Name));
                return;
            }

            // Validate: must have parameterless constructor
            if (!TypeAnalyzer.HasParameterlessConstructor(typeSymbol))
            {
                var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ParameterlessConstructorRequired,
                    location,
                    typeSymbol.Name));
                return;
            }

            // Validate: must be public or internal
            if (!TypeAnalyzer.HasValidAccessibility(typeSymbol))
            {
                var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidAccessibility,
                    location,
                    typeSymbol.Name));
                return;
            }

            // Get clonable members
            var members = TypeAnalyzer.GetClonableMembers(typeSymbol, compilation);

            // Report diagnostics for warnings and errors
            bool hasErrors = false;
            foreach (var member in members)
            {
                // Init-only property error
                if (member.IsInitOnly && member.Option != CloneOption.Ignore && member.Location != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InitOnlyPropertyNotSupported,
                        member.Location,
                        member.Name));
                    hasErrors = true;
                }

                // Event shallow copy warning
                if (member.IsEvent && member.Option != CloneOption.Ignore && member.Location != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.EventShallowCopy,
                        member.Location,
                        member.Name));
                }
                // Shallow copy warning (non-event)
                else if (member.Strategy == CopyStrategy.ShallowWithWarning && member.Location != null)
                {
                    // Check if it's a delegate type for specific warning
                    if (member.Type.TypeKind == TypeKind.Delegate)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DelegateShallowCopy,
                            member.Location,
                            member.Type.ToDisplayString()));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.ShallowCopyWarning,
                            member.Location,
                            member.Name,
                            member.Type.ToDisplayString()));
                    }
                }

                // Readonly field warning
                if (member.IsReadonly && !member.IsInitOnly && member.Location != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ReadonlyFieldSkipped,
                        member.Location,
                        member.Name));
                }
            }

            if (hasErrors)
                return;

            // Check if any member has Cyclable option
            var hasCyclable = TypeAnalyzer.HasAnyCyclable(members);

            // Generate source code
            var source = CodeEmitter.Generate(typeSymbol, members, hasCyclable);

            // Add source to compilation
            var hintName = GetHintName(typeSymbol);
            context.AddSource(hintName, source);
        }

        private static string GetHintName(INamedTypeSymbol typeSymbol)
        {
            var ns = typeSymbol.ContainingNamespace;
            var namespacePart = ns.IsGlobalNamespace ? "" : $"{ns.ToDisplayString()}.";

            // Handle nested types
            var containingTypePart = "";
            var containingType = typeSymbol.ContainingType;
            while (containingType != null)
            {
                containingTypePart = $"{containingType.Name}." + containingTypePart;
                containingType = containingType.ContainingType;
            }

            return $"{namespacePart}{containingTypePart}{typeSymbol.Name}.DeepClone.g.cs";
        }
    }
}

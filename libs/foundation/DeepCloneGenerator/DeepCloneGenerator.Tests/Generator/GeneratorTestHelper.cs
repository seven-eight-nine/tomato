using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    internal static class GeneratorTestHelper
    {
        private static readonly MetadataReference[] DefaultReferences;

        static GeneratorTestHelper()
        {
            var references = new List<MetadataReference>();

            // Get the runtime directory
            var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

            // Add core references from the runtime directory
            var coreAssemblies = new[]
            {
                "System.Runtime.dll",
                "System.Collections.dll",
                "System.Collections.Concurrent.dll",
                "System.ObjectModel.dll",
                "System.Private.CoreLib.dll",
                "netstandard.dll"
            };

            foreach (var assembly in coreAssemblies)
            {
                var path = Path.Combine(runtimeDir, assembly);
                if (File.Exists(path))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }

            // Add DeepCloneGenerator.Attributes reference
            references.Add(MetadataReference.CreateFromFile(typeof(DeepClonableAttribute).Assembly.Location));

            DefaultReferences = references.ToArray();
        }

        public static (ImmutableArray<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                DefaultReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new DeepCloneSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);

            var generatedSources = driver.GetRunResult()
                .GeneratedTrees
                .Select(t => t.GetText().ToString())
                .ToArray();

            return (diagnostics, generatedSources);
        }

        public static (ImmutableArray<Diagnostic> CompilationDiagnostics, Compilation OutputCompilation) GetCompiledResult(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                DefaultReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new DeepCloneSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out _);

            var compDiags = outputCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            return (compDiags, outputCompilation);
        }
    }
}

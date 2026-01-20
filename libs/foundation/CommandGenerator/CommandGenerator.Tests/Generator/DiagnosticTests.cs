using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Generator;

/// <summary>
/// Diagnostic descriptor tests - t-wada style comprehensive coverage
/// Tests for diagnostic descriptors CG0001, CG0002, CG0003
/// Note: Current generator behavior skips non-partial declarations without emitting diagnostics.
/// These tests verify the diagnostic descriptors are properly defined.
/// </summary>
public class DiagnosticTests
{
    #region DiagnosticDescriptor Definition Tests

    [Fact]
    public void CG0001_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial;

        Assert.Equal("CG0001", descriptor.Id);
    }

    [Fact]
    public void CG0001_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0001_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void CG0001_Descriptor_ShouldHaveCorrectTitle()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial;

        Assert.Equal("CommandQueue must be a partial class", descriptor.Title.ToString());
    }

    [Fact]
    public void CG0001_Descriptor_ShouldHaveCorrectCategory()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial;

        Assert.Equal("CommandGenerator", descriptor.Category);
    }

    [Fact]
    public void CG0002_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial;

        Assert.Equal("CG0002", descriptor.Id);
    }

    [Fact]
    public void CG0002_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0002_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void CG0002_Descriptor_ShouldHaveCorrectTitle()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial;

        Assert.Equal("CommandMethod must be a partial method", descriptor.Title.ToString());
    }

    [Fact]
    public void CG0002_Descriptor_ShouldHaveCorrectCategory()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial;

        Assert.Equal("CommandGenerator", descriptor.Category);
    }

    [Fact]
    public void CG0003_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial;

        Assert.Equal("CG0003", descriptor.Id);
    }

    [Fact]
    public void CG0003_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0003_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void CG0003_Descriptor_ShouldHaveCorrectTitle()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial;

        Assert.Equal("Command must be a partial class", descriptor.Title.ToString());
    }

    [Fact]
    public void CG0003_Descriptor_ShouldHaveCorrectCategory()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial;

        Assert.Equal("CommandGenerator", descriptor.Category);
    }

    #endregion

    #region CG0004: TypeArgument Must Be CommandQueue Tests

    [Fact]
    public void CG0004_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue;

        Assert.Equal("CG0004", descriptor.Id);
    }

    [Fact]
    public void CG0004_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0004_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    #endregion

    #region CG0005: Command Must Implement Method Tests

    [Fact]
    public void CG0005_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod;

        Assert.Equal("CG0005", descriptor.Id);
    }

    [Fact]
    public void CG0005_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0005_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    #endregion

    #region CG0006: Duplicate Command Attribute Tests

    [Fact]
    public void CG0006_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute;

        Assert.Equal("CG0006", descriptor.Id);
    }

    [Fact]
    public void CG0006_Descriptor_ShouldHaveErrorSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute;

        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0006_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    #endregion

    #region CG0007: Unable to Analyze Field Initializer Tests

    [Fact]
    public void CG0007_Descriptor_ShouldHaveCorrectId()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer;

        Assert.Equal("CG0007", descriptor.Id);
    }

    [Fact]
    public void CG0007_Descriptor_ShouldHaveWarningSeverity()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer;

        // CG0007 is a warning, not an error
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
    }

    [Fact]
    public void CG0007_Descriptor_ShouldBeEnabledByDefault()
    {
        var descriptor = Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer;

        Assert.True(descriptor.IsEnabledByDefault);
    }

    #endregion

    #region Generator Behavior Tests - Partial Classes

    [Fact]
    public void PartialCommandQueue_ShouldNotEmitDiagnostics()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class PartialQueue
    {
        [CommandMethod]
        public partial void Execute();
    }
}";

        var diagnostics = GetGeneratorDiagnostics(source);
        var cgDiagnostics = diagnostics.Where(d => d.Id.StartsWith("CG")).ToList();

        Assert.Empty(cgDiagnostics);
    }

    [Fact]
    public void PartialCommand_ShouldNotEmitDiagnostics()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute();
    }

    [Command<TestQueue>]
    public partial class PartialCommand
    {
        public void Execute() { }
    }
}";

        var diagnostics = GetGeneratorDiagnostics(source);
        var cgDiagnostics = diagnostics.Where(d => d.Id.StartsWith("CG")).ToList();

        Assert.Empty(cgDiagnostics);
    }

    [Fact]
    public void PartialCommandMethod_ShouldNotEmitDiagnostics()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute();

        [CommandMethod(false)]
        public partial void Preview();
    }
}";

        var diagnostics = GetGeneratorDiagnostics(source);
        var cgDiagnostics = diagnostics.Where(d => d.Id.StartsWith("CG")).ToList();

        Assert.Empty(cgDiagnostics);
    }

    #endregion

    #region Generator Behavior Tests - Non-Partial Classes (Current Behavior)

    [Fact]
    public void NonPartialCommandQueue_CurrentBehavior_SkipsGenerationSilently()
    {
        // Current generator behavior: Non-partial CommandQueue is silently skipped
        // No code is generated for it, and no diagnostics are emitted
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public class NonPartialQueue
    {
        [CommandMethod]
        public void Execute() { }
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        // No CG diagnostics should be emitted (current behavior)
        var cgDiagnostics = diagnostics.Where(d => d.Id.StartsWith("CG")).ToList();
        Assert.Empty(cgDiagnostics);

        // Only 1 syntax tree (original source) - no generated code
        Assert.Single(compilation.SyntaxTrees);
    }

    [Fact]
    public void NonPartialCommand_CurrentBehavior_SkipsGenerationSilently()
    {
        // Current generator behavior: Non-partial Command is silently skipped
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute();
    }

    [Command<TestQueue>]
    public class NonPartialCommand
    {
        public void Execute() { }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        // No CG diagnostics should be emitted for the non-partial command
        var cgDiagnostics = diagnostics.Where(d => d.Id.StartsWith("CG")).ToList();
        Assert.Empty(cgDiagnostics);
    }

    #endregion

    #region All Descriptors Tests

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveUniqueIds()
    {
        var descriptors = new[]
        {
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod,
            Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute,
            Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer
        };

        var ids = descriptors.Select(d => d.Id).ToList();
        var uniqueIds = ids.Distinct().ToList();

        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveCommandGeneratorCategory()
    {
        var descriptors = new[]
        {
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod,
            Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute,
            Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer
        };

        Assert.All(descriptors, d => Assert.Equal("CommandGenerator", d.Category));
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveNonEmptyTitle()
    {
        var descriptors = new[]
        {
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod,
            Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute,
            Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer
        };

        Assert.All(descriptors, d => Assert.False(string.IsNullOrEmpty(d.Title.ToString())));
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveNonEmptyMessageFormat()
    {
        var descriptors = new[]
        {
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandQueueMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMethodMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustBePartial,
            Tomato.CommandGenerator.DiagnosticDescriptors.TypeArgumentMustBeCommandQueue,
            Tomato.CommandGenerator.DiagnosticDescriptors.CommandMustImplementMethod,
            Tomato.CommandGenerator.DiagnosticDescriptors.DuplicateCommandAttribute,
            Tomato.CommandGenerator.DiagnosticDescriptors.UnableToAnalyzeFieldInitializer
        };

        Assert.All(descriptors, d => Assert.False(string.IsNullOrEmpty(d.MessageFormat.ToString())));
    }

    #endregion

    #region Helper Methods

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source)
    {
        var (_, diagnostics) = RunGenerator(source);
        return diagnostics;
    }

    private static (Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add the attributes assembly
        references.Add(MetadataReference.CreateFromFile(typeof(CommandQueueAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Tomato.CommandGenerator.CommandGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        return (outputCompilation, diagnostics);
    }

    #endregion
}

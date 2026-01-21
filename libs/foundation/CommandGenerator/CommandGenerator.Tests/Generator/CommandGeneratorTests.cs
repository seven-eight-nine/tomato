using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Generator;

/// <summary>
/// CommandGenerator source generator tests - t-wada style with 3x coverage
/// </summary>
public class CommandGeneratorTests
{
    #region Generator Initialization Tests

    [Fact]
    public void Generator_ShouldBeCreatable()
    {
        var generator = new Tomato.CommandGenerator.CommandGenerator();

        Assert.NotNull(generator);
    }

    [Fact]
    public void Generator_ShouldHaveGeneratorAttribute()
    {
        var type = typeof(Tomato.CommandGenerator.CommandGenerator);
        var attr = type.GetCustomAttributes(typeof(GeneratorAttribute), false);

        Assert.Single(attr);
    }

    [Fact]
    public void Generator_ShouldImplementIIncrementalGenerator()
    {
        var generator = new Tomato.CommandGenerator.CommandGenerator();

        Assert.IsAssignableFrom<IIncrementalGenerator>(generator);
    }

    #endregion

    #region Queue Generation Tests

    [Fact]
    public void Generate_CommandQueue_ShouldProduceNoErrors()
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
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_CommandQueue_ShouldGenerateCode()
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
}";

        var (compilation, _) = RunGenerator(source);

        // Should have more trees than input (generated code added)
        Assert.True(compilation.SyntaxTrees.Count() > 1);
    }

    [Fact]
    public void Generate_CommandQueue_GeneratedCode_ShouldContainInterface()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("ITestQueue", generatedCode);
    }

    #endregion

    #region CommandMethod Tests

    [Fact]
    public void Generate_CommandMethod_WithParameters_ShouldWork()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute(float deltaTime);
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("deltaTime", generatedCode);
    }

    [Fact]
    public void Generate_CommandMethod_Multiple_ShouldWork()
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

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_CommandQueue_ShouldContainEnqueue()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("Enqueue", generatedCode);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Generate_Queue_ShouldContainLocking()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("lock", generatedCode);
    }

    [Fact]
    public void Generate_Queue_ShouldContainExecuteMethod()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("Execute", generatedCode);
    }

    #endregion

    #region Namespace Tests

    [Fact]
    public void Generate_Queue_WithNamespace_ShouldPreserveNamespace()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace MyGame.Commands
{
    [CommandQueue]
    public partial class GameQueue
    {
        [CommandMethod]
        public partial void Execute();
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("MyGame.Commands", generatedCode);
    }

    [Fact]
    public void Generate_Queue_WithGlobalNamespace_ShouldWork()
    {
        var source = @"
using Tomato.CommandGenerator;

[CommandQueue]
public partial class GlobalQueue
{
    [CommandMethod]
    public partial void Execute();
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_NoAttributes_ShouldNotGenerateAnything()
    {
        var source = @"
namespace TestNamespace
{
    public class RegularClass
    {
        public void DoSomething() { }
    }
}";

        var (compilation, _) = RunGenerator(source);

        // Only the original source should be present
        Assert.Equal(1, compilation.SyntaxTrees.Count());
    }

    [Fact]
    public void Generate_EmptyQueue_ShouldNotFail()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class EmptyQueue
    {
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        // May produce warnings but should not fail
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_MultipleQueues_ShouldWork()
    {
        var source = @"
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class QueueA
    {
        [CommandMethod]
        public partial void Execute();
    }

    [CommandQueue]
    public partial class QueueB
    {
        [CommandMethod]
        public partial void Execute();
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("IQueueA", generatedCode);
        Assert.Contains("IQueueB", generatedCode);
    }

    #endregion

    #region Instance Methods Tests

    [Fact]
    public void Generate_Queue_ShouldContainCount()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("Count", generatedCode);
    }

    [Fact]
    public void Generate_Queue_ShouldContainClear()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("Clear", generatedCode);
    }

    #endregion

    #region Double Buffering Tests

    [Fact]
    public void Generate_Queue_ShouldContainCurrentQueue()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("_currentQueue", generatedCode);
    }

    [Fact]
    public void Generate_Queue_ShouldContainPendingQueue()
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
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("_pendingQueue", generatedCode);
    }

    #endregion

    #region Helper Methods

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

    private static string GetAllGeneratedCode(Compilation compilation)
    {
        // Skip the first tree (original source) and get all generated trees
        var generatedTrees = compilation.SyntaxTrees.Skip(1).ToList();

        return string.Join(Environment.NewLine, generatedTrees.Select(t => t.ToString()));
    }

    #endregion
}

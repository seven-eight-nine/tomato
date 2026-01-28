using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tomato.EntityHandleSystem;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Generator;

/// <summary>
/// Tests for {GroupName}Container generation in EntityGenerator.
/// </summary>
public class EntityGeneratorContainerTests
{
    #region Container Generation Tests

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldGenerateContainer()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public sealed class EnemyEntityContainer", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveArenaSegmentStruct()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("private struct ArenaSegment", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveAddMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public void Add(EnemyEntityAnyHandle handle)", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveRemoveMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public bool Remove(EnemyEntityAnyHandle handle)", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveClearMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public void Clear()", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveCountProperty()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public int Count => _totalCount;", generatedCode);
    }

    #endregion

    #region Enumerator Tests

    [Fact]
    public void Generate_Container_ShouldHaveEnumerator()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public struct Enumerator", generatedCode);
        Assert.Contains("public Enumerator GetEnumerator()", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveIterator()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public struct Iterator", generatedCode);
        Assert.Contains("public Iterator GetIterator(int skip = 0, int offset = 0)", generatedCode);
    }

    #endregion

    #region Query Tests

    [Fact]
    public void Generate_Container_ShouldHaveQueryMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public QueryView<TComponent> Query<TComponent>()", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveQueryMethodForTwoComponents()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public QueryView<TComponent1, TComponent2> Query<TComponent1, TComponent2>()", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveQueryMethodForThreeComponents()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public QueryView<TComponent1, TComponent2, TComponent3> Query<TComponent1, TComponent2, TComponent3>()", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveQueryViews()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public readonly ref struct QueryView<TComponent>", generatedCode);
        Assert.Contains("public readonly ref struct QueryView<TComponent1, TComponent2>", generatedCode);
        Assert.Contains("public readonly ref struct QueryView<TComponent1, TComponent2, TComponent3>", generatedCode);
    }

    [Fact]
    public void Generate_Container_ShouldHaveQueryEnumerators()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public ref struct QueryEnumerator<TComponent>", generatedCode);
        Assert.Contains("public ref struct QueryEnumerator<TComponent1, TComponent2>", generatedCode);
        Assert.Contains("public ref struct QueryEnumerator<TComponent1, TComponent2, TComponent3>", generatedCode);
    }

    #endregion

    #region GroupAnyHandle Internal Arena Property Tests

    [Fact]
    public void Generate_GroupAnyHandle_ShouldHaveInternalArenaProperty()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("internal IEnemyEntityArena Arena => _arena;", generatedCode);
    }

    #endregion

    #region Binary Search Helper Tests

    [Fact]
    public void Generate_Container_ShouldHaveBinarySearchHelpers()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("private static int BinarySearchInsertPosition(ref ArenaSegment segment, int index)", generatedCode);
        Assert.Contains("private static int BinarySearchExact(ref ArenaSegment segment, int index, int generation)", generatedCode);
    }

    #endregion

    #region Compact Tests

    [Fact]
    public void Generate_Container_ShouldHaveCompactMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public void Compact()", generatedCode);
    }

    #endregion

    #region Multiple Entities Same Group Tests

    [Fact]
    public void Generate_MultipleEntitiesSameGroup_ShouldGenerateContainerOnce()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    public partial class Zombie
    {
        public int Health;
    }

    [EnemyEntity]
    public partial class Ghost
    {
        public float Speed;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        // Container should appear once
        int containerCount = CountOccurrences(generatedCode, "public sealed class EnemyEntityContainer");
        Assert.Equal(1, containerCount);
    }

    #endregion

    #region Direct Entity Attribute Tests

    [Fact]
    public void Generate_DirectEntityAttribute_ShouldNotGenerateContainer()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class SimpleEntity
    {
        public int Value;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        // No Container for direct [Entity]
        Assert.DoesNotContain("SimpleEntityContainer", generatedCode);
    }

    #endregion

    #region Compilation Tests

    [Fact]
    public void Generate_Container_ShouldCompileSuccessfully()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public struct HealthComponent
    {
        public int Hp;
    }

    public struct PositionComponent
    {
        public float X;
        public float Y;
    }

    public class EnemyEntityAttribute : EntityAttribute { }

    [EnemyEntity]
    [EntityComponent(typeof(HealthComponent))]
    [EntityComponent(typeof(PositionComponent))]
    public partial class Zombie
    {
        public int Id;
    }

    [EnemyEntity]
    [EntityComponent(typeof(HealthComponent))]
    public partial class Ghost
    {
        public float Opacity;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var compilationDiagnostics = compilation.GetDiagnostics();
        var compilationErrors = compilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(compilationErrors);
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
        references.Add(MetadataReference.CreateFromFile(typeof(EntityAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new EntityGenerator();

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

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}

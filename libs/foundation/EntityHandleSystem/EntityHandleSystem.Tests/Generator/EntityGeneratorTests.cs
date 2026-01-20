using System;
using System.Collections.Immutable;
using System.Linq;
using Tomato.EntityHandleSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Generator;

/// <summary>
/// EntityGenerator source generator tests - t-wada style with 3x coverage
/// </summary>
public class EntityGeneratorTests
{
    #region Generator Initialization Tests

    [Fact]
    public void Generator_ShouldBeCreatable()
    {
        var generator = new EntityGenerator();

        Assert.NotNull(generator);
    }

    [Fact]
    public void Generator_ShouldHaveGeneratorAttribute()
    {
        var type = typeof(EntityGenerator);
        var attr = type.GetCustomAttributes(typeof(GeneratorAttribute), false);

        Assert.Single(attr);
    }

    [Fact]
    public void Generator_ShouldImplementISourceGenerator()
    {
        var generator = new EntityGenerator();

        Assert.IsAssignableFrom<ISourceGenerator>(generator);
    }

    #endregion

    #region Basic Generation Tests

    [Fact]
    public void Generate_SimpleEntity_ShouldProduceNoErrors()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class SimpleEntity
    {
        public int Value { get; set; }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_SimpleEntity_ShouldGenerateCode()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class SimpleEntity
    {
        public int Value { get; set; }
    }
}";

        var (compilation, _) = RunGenerator(source);

        // Should have more trees than input (generated code added)
        Assert.True(compilation.SyntaxTrees.Count() > 1);
    }

    [Fact]
    public void Generate_SimpleEntity_ShouldContainHandleStruct()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class Player
    {
        public int Value { get; set; }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("PlayerHandle", generatedCode);
    }

    #endregion

    #region Handle Generation Tests

    [Fact]
    public void Generate_Entity_ShouldCreateHandleWithEquality()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("IEquatable<TestEntityHandle>", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldCreateHandleWithIsValid()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public bool IsValid", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldCreateHandleWithDispose()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public void Dispose()", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldCreateHandleWithInvalidStatic()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public static TestEntityHandle Invalid", generatedCode);
    }

    #endregion

    #region Arena Generation Tests

    [Fact]
    public void Generate_Entity_ShouldCreateArenaClass()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public class TestEntityArena", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldInheritEntityArenaBase()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("EntityArenaBase<TestEntity, TestEntityHandle>", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldHaveCreateMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public TestEntityHandle Create()", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldHaveCountProperty()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public int Count", generatedCode);
    }

    [Fact]
    public void Generate_Entity_ShouldHaveCapacityProperty()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public int Capacity", generatedCode);
    }

    #endregion

    #region InitialCapacity Attribute Tests

    [Fact]
    public void Generate_WithInitialCapacity_ShouldUseSpecifiedCapacity()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity(InitialCapacity = 512)]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("512", generatedCode);
    }

    [Fact]
    public void Generate_WithDefaultCapacity_ShouldUse256()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("256", generatedCode);
    }

    #endregion

    #region Custom ArenaName Tests

    [Fact]
    public void Generate_WithCustomArenaName_ShouldUseSpecifiedName()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity(ArenaName = ""CustomPool"")]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public class CustomPool", generatedCode);
    }

    [Fact]
    public void Generate_WithCustomArenaName_HandleShouldReferenceArena()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity(ArenaName = ""MyArena"")]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("internal readonly MyArena _arena", generatedCode);
    }

    #endregion

    #region EntityMethod Tests

    [Fact]
    public void Generate_WithEntityMethod_ShouldGenerateTryMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Health;

        [EntityMethod]
        public void TakeDamage(int amount)
        {
            Health -= amount;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryTakeDamage", generatedCode);
    }

    [Fact]
    public void Generate_WithEntityMethodUnsafe_ShouldGenerateUnsafeMethod()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Health;

        [EntityMethod(Unsafe = true)]
        public void TakeDamage(int amount)
        {
            Health -= amount;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TakeDamage_Unsafe", generatedCode);
    }

    [Fact]
    public void Generate_WithEntityMethodReturnValue_ShouldHaveOutParameter()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Health;

        [EntityMethod]
        public int GetHealth()
        {
            return Health;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("out int result", generatedCode);
    }

    #endregion

    #region Namespace Tests

    [Fact]
    public void Generate_WithNamespace_ShouldPreserveNamespace()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace MyGame.Entities
{
    [Entity]
    public partial class Player
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("namespace MyGame.Entities", generatedCode);
    }

    [Fact]
    public void Generate_WithGlobalNamespace_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

[Entity]
public partial class GlobalEntity
{
    public int Value;
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_WithNestedNamespace_ShouldPreserveNamespace()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace Game.World.Entities
{
    [Entity]
    public partial class NPC
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("namespace Game.World.Entities", generatedCode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_NoEntities_ShouldNotGenerateAnything()
    {
        var source = @"
namespace TestNamespace
{
    public class RegularClass
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        // Only the original source should be present
        Assert.Equal(1, compilation.SyntaxTrees.Count());
    }

    [Fact]
    public void Generate_MultipleEntities_ShouldGenerateAll()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class Player
    {
        public int Health;
    }

    [Entity]
    public partial class Enemy
    {
        public int Health;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);
        Assert.Contains("PlayerHandle", generatedCode);
        Assert.Contains("EnemyHandle", generatedCode);
        Assert.Contains("PlayerArena", generatedCode);
        Assert.Contains("EnemyArena", generatedCode);
    }

    [Fact]
    public void Generate_EmptyEntity_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class EmptyEntity
    {
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Equality Operators Tests

    [Fact]
    public void Generate_Handle_ShouldHaveEqualityOperator()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("operator ==(TestEntityHandle left, TestEntityHandle right)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldHaveInequalityOperator()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("operator !=(TestEntityHandle left, TestEntityHandle right)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldHaveGetHashCode()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public override int GetHashCode()", generatedCode);
    }

    #endregion

    #region Auto-Generated Header Tests

    [Fact]
    public void Generate_ShouldHaveAutoGeneratedHeader()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("<auto-generated", generatedCode);
    }

    [Fact]
    public void Generate_ShouldHaveUsingSystem()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("using System;", generatedCode);
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

    #endregion
}

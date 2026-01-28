using System;
using System.Collections.Immutable;
using System.Linq;
using Tomato.EntityHandleSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Generator;

/// <summary>
/// Tests for Entity Group (derived attribute) support in EntityGenerator.
/// </summary>
public class EntityGeneratorGroupTests
{
    #region Derived Attribute Detection Tests

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldProduceNoErrors()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldGenerateHandleAndArena()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("IngamePlayerHandle", generatedCode);
        Assert.Contains("IngamePlayerArena", generatedCode);
    }

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldStillHaveToAnyHandle()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public Tomato.EntityHandleSystem.AnyHandle ToAnyHandle()", generatedCode);
    }

    #endregion

    #region Group Interface Generation Tests

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldGenerateGroupInterface()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public interface IPlayerEntityArena : Tomato.EntityHandleSystem.IEntityArena", generatedCode);
    }

    [Fact]
    public void Generate_WithDerivedAttribute_ArenaImplementsGroupInterface()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("IPlayerEntityArena", generatedCode);
        // Arena should implement both IEntityArena and IPlayerEntityArena
        Assert.Contains("class IngamePlayerArena", generatedCode);
    }

    #endregion

    #region Group AnyHandle Generation Tests

    [Fact]
    public void Generate_WithDerivedAttribute_ShouldGenerateGroupAnyHandle()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public readonly struct PlayerEntityAnyHandle", generatedCode);
    }

    [Fact]
    public void Generate_WithDerivedAttribute_GroupAnyHandleHasIsValid()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // PlayerEntityAnyHandle should have IsValid property
        Assert.Contains("public bool IsValid =>", generatedCode);
    }

    [Fact]
    public void Generate_WithDerivedAttribute_GroupAnyHandleHasTryExecute()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public bool TryExecute<TComponent>", generatedCode);
    }

    [Fact]
    public void Generate_WithDerivedAttribute_GroupAnyHandleCanConvertToGlobalAnyHandle()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // PlayerEntityAnyHandle.ToAnyHandle() should exist
        Assert.Contains("public Tomato.EntityHandleSystem.AnyHandle ToAnyHandle()", generatedCode);
    }

    #endregion

    #region Handle ToGroupAnyHandle Generation Tests

    [Fact]
    public void Generate_WithDerivedAttribute_HandleHasToGroupAnyHandle()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public PlayerEntityAnyHandle ToPlayerEntityAnyHandle()", generatedCode);
    }

    #endregion

    #region Multiple Entities Same Group Tests

    [Fact]
    public void Generate_MultipleEntitiesSameGroup_ShouldGenerateGroupOnce()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }

    [PlayerEntity]
    public partial class OutgamePlayer
    {
        public string Name;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        // Both entities should have their own Handle/Arena
        Assert.Contains("IngamePlayerHandle", generatedCode);
        Assert.Contains("IngamePlayerArena", generatedCode);
        Assert.Contains("OutgamePlayerHandle", generatedCode);
        Assert.Contains("OutgamePlayerArena", generatedCode);

        // Group types should be generated once
        Assert.Contains("interface IPlayerEntityArena", generatedCode);
        Assert.Contains("struct PlayerEntityAnyHandle", generatedCode);

        // Both handles should have ToPlayerEntityAnyHandle
        // Count occurrences of ToPlayerEntityAnyHandle - should appear twice (once in each handle)
        int count = CountOccurrences(generatedCode, "public PlayerEntityAnyHandle ToPlayerEntityAnyHandle()");
        Assert.Equal(2, count);
    }

    [Fact]
    public void Generate_MultipleEntitiesSameGroup_AllArenasImplementGroupInterface()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }

    [PlayerEntity]
    public partial class OutgamePlayer
    {
        public string Name;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // Both arenas should implement IPlayerEntityArena
        Assert.Contains("class IngamePlayerArena", generatedCode);
        Assert.Contains("class OutgamePlayerArena", generatedCode);
        // The interface list should include IPlayerEntityArena for both
        int count = CountOccurrences(generatedCode, "IPlayerEntityArena");
        Assert.True(count >= 3, $"Expected at least 3 occurrences of IPlayerEntityArena (2 arenas + 1 interface + AnyHandle), got {count}");
    }

    #endregion

    #region Multiple Groups Tests

    [Fact]
    public void Generate_MultipleGroups_ShouldGenerateAllGroupTypes()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }
    public class ProjectileEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }

    [ProjectileEntity]
    public partial class Arrow
    {
        public float Speed;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        // PlayerEntity group types
        Assert.Contains("interface IPlayerEntityArena", generatedCode);
        Assert.Contains("struct PlayerEntityAnyHandle", generatedCode);

        // ProjectileEntity group types
        Assert.Contains("interface IProjectileEntityArena", generatedCode);
        Assert.Contains("struct ProjectileEntityAnyHandle", generatedCode);

        // Each entity has its own ToGroupAnyHandle
        Assert.Contains("ToPlayerEntityAnyHandle()", generatedCode);
        Assert.Contains("ToProjectileEntityAnyHandle()", generatedCode);
    }

    #endregion

    #region Direct Entity Attribute Tests

    [Fact]
    public void Generate_DirectEntityAttribute_NoGroupTypesGenerated()
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

        // Standard Handle and Arena should be generated
        Assert.Contains("SimpleEntityHandle", generatedCode);
        Assert.Contains("SimpleEntityArena", generatedCode);

        // No group-specific types should be generated
        Assert.DoesNotContain("ISimpleEntityArena", generatedCode);
        Assert.DoesNotContain("SimpleEntityAnyHandle", generatedCode);

        // ToAnyHandle should exist
        Assert.Contains("ToAnyHandle()", generatedCode);
    }

    [Fact]
    public void Generate_MixedAttributes_BothWorkCorrectly()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [Entity]
    public partial class SimpleEntity
    {
        public int Value;
    }

    [PlayerEntity]
    public partial class PlayerEntity
    {
        public int Health;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        // Simple entity - no group
        Assert.Contains("SimpleEntityHandle", generatedCode);
        Assert.Contains("SimpleEntityArena", generatedCode);

        // Player entity - has group
        Assert.Contains("PlayerEntityHandle", generatedCode);
        Assert.Contains("PlayerEntityArena", generatedCode);
        Assert.Contains("interface IPlayerEntityArena", generatedCode);
        Assert.Contains("struct PlayerEntityAnyHandle", generatedCode);
        Assert.Contains("ToPlayerEntityAnyHandle()", generatedCode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_DerivedAttributeWithInitialCapacity_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity(InitialCapacity = 512)]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("512", generatedCode);
    }

    [Fact]
    public void Generate_DerivedAttributeWithCustomArenaName_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity(ArenaName = ""CustomPlayerPool"")]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public class CustomPlayerPool", generatedCode);
        // Custom arena should still implement the group interface
        Assert.Contains("IPlayerEntityArena", generatedCode);
    }

    [Fact]
    public void Generate_GlobalNamespace_WithDerivedAttribute_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

public class PlayerEntityAttribute : EntityAttribute { }

[PlayerEntity]
public partial class GlobalPlayer
{
    public int Health;
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Equality Tests for GroupAnyHandle

    [Fact]
    public void Generate_GroupAnyHandle_HasEqualityOperators()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("operator ==(PlayerEntityAnyHandle left, PlayerEntityAnyHandle right)", generatedCode);
        Assert.Contains("operator !=(PlayerEntityAnyHandle left, PlayerEntityAnyHandle right)", generatedCode);
    }

    [Fact]
    public void Generate_GroupAnyHandle_ImplementsIEquatable()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class PlayerEntityAttribute : EntityAttribute { }

    [PlayerEntity]
    public partial class IngamePlayer
    {
        public int Health;
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("IEquatable<PlayerEntityAnyHandle>", generatedCode);
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

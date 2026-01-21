using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Tomato.EntityHandleSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Runtime;

/// <summary>
/// Handle validation tests - t-wada style thorough coverage
/// Tests handle behavior with null arena, generation 0, Handle.Invalid, and equality/GetHashCode consistency.
/// </summary>
public class HandleValidationTests
{
    #region IsValid with Null Arena Tests

    [Fact]
    public void Generate_Handle_IsValid_WithNullArena_ShouldReturnFalse()
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

        // The generated IsValid should check for null arena
        Assert.Contains("_arena != null", generatedCode);
    }

    [Fact]
    public void Generate_DefaultHandle_ShouldHaveNullArena()
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

        // default(Handle) should have null arena, 0 index, 0 generation
        Assert.Contains("default(TestEntityHandle)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_Dispose_WithNullArena_ShouldNotThrow()
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

        // Dispose should check for null arena before calling destroy
        Assert.Contains("if (_arena != null)", generatedCode);
    }

    #endregion

    #region IsValid with Generation 0 Tests

    [Fact]
    public void Arena_InitialGeneration_ShouldBeOne()
    {
        var arena = new TestArena(16);

        var (_, generation) = arena.Allocate();

        // Generation starts at 1, not 0
        Assert.Equal(1, generation);
    }

    [Fact]
    public void Arena_Generation0_ShouldBeInvalid()
    {
        var arena = new TestArena(16);
        arena.Allocate();

        // Generation 0 should never be valid (reserved for invalid handles)
        var result = arena.IsValid(0, 0);

        Assert.False(result);
    }

    [Fact]
    public void Arena_AllSlots_StartWithGeneration1()
    {
        var arena = new TestArena(4);

        for (int i = 0; i < 4; i++)
        {
            var (_, generation) = arena.Allocate();
            Assert.Equal(1, generation);
        }
    }

    [Fact]
    public void Arena_AfterOverflow_ShouldSkipGeneration0()
    {
        var arena = new TestArenaWithGenerationAccess(16);

        var (index, _) = arena.Allocate();

        // Set generation close to overflow
        arena.SetGeneration(index, int.MaxValue);
        arena.Deallocate(index, int.MaxValue);

        var (_, newGen) = arena.Allocate();

        // Should wrap to 1, never 0
        Assert.Equal(1, newGen);
        Assert.NotEqual(0, newGen);
    }

    #endregion

    #region Handle.Invalid Behavior Tests

    [Fact]
    public void Generate_Handle_InvalidProperty_ShouldReturnDefault()
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
        Assert.Contains("default(TestEntityHandle)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_Invalid_IsValid_ShouldBeFalse()
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

        // Invalid handle has null arena, so IsValid returns false
        Assert.Contains("_arena != null && _arena.IsValid(this)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_Invalid_Dispose_ShouldNotThrow()
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

        // Dispose checks for null arena
        Assert.Contains("if (_arena != null)", generatedCode);
        Assert.Contains("_arena.DestroyInternal", generatedCode);
    }

    [Fact]
    public void Generate_Handle_TryMethods_WithInvalidHandle_ShouldReturnFalse()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;

        [EntityMethod]
        public int GetValue()
        {
            return Value;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // Try methods should check validity via ref and return false if invalid
        Assert.Contains("ref var entity = ref _arena.TryGetRefInternal(_index, _generation, out var valid)", generatedCode);
        Assert.Contains("if (!valid)", generatedCode);
        Assert.Contains("return false", generatedCode);
    }

    #endregion

    #region Handle Equality and GetHashCode Consistency Tests

    [Fact]
    public void Generate_Handle_EqualsAndHashCode_ShouldBeConsistent()
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

        // Both Equals and GetHashCode should use the same fields
        Assert.Contains("_arena == other._arena", generatedCode);
        Assert.Contains("_generation == other._generation", generatedCode);
        Assert.Contains("_index == other._index", generatedCode);

        // GetHashCode should combine all fields
        Assert.Contains("hash * 31 + (_arena != null ? _arena.GetHashCode() : 0)", generatedCode);
        Assert.Contains("hash * 31 + _generation", generatedCode);
        Assert.Contains("hash * 31 + _index", generatedCode);
    }

    [Fact]
    public void Generate_Handle_SameFields_ShouldBeEqual()
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

        // Equals uses && for all fields
        Assert.Contains("_arena == other._arena", generatedCode);
        Assert.Contains("&& _generation == other._generation", generatedCode);
        Assert.Contains("&& _index == other._index", generatedCode);
    }

    [Fact]
    public void Generate_Handle_OperatorEquals_ShouldUseEquals()
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

        Assert.Contains("return left.Equals(right)", generatedCode);
        Assert.Contains("return !left.Equals(right)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ObjectEquals_ShouldCheckType()
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

        Assert.Contains("obj is TestEntityHandle", generatedCode);
    }

    [Fact]
    public void Generate_Handle_GetHashCode_ShouldUseUnchecked()
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

        // Should use unchecked to prevent overflow exceptions
        Assert.Contains("unchecked", generatedCode);
    }

    [Fact]
    public void Generate_Handle_Default_EqualsSelf()
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

        // Default handles should equal each other
        // Both have null arena, 0 generation, 0 index
        Assert.Contains("_arena == other._arena", generatedCode);
    }

    [Fact]
    public void Generate_Handle_NullArena_GetHashCode_ShouldNotThrow()
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

        // Should handle null arena in GetHashCode
        Assert.Contains("_arena != null ? _arena.GetHashCode() : 0", generatedCode);
    }

    #endregion

    #region Handle Struct Behavior Tests

    [Fact]
    public void Generate_Handle_IsStruct()
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

        Assert.Contains("public struct TestEntityHandle", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ImplementsIEquatable()
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

    #endregion

    #region Arena IsValid Method Tests

    [Fact]
    public void Arena_IsValid_WithCorrectIndexAndGeneration_ShouldReturnTrue()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.IsValid(index, generation);

        Assert.True(result);
    }

    [Fact]
    public void Arena_IsValid_WithWrongGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        var result = arena.IsValid(index, generation + 1);

        Assert.False(result);
    }

    [Fact]
    public void Arena_IsValid_WithNegativeIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(-1, 1);

        Assert.False(result);
    }

    [Fact]
    public void Arena_IsValid_WithOutOfBoundsIndex_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var result = arena.IsValid(100, 1);

        Assert.False(result);
    }

    [Fact]
    public void Arena_IsValid_AfterDeallocation_ShouldReturnFalse()
    {
        var arena = new TestArena(16);
        var (index, generation) = arena.Allocate();

        arena.Deallocate(index, generation);

        var result = arena.IsValid(index, generation);

        Assert.False(result);
    }

    [Fact]
    public void Arena_IsValid_AfterReallocation_OldGeneration_ShouldReturnFalse()
    {
        var arena = new TestArena(16);

        var (index1, gen1) = arena.Allocate();
        arena.Deallocate(index1, gen1);

        var (index2, gen2) = arena.Allocate();

        // Same index, old generation
        Assert.Equal(index1, index2);
        Assert.False(arena.IsValid(index2, gen1));
        Assert.True(arena.IsValid(index2, gen2));
    }

    #endregion

    #region Helper Classes

    private class TestEntity
    {
        public int Value { get; set; }
    }

    private class TestArena : EntityArenaBase<TestEntity, object>
    {
        public TestArena(int initialCapacity)
            : base(initialCapacity, null, null)
        {
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public int Capacity
        {
            get
            {
                lock (_lock)
                {
                    return _entities.Length;
                }
            }
        }

        public (int Index, int Generation) Allocate()
        {
            lock (_lock)
            {
                var index = AllocateInternal(out var generation);
                return (index, generation);
            }
        }

        public bool Deallocate(int index, int generation)
        {
            lock (_lock)
            {
                return DeallocateInternal(index, generation);
            }
        }

        public bool TryGet(int index, int generation, out TestEntity? entity)
        {
            lock (_lock)
            {
                ref var e = ref TryGetRefInternal(index, generation, out var valid);
                entity = valid ? e : null;
                return valid;
            }
        }

        public bool IsValid(int index, int generation)
        {
            lock (_lock)
            {
                return IsValidInternal(index, generation);
            }
        }
    }

    private class TestArenaWithGenerationAccess : TestArena
    {
        public TestArenaWithGenerationAccess(int initialCapacity)
            : base(initialCapacity)
        {
        }

        public void SetGeneration(int index, int generation)
        {
            lock (_lock)
            {
                _generations[index] = generation;
            }
        }

        public int GetGeneration(int index)
        {
            lock (_lock)
            {
                return _generations[index];
            }
        }
    }

    #endregion

    #region Generator Helper Methods

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

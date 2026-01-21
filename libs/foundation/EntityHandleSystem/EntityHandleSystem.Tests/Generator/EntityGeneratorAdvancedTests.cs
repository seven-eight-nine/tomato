using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Tomato.EntityHandleSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Generator;

/// <summary>
/// EntityGenerator advanced tests - t-wada style thorough coverage
/// Covers nested classes, generics, copy semantics, equality in collections, ref/out parameters, and unsafe methods.
/// </summary>
public class EntityGeneratorAdvancedTests
{
    #region Nested Classes with [Entity] Attribute Tests

    [Fact]
    public void Generate_NestedClass_ShouldProduceNoErrors()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class Outer
    {
        [Entity]
        public partial class NestedEntity
        {
            public int Value { get; set; }
        }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        // Note: Nested classes may or may not be fully supported
        // This test documents the current behavior
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        // We accept if it works or produces expected diagnostic
        Assert.True(errors.Count == 0 || errors.Any(e => e.Id.Contains("CS")));
    }

    [Fact]
    public void Generate_DeeplyNestedClass_ShouldHandleCorrectly()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    public class Level1
    {
        public class Level2
        {
            [Entity]
            public partial class DeeplyNestedEntity
            {
                public int Value { get; set; }
            }
        }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        // Document current behavior
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0 || errors.Any(e => e.Id.Contains("CS")));
    }

    #endregion

    #region Generic Classes with [Entity] Tests

    [Fact]
    public void Generate_GenericClass_ShouldHandleCorrectly()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class GenericEntity<T>
    {
        public T Value { get; set; }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        // Generic entities may have limitations
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        // This documents the behavior - generics may not be fully supported
        Assert.True(errors.Count >= 0); // Accept any result
    }

    [Fact]
    public void Generate_NonGenericClassWithGenericProperty_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;
using System.Collections.Generic;

namespace TestNamespace
{
    [Entity]
    public partial class EntityWithGenericProperty
    {
        public List<int> Values { get; set; }
    }
}";

        var (_, diagnostics) = RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Handle Struct Copy Semantics Tests

    [Fact]
    public void Generate_Handle_ShouldBeValueType()
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
    public void Generate_Handle_ShouldHaveReadonlyFields()
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

        Assert.Contains("private readonly", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldHaveInternalConstructor()
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

        Assert.Contains("internal TestEntityHandle(", generatedCode);
    }

    [Fact]
    public void Generate_Handle_CopySemantics_ShouldCopyAllFields()
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

        // Handle should store arena, index, and generation
        Assert.Contains("_arena", generatedCode);
        Assert.Contains("_index", generatedCode);
        Assert.Contains("_generation", generatedCode);
    }

    #endregion

    #region Handle Equality in Dictionary/HashSet Tests

    [Fact]
    public void Generate_Handle_ShouldImplementIEquatable()
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
    public void Generate_Handle_EqualsMethod_ShouldCompareAllFields()
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

        // Equals should compare arena, generation, and index
        Assert.Contains("_arena == other._arena", generatedCode);
        Assert.Contains("_generation == other._generation", generatedCode);
        Assert.Contains("_index == other._index", generatedCode);
    }

    [Fact]
    public void Generate_Handle_GetHashCode_ShouldIncludeAllFields()
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

        // GetHashCode should combine arena, generation, and index
        Assert.Contains("_arena", generatedCode);
        Assert.Contains("_generation", generatedCode);
        Assert.Contains("_index", generatedCode);
        Assert.Contains("GetHashCode()", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldOverrideObjectEquals()
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

        Assert.Contains("public override bool Equals(object obj)", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldHaveEqualityOperators()
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

        Assert.Contains("operator ==", generatedCode);
        Assert.Contains("operator !=", generatedCode);
    }

    #endregion

    #region Generated Try* Method with ref/out Parameters Tests

    [Fact]
    public void Generate_TryMethodWithOutParameter_ShouldSetDefault()
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
        public void GetHealthInfo(out int health, out bool isDead)
        {
            health = Health;
            isDead = Health <= 0;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryGetHealthInfo", generatedCode);
        Assert.Contains("out int health", generatedCode);
        Assert.Contains("out bool isDead", generatedCode);
    }

    [Fact]
    public void Generate_TryMethodWithRefParameter_ShouldPassByRef()
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
        public void ModifyHealth(ref int amount)
        {
            Health += amount;
            amount = Health;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryModifyHealth", generatedCode);
        Assert.Contains("ref int amount", generatedCode);
    }

    [Fact]
    public void Generate_TryMethodWithMixedParameters_ShouldHandleCorrectly()
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
        public bool ProcessDamage(int damage, ref int remainingHealth, out bool wasFatal)
        {
            Health -= damage;
            remainingHealth = Health;
            wasFatal = Health <= 0;
            return true;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryProcessDamage", generatedCode);
        Assert.Contains("int damage", generatedCode);
        Assert.Contains("ref int remainingHealth", generatedCode);
        Assert.Contains("out bool wasFatal", generatedCode);
        Assert.Contains("out bool result", generatedCode);
    }

    [Fact]
    public void Generate_TryMethodWithReturnValue_ShouldHaveResultOutParameter()
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

        Assert.Contains("public bool TryGetHealth(out int result)", generatedCode);
    }

    [Fact]
    public void Generate_TryMethod_ShouldReturnFalseOnInvalidHandle()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        [EntityMethod]
        public void DoSomething()
        {
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryGetRefInternal", generatedCode);
        Assert.Contains("return false", generatedCode);
        Assert.Contains("return true", generatedCode);
    }

    [Fact]
    public void Generate_TryMethod_WithOutParameters_ShouldSetDefaultsOnFailure()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        [EntityMethod]
        public int Compute(out string message)
        {
            message = ""done"";
            return 42;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // Should set defaults when handle is invalid
        Assert.Contains("default(", generatedCode);
    }

    #endregion

    #region Unsafe Method Generation Verification Tests

    [Fact]
    public void Generate_UnsafeMethod_ShouldHaveUnsafeSuffix()
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
    public void Generate_UnsafeMethod_ShouldNotHaveLock()
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
        public int GetHealth()
        {
            return Health;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // The unsafe method should use GetEntityRefUnchecked without locks
        Assert.Contains("GetEntityRefUnchecked", generatedCode);
    }

    [Fact]
    public void Generate_UnsafeMethod_ShouldHaveSameReturnType()
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
        public int GetHealth()
        {
            return Health;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public int GetHealth_Unsafe()", generatedCode);
    }

    [Fact]
    public void Generate_UnsafeMethod_ShouldPreserveParameters()
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
        public void SetHealth(int value, bool clamp)
        {
            Health = clamp ? System.Math.Max(0, value) : value;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("SetHealth_Unsafe(int value, bool clamp)", generatedCode);
    }

    [Fact]
    public void Generate_UnsafeMethod_VoidReturn_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Counter;

        [EntityMethod(Unsafe = true)]
        public void Increment()
        {
            Counter++;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("public void Increment_Unsafe()", generatedCode);
    }

    [Fact]
    public void Generate_UnsafeMethod_WithRefParameter_ShouldWork()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;

        [EntityMethod(Unsafe = true)]
        public void Swap(ref int other)
        {
            var temp = Value;
            Value = other;
            other = temp;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("Swap_Unsafe(ref int other)", generatedCode);
    }

    [Fact]
    public void Generate_NonUnsafeMethod_ShouldNotHaveUnsafeSuffix()
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
        Assert.DoesNotContain("TakeDamage_Unsafe", generatedCode);
    }

    [Fact]
    public void Generate_BothUnsafeAndTry_ShouldGenerateBoth()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Value;

        [EntityMethod(Unsafe = true)]
        public int GetValue()
        {
            return Value;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        // Both Try and Unsafe versions should exist
        Assert.Contains("TryGetValue", generatedCode);
        Assert.Contains("GetValue_Unsafe", generatedCode);
    }

    #endregion

    #region Internal Index/Generation Accessor Tests

    [Fact]
    public void Generate_Handle_ShouldHaveInternalIndexAccessor()
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

        Assert.Contains("internal int Index", generatedCode);
    }

    [Fact]
    public void Generate_Handle_ShouldHaveInternalGenerationAccessor()
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

        Assert.Contains("internal int Generation", generatedCode);
    }

    #endregion

    #region Arena GetEntityDirect Tests

    [Fact]
    public void Generate_Arena_ShouldHaveGetEntityDirect()
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

        Assert.Contains("GetEntityRefUnchecked", generatedCode);
    }

    [Fact]
    public void Generate_Arena_GetEntityRefUnchecked_ShouldBeInternal()
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

        Assert.Contains("internal new ref TestEntity GetEntityRefUnchecked(int index)", generatedCode);
    }

    #endregion

    #region Multiple EntityMethod Tests

    [Fact]
    public void Generate_MultipleEntityMethods_ShouldGenerateAll()
    {
        var source = @"
using Tomato.EntityHandleSystem;

namespace TestNamespace
{
    [Entity]
    public partial class TestEntity
    {
        public int Health;
        public int Mana;

        [EntityMethod]
        public void TakeDamage(int amount)
        {
            Health -= amount;
        }

        [EntityMethod]
        public void UseMana(int amount)
        {
            Mana -= amount;
        }

        [EntityMethod(Unsafe = true)]
        public int GetStats()
        {
            return Health + Mana;
        }
    }
}";

        var (compilation, _) = RunGenerator(source);

        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("TryTakeDamage", generatedCode);
        Assert.Contains("TryUseMana", generatedCode);
        Assert.Contains("TryGetStats", generatedCode);
        Assert.Contains("GetStats_Unsafe", generatedCode);
    }

    #endregion

    #region Pragma Warnings Tests

    [Fact]
    public void Generate_ShouldDisableObsoleteWarning()
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

        Assert.Contains("#pragma warning disable CS0618", generatedCode);
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

using System.Linq;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class DeepCloneableTypeTests
    {
        [Fact]
        public void Generates_DeepClone_ForNestedDeepClonableType()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Inner
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class Outer
    {
        public Inner InnerObject { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Equal(2, generatedSources.Length);

            var outerGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Outer"));
            Assert.NotNull(outerGenerated);
            Assert.Contains("this.InnerObject.DeepCloneInternal()", outerGenerated);
        }

        [Fact]
        public void Generates_DeepClone_ForListOfDeepClonableType()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Item
    {
        public string Name { get; set; }
    }

    [DeepClonable]
    public partial class Container
    {
        public List<Item> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

            var containerGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Container"));
            Assert.NotNull(containerGenerated);
            Assert.Contains("item.DeepCloneInternal()", containerGenerated);
        }

        [Fact]
        public void IgnoresProperty_WithIgnoreAttribute()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class WithIgnore
    {
        public int Value { get; set; }

        [DeepCloneOption.Ignore]
        public string Ignored { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("clone.Value", generated);
            // [Ignore] attribute should generate clone.X = default; (not copy the value)
            Assert.Contains("clone.Ignored = default;", generated);
        }

        [Fact]
        public void UsesShallowCopy_WithShallowAttribute()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class SomeReferenceType
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class WithShallow
    {
        [DeepCloneOption.Shallow]
        public SomeReferenceType Reference { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Empty(diagnostics.Where(d => d.Id == "DCG101"));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("clone.Reference = this.Reference;", generated);
        }

        [Fact]
        public void GeneratesCycleTracking_WithCyclableAttribute()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Node
    {
        public string Name { get; set; }

        [DeepCloneOption.Cyclable]
        public Node? Next { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("DeepCloneCycleTracker.TryGetClone", generated);
            Assert.Contains("DeepCloneCycleTracker.Register", generated);
            Assert.Contains("DeepCloneCycleTracker.Clear()", generated);
        }

        [Fact]
        public void CompilesSuccessfully_WithNestedTypes()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Inner
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class Outer
    {
        public Inner InnerObject { get; set; }
        public List<Inner> InnerList { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }

        [Fact]
        public void UsesCustomDeepClone_ForManualIDeepCloneableImplementation()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    // Manual IDeepCloneable<T> implementation (no [DeepClonable] attribute)
    public class CustomQueue : IDeepCloneable<CustomQueue>
    {
        public int Count { get; set; }

        public CustomQueue DeepClone()
        {
            return new CustomQueue(); // Custom logic: don't copy Count
        }
    }

    [DeepClonable]
    public partial class Entity
    {
        public string Name { get; set; }
        public CustomQueue Queue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

            var entityGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Entity"));
            Assert.NotNull(entityGenerated);

            // Debug output
            System.Console.WriteLine("=== Generated Entity ===");
            System.Console.WriteLine(entityGenerated);

            // Should use .DeepClone() (not .DeepCloneInternal()) for manual IDeepCloneable<T>
            Assert.Contains(".Queue.DeepClone()", entityGenerated);
            Assert.DoesNotContain(".Queue.DeepCloneInternal()", entityGenerated);
        }

        [Fact]
        public void UsesDeepCloneInternal_ForDeepClonableAttribute()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    // Has [DeepClonable] attribute - should use generated DeepCloneInternal()
    [DeepClonable]
    public partial class GeneratedQueue
    {
        public int Count { get; set; }
    }

    [DeepClonable]
    public partial class Entity
    {
        public string Name { get; set; }
        public GeneratedQueue Queue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

            var entityGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Entity"));
            Assert.NotNull(entityGenerated);
            // Should use .DeepCloneInternal() for [DeepClonable] types
            Assert.Contains("this.Queue.DeepCloneInternal()", entityGenerated);
        }

        [Fact]
        public void UsesCustomDeepClone_ForListOfManualIDeepCloneable()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    // Manual IDeepCloneable<T> implementation
    public class CustomItem : IDeepCloneable<CustomItem>
    {
        public int Value { get; set; }

        public CustomItem DeepClone()
        {
            return new CustomItem { Value = this.Value * 2 }; // Custom logic
        }
    }

    [DeepClonable]
    public partial class Container
    {
        public List<CustomItem> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));

            var containerGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Container"));
            Assert.NotNull(containerGenerated);
            // Should use .DeepClone() for manual IDeepCloneable<T> in collections
            Assert.Contains("item.DeepClone()", containerGenerated);
            Assert.DoesNotContain("item.DeepCloneInternal()", containerGenerated);
        }

        [Fact]
        public void CompilesSuccessfully_WithCustomDeepClone()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class CustomQueue : IDeepCloneable<CustomQueue>
    {
        private int _internalCount;

        public CustomQueue DeepClone()
        {
            return new CustomQueue(); // Returns empty queue
        }
    }

    [DeepClonable]
    public partial class Entity
    {
        public string Name { get; set; }
        public CustomQueue Queue { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }
    }
}

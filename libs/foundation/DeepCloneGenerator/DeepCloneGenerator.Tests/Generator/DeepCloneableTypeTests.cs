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
    }
}

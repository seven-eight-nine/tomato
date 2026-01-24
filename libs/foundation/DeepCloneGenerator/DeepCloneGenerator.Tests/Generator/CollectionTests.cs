using System.Linq;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class CollectionTests
    {
        [Fact]
        public void Generates_DeepClone_ForArrayProperty()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ArrayClass
    {
        public int[] Numbers { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("new int[this.Numbers.Length]", generated);
            Assert.Contains("System.Array.Copy", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForListProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ListClass
    {
        public List<string> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("List<string>", generated);
            Assert.Contains("AddRange", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForDictionaryProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class DictionaryClass
    {
        public Dictionary<string, int> Lookup { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("Dictionary<string, int>", generated);
            Assert.Contains("kvp.Key", generated);
            Assert.Contains("kvp.Value", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForHashSetProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class HashSetClass
    {
        public HashSet<int> UniqueValues { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("HashSet<int>", generated);
        }

        [Fact]
        public void CompilesSuccessfully_WithCollections()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class CollectionCompileTest
    {
        public int[] ArrayProp { get; set; }
        public List<string> ListProp { get; set; }
        public Dictionary<int, string> DictProp { get; set; }
        public HashSet<double> SetProp { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }

        [Fact]
        public void Generates_DeepClone_HandlesNullCollections()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class NullCollectionClass
    {
        public List<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            // Should have null check
            Assert.Contains("if (this.Items != null)", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForNestedCollections()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class NestedCollectionClass
    {
        public List<List<int>> NestedList { get; set; }
        public Dictionary<string, List<int>> DictWithList { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);
        }

        [Fact]
        public void CompilesSuccessfully_WithNullableCollections()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class NullableCollectionClass
    {
        public List<int>? NullableList { get; set; }
        public int[]? NullableArray { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }

        [Fact]
        public void Generates_DeepClone_ForArrayOfDeepClonableType()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Item
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class Container
    {
        public Item[] Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Equal(2, generatedSources.Length);

            var containerGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Container"));
            Assert.NotNull(containerGenerated);
            Assert.Contains("DeepCloneInternal()", containerGenerated);
        }

        [Fact]
        public void Generates_DeepClone_ForDictionaryWithDeepClonableValue()
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
        public Dictionary<string, Item> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Equal(2, generatedSources.Length);

            var containerGenerated = generatedSources.FirstOrDefault(s => s.Contains("partial class Container"));
            Assert.NotNull(containerGenerated);
            Assert.Contains("kvp.Value.DeepCloneInternal()", containerGenerated);
        }
    }
}

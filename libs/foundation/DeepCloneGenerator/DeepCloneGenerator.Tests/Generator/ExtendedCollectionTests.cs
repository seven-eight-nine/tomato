using System.Linq;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class ExtendedCollectionTests
    {
        [Fact]
        public void Generates_DeepClone_ForQueueProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class QueueClass
    {
        public Queue<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("Queue<int>", generated);
            Assert.Contains("Enqueue", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForStackProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class StackClass
    {
        public Stack<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("Stack<int>", generated);
            Assert.Contains("Push", generated);
            Assert.Contains("ToArray", generated);
            Assert.Contains("Reverse", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForLinkedListProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class LinkedListClass
    {
        public LinkedList<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("LinkedList<int>", generated);
            Assert.Contains("AddLast", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForSortedListProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class SortedListClass
    {
        public SortedList<string, int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("SortedList<", generated);
            Assert.Contains("Comparer", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForSortedDictionaryProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class SortedDictionaryClass
    {
        public SortedDictionary<string, int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("SortedDictionary<", generated);
            Assert.Contains("Comparer", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForSortedSetProperty()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class SortedSetClass
    {
        public SortedSet<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("SortedSet<", generated);
            Assert.Contains("Comparer", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForConcurrentDictionaryProperty()
        {
            var source = @"
using System.Collections.Concurrent;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ConcurrentDictClass
    {
        public ConcurrentDictionary<string, int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ConcurrentDictionary<", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForConcurrentQueueProperty()
        {
            var source = @"
using System.Collections.Concurrent;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ConcurrentQueueClass
    {
        public ConcurrentQueue<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ConcurrentQueue<", generated);
            Assert.Contains("Enqueue", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForConcurrentStackProperty()
        {
            var source = @"
using System.Collections.Concurrent;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ConcurrentStackClass
    {
        public ConcurrentStack<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ConcurrentStack<", generated);
            Assert.Contains("Push", generated);
            Assert.Contains("ToArray", generated);
            Assert.Contains("Reverse", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForConcurrentBagProperty()
        {
            var source = @"
using System.Collections.Concurrent;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ConcurrentBagClass
    {
        public ConcurrentBag<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ConcurrentBag<", generated);
            Assert.Contains(".Add(", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForObservableCollectionProperty()
        {
            var source = @"
using System.Collections.ObjectModel;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ObservableClass
    {
        public ObservableCollection<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ObservableCollection<", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForReadOnlyCollectionProperty()
        {
            var source = @"
using System.Collections.ObjectModel;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ReadOnlyClass
    {
        public ReadOnlyCollection<int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ReadOnlyCollection<", generated);
            Assert.Contains("tempList", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForJaggedArrayProperty()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class JaggedArrayClass
    {
        public int[][] Matrix { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("new int[this.Matrix.Length][]", generated);
            Assert.Contains("for (int i = 0;", generated);
            Assert.Contains("Array.Copy", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForMultiDimensionalArray2()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Matrix2DClass
    {
        public int[,] Matrix { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("GetLength(0)", generated);
            Assert.Contains("GetLength(1)", generated);
            Assert.Contains("[dim0, dim1]", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForMultiDimensionalArray3()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class Matrix3DClass
    {
        public int[,,] Cube { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("GetLength(0)", generated);
            Assert.Contains("GetLength(1)", generated);
            Assert.Contains("GetLength(2)", generated);
            Assert.Contains("[dim0, dim1, dim2]", generated);
        }

        [Fact]
        public void PreservesComparer_ForDictionary()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class DictionaryComparerClass
    {
        public Dictionary<string, int> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("this.Items.Comparer", generated);
        }

        [Fact]
        public void PreservesComparer_ForHashSet()
        {
            var source = @"
using System.Collections.Generic;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class HashSetComparerClass
    {
        public HashSet<string> Items { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("this.Items.Comparer", generated);
        }

        [Fact]
        public void CompilesSuccessfully_WithExtendedCollections()
        {
            var source = @"
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ExtendedCollectionCompileTest
    {
        public Queue<int> QueueProp { get; set; }
        public Stack<int> StackProp { get; set; }
        public LinkedList<int> LinkedListProp { get; set; }
        public SortedList<string, int> SortedListProp { get; set; }
        public SortedDictionary<string, int> SortedDictProp { get; set; }
        public SortedSet<int> SortedSetProp { get; set; }
        public ConcurrentDictionary<string, int> ConcurrentDictProp { get; set; }
        public ConcurrentQueue<int> ConcurrentQueueProp { get; set; }
        public ConcurrentStack<int> ConcurrentStackProp { get; set; }
        public ConcurrentBag<int> ConcurrentBagProp { get; set; }
        public ObservableCollection<int> ObservableProp { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }

        [Fact]
        public void CompilesSuccessfully_WithArrayTypes()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ArrayTypesCompileTest
    {
        public int[][] JaggedArray { get; set; }
        public int[,] Matrix2D { get; set; }
        public int[,,] Matrix3D { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }
    }
}

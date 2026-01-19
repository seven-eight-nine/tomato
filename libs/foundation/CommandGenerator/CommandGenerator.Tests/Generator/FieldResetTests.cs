using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Generator;

/// <summary>
/// Field reset generation tests - t-wada style comprehensive coverage
/// Tests for nullable field reset, array field reset (multidimensional, jagged),
/// and field with initializer preservation
/// </summary>
public class FieldResetTests
{
    #region Nullable Field Reset Tests

    [Fact]
    public void NullableReferenceField_ShouldResetToDefaultBang()
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

    [Command<TestQueue>]
    public partial class NullableRefCommand
    {
        private string? _name;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        // Nullable reference type should reset to default!
        Assert.Contains("_name = default!", generatedCode);
    }

    [Fact]
    public void NullableValueField_ShouldResetToDefault()
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

    [Command<TestQueue>]
    public partial class NullableValueCommand
    {
        private int? _nullableInt;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        // Nullable value type should reset to default
        Assert.Contains("_nullableInt = default", generatedCode);
    }

    [Fact]
    public void NullableObjectField_ShouldResetToDefaultBang()
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

    [Command<TestQueue>]
    public partial class NullableObjectCommand
    {
        private object? _data;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_data = default!", generatedCode);
    }

    [Fact]
    public void MultipleNullableFields_ShouldAllReset()
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

    [Command<TestQueue>]
    public partial class MultiNullableCommand
    {
        private string? _name;
        private int? _age;
        private double? _score;
        private object? _data;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_name = default!", generatedCode);
        Assert.Contains("_age = default", generatedCode);
        Assert.Contains("_score = default", generatedCode);
        Assert.Contains("_data = default!", generatedCode);
    }

    #endregion

    #region Array Field Reset Tests - Simple Arrays

    [Fact]
    public void SimpleArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class SimpleArrayCommand
    {
        private int[] _numbers = new int[10];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_numbers, 0, _numbers.Length)", generatedCode);
    }

    [Fact]
    public void StringArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class StringArrayCommand
    {
        private string[] _items = new string[5];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_items, 0, _items.Length)", generatedCode);
    }

    #endregion

    #region Multidimensional Array Reset Tests

    [Fact]
    public void TwoDimensionalArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class TwoDArrayCommand
    {
        private int[,] _matrix = new int[3, 3];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_matrix, 0, _matrix.Length)", generatedCode);
    }

    [Fact]
    public void ThreeDimensionalArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class ThreeDArrayCommand
    {
        private double[,,] _cube = new double[2, 2, 2];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_cube, 0, _cube.Length)", generatedCode);
    }

    #endregion

    #region Jagged Array Reset Tests

    [Fact]
    public void JaggedArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class JaggedArrayCommand
    {
        private int[][] _jagged = new int[3][];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_jagged, 0, _jagged.Length)", generatedCode);
    }

    [Fact]
    public void DeepJaggedArray_ShouldUseArrayClear()
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

    [Command<TestQueue>]
    public partial class DeepJaggedCommand
    {
        private string[][][] _deepJagged = new string[2][][];

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("System.Array.Clear(_deepJagged, 0, _deepJagged.Length)", generatedCode);
    }

    #endregion

    #region Field with Initializer Preservation Tests

    [Fact]
    public void FieldWithIntInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class IntInitializerCommand
    {
        private int _count = 42;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_count = 42", generatedCode);
    }

    [Fact]
    public void FieldWithStringInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class StringInitializerCommand
    {
        private string _name = ""Default"";

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_name = \"Default\"", generatedCode);
    }

    [Fact]
    public void FieldWithBoolInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class BoolInitializerCommand
    {
        private bool _isEnabled = true;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_isEnabled = true", generatedCode);
    }

    [Fact]
    public void FieldWithExpressionInitializer_ShouldPreserveExpression()
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

    [Command<TestQueue>]
    public partial class ExpressionInitializerCommand
    {
        private int _value = 10 * 5;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_value = 10 * 5", generatedCode);
    }

    [Fact]
    public void FieldWithNegativeInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class NegativeInitializerCommand
    {
        private int _offset = -100;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_offset = -100", generatedCode);
    }

    [Fact]
    public void FieldWithFloatInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class FloatInitializerCommand
    {
        private float _scale = 1.5f;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_scale = 1.5f", generatedCode);
    }

    [Fact]
    public void FieldWithDoubleInitializer_ShouldPreserveValue()
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

    [Command<TestQueue>]
    public partial class DoubleInitializerCommand
    {
        private double _precision = 0.001;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_precision = 0.001", generatedCode);
    }

    #endregion

    #region Static, Const, and Readonly Fields Should Be Excluded

    [Fact]
    public void StaticField_ShouldNotBeReset()
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

    [Command<TestQueue>]
    public partial class StaticFieldCommand
    {
        private static int _instanceCount = 0;
        private int _value = 10;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        // Static field should NOT be in reset code
        Assert.DoesNotContain("_instanceCount", generatedCode);
        // Instance field should be reset
        Assert.Contains("_value = 10", generatedCode);
    }

    [Fact]
    public void ConstField_ShouldNotBeReset()
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

    [Command<TestQueue>]
    public partial class ConstFieldCommand
    {
        private const int MaxValue = 100;
        private int _current = 50;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        // Const field should NOT be in reset code
        Assert.DoesNotContain("MaxValue", generatedCode);
        // Instance field should be reset
        Assert.Contains("_current = 50", generatedCode);
    }

    [Fact]
    public void ReadonlyField_ShouldNotBeReset()
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

    [Command<TestQueue>]
    public partial class ReadonlyFieldCommand
    {
        private readonly int _id = 1;
        private int _mutableValue = 0;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        // Readonly field should NOT be in reset code (look for assignment, not declaration)
        // The reset code would be "_id = ..." which should not exist
        // We need to be careful - "_id" might appear in comments or other places
        // Check that there's no assignment to _id in the ResetToDefault method
        var resetMethodContent = ExtractResetMethodContent(generatedCode);
        Assert.DoesNotContain("_id =", resetMethodContent);
        // Instance field should be reset
        Assert.Contains("_mutableValue = ", resetMethodContent);
    }

    #endregion

    #region Mixed Field Types Tests

    [Fact]
    public void MixedFieldTypes_ShouldGenerateCorrectReset()
    {
        var source = @"
using System.Collections.Generic;
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute();
    }

    [Command<TestQueue>]
    public partial class MixedFieldsCommand
    {
        private int _intField = 5;
        private string? _nullableString;
        private List<int> _list = new();
        private int[] _array = new int[10];
        private bool _flag = true;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_intField = 5", generatedCode);
        Assert.Contains("_nullableString = default!", generatedCode);
        Assert.Contains("_list.Clear()", generatedCode);
        Assert.Contains("System.Array.Clear(_array", generatedCode);
        Assert.Contains("_flag = true", generatedCode);
    }

    #endregion

    #region Value Type Default Reset Tests

    [Fact]
    public void ValueTypeWithoutInitializer_ShouldResetToDefault()
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

    [Command<TestQueue>]
    public partial class ValueTypeCommand
    {
        private int _count;
        private double _value;
        private bool _flag;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_count = default", generatedCode);
        Assert.Contains("_value = default", generatedCode);
        Assert.Contains("_flag = default", generatedCode);
    }

    [Fact]
    public void StructField_ShouldResetToDefault()
    {
        var source = @"
using System;
using Tomato.CommandGenerator;

namespace TestNamespace
{
    [CommandQueue]
    public partial class TestQueue
    {
        [CommandMethod]
        public partial void Execute();
    }

    [Command<TestQueue>]
    public partial class StructFieldCommand
    {
        private DateTime _timestamp;
        private Guid _id;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_timestamp = default", generatedCode);
        Assert.Contains("_id = default", generatedCode);
    }

    #endregion

    #region Reference Type Default Reset Tests

    [Fact]
    public void ReferenceTypeWithoutInitializer_ShouldResetToDefaultBang()
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

    [Command<TestQueue>]
    public partial class RefTypeCommand
    {
        private string _name;
        private object _data;

        public void Execute() { }
    }
}";

        var (compilation, _) = RunGenerator(source);
        var generatedCode = GetAllGeneratedCode(compilation);

        Assert.Contains("_name = default!", generatedCode);
        Assert.Contains("_data = default!", generatedCode);
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

    private static string ExtractResetMethodContent(string generatedCode)
    {
        // Extract the content between "public void ResetToDefault()" and the closing brace
        var startMarker = "public void ResetToDefault()";
        var startIndex = generatedCode.IndexOf(startMarker);
        if (startIndex < 0) return "";

        var braceStart = generatedCode.IndexOf('{', startIndex);
        if (braceStart < 0) return "";

        int braceCount = 1;
        int endIndex = braceStart + 1;

        while (endIndex < generatedCode.Length && braceCount > 0)
        {
            if (generatedCode[endIndex] == '{') braceCount++;
            else if (generatedCode[endIndex] == '}') braceCount--;
            endIndex++;
        }

        return generatedCode.Substring(braceStart, endIndex - braceStart);
    }

    #endregion
}

using System.Linq;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class BasicTypeTests
    {
        [Fact]
        public void Generates_DeepClone_ForSimpleClass()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class SimpleClass
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; }
        public double DoubleValue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("public SimpleClass DeepClone()", generated);
            Assert.Contains("internal SimpleClass DeepCloneInternal()", generated);
            Assert.Contains("clone.IntValue = this.IntValue", generated);
            Assert.Contains("clone.StringValue = this.StringValue", generated);
            Assert.Contains("clone.DoubleValue = this.DoubleValue", generated);
        }

        [Fact]
        public void Generates_DeepClone_WithAllPrimitiveTypes()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class PrimitiveClass
    {
        public bool BoolValue { get; set; }
        public byte ByteValue { get; set; }
        public sbyte SByteValue { get; set; }
        public short ShortValue { get; set; }
        public ushort UShortValue { get; set; }
        public int IntValue { get; set; }
        public uint UIntValue { get; set; }
        public long LongValue { get; set; }
        public ulong ULongValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
        public decimal DecimalValue { get; set; }
        public char CharValue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);
        }

        [Fact]
        public void Generates_DeepClone_ForStruct()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial struct SimpleStruct
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("partial struct SimpleStruct", generated);
        }

        [Fact]
        public void Generates_DeepClone_ForInternalClass()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    internal partial class InternalClass
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("internal partial class InternalClass", generated);
        }

        [Fact]
        public void CompilesSuccessfully_WithGeneratedCode()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class CompileTestClass
    {
        public int Value { get; set; }
        public string Name { get; set; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }
    }
}

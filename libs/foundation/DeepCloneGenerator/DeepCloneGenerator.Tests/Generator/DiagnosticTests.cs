using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class DiagnosticTests
    {
        [Fact]
        public void ReportsError_WhenTypeIsNotPartial()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public class NonPartialClass
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, _) = GeneratorTestHelper.RunGenerator(source);

            var error = diagnostics.FirstOrDefault(d => d.Id == "DCG001");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("NonPartialClass", error.GetMessage());
            Assert.Contains("partial", error.GetMessage());
        }

        [Fact]
        public void ReportsError_WhenNoParameterlessConstructor()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class NoDefaultCtorClass
    {
        public int Value { get; set; }

        public NoDefaultCtorClass(int value)
        {
            Value = value;
        }
    }
}";

            var (diagnostics, _) = GeneratorTestHelper.RunGenerator(source);

            var error = diagnostics.FirstOrDefault(d => d.Id == "DCG002");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("NoDefaultCtorClass", error.GetMessage());
        }

        [Fact]
        public void ReportsWarning_ForShallowCopyMember()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class UnclonableClass
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class HasUnclonableMember
    {
        public UnclonableClass Other { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var warning = diagnostics.FirstOrDefault(d => d.Id == "DCG101");
            Assert.NotNull(warning);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("Other", warning.GetMessage());

            Assert.Single(generatedSources);
            Assert.Contains("shallow copy", generatedSources[0]);
        }

        [Fact]
        public void ReportsWarning_ForReadonlyField()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class HasReadonlyField
    {
        public readonly int ReadonlyValue;
        public int NormalValue;
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var warning = diagnostics.FirstOrDefault(d => d.Id == "DCG102");
            Assert.NotNull(warning);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("ReadonlyValue", warning.GetMessage());

            Assert.Single(generatedSources);
            var generated = generatedSources[0];
            Assert.DoesNotContain("clone.ReadonlyValue", generated);
            Assert.Contains("clone.NormalValue", generated);
        }

        [Fact]
        public void DoesNotReportError_WhenClassHasImplicitParameterlessConstructor()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ImplicitCtorClass
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Id == "DCG002"));
            Assert.Single(generatedSources);
        }

        [Fact]
        public void ReportsError_WhenTypeIsPrivate()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class Outer
    {
        [DeepClonable]
        private partial class PrivateClass
        {
            public int Value { get; set; }
        }
    }
}";

            var (diagnostics, _) = GeneratorTestHelper.RunGenerator(source);

            var error = diagnostics.FirstOrDefault(d => d.Id == "DCG003");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("PrivateClass", error.GetMessage());
        }

        [Fact]
        public void NoWarning_WhenShallowAttributeUsed()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class UnclonableClass
    {
        public int Value { get; set; }
    }

    [DeepClonable]
    public partial class HasShallowMember
    {
        [DeepCloneOption.Shallow]
        public UnclonableClass Other { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            // No DCG101 warning because [Shallow] attribute is explicitly used
            Assert.Empty(diagnostics.Where(d => d.Id == "DCG101"));
            Assert.Single(generatedSources);
        }
    }
}

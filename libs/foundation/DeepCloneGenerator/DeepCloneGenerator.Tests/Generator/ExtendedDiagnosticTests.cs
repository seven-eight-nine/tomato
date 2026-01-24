using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Generator
{
    public class ExtendedDiagnosticTests
    {
        [Fact]
        public void Reports_DCG004_ForAbstractClass()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public abstract partial class AbstractClass
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, _) = GeneratorTestHelper.RunGenerator(source);

            var errorDiagnostics = diagnostics.Where(d => d.Id == "DCG004").ToList();
            Assert.Single(errorDiagnostics);
            Assert.Contains("AbstractClass", errorDiagnostics[0].GetMessage());
        }

        [Fact]
        public void Reports_DCG005_ForInitOnlyProperty()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class InitOnlyClass
    {
        public int Value { get; init; }
    }
}";

            var (diagnostics, _) = GeneratorTestHelper.RunGenerator(source);

            var errorDiagnostics = diagnostics.Where(d => d.Id == "DCG005").ToList();
            Assert.Single(errorDiagnostics);
            Assert.Contains("Value", errorDiagnostics[0].GetMessage());
        }

        [Fact]
        public void NoError_ForInitOnlyProperty_WithIgnore()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class InitOnlyIgnoreClass
    {
        [DeepCloneOption.Ignore]
        public int Value { get; init; }
        public string Name { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errorDiagnostics);
            Assert.Single(generatedSources);
        }

        [Fact]
        public void Reports_DCG103_ForDelegateProperty()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class DelegateClass
    {
        public Action OnAction { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var warningDiagnostics = diagnostics.Where(d => d.Id == "DCG103").ToList();
            Assert.Single(warningDiagnostics);

            // Should still generate code (just with shallow copy)
            Assert.Single(generatedSources);
            var generated = generatedSources[0];
            Assert.Contains("shallow copy", generated);
        }

        [Fact]
        public void Reports_DCG103_ForFuncProperty()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class FuncClass
    {
        public Func<int, string> Converter { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var warningDiagnostics = diagnostics.Where(d => d.Id == "DCG103").ToList();
            Assert.Single(warningDiagnostics);

            Assert.Single(generatedSources);
        }

        [Fact]
        public void NoError_ForRecordWithSetterProperties()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial record RecordWithSetter
    {
        public int Value { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errorDiagnostics);
            Assert.Single(generatedSources);
        }

        [Fact]
        public void Generates_DeepClone_ForInheritedMembers()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class BaseClass
    {
        public int BaseValue { get; set; }
    }

    [DeepClonable]
    public partial class DerivedClass : BaseClass
    {
        public int DerivedValue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            // Should clone both base and derived members
            Assert.Contains("BaseValue", generated);
            Assert.Contains("DerivedValue", generated);
        }

        [Fact]
        public void Handles_ProtectedMembers_FromBaseClass()
        {
            var source = @"
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class BaseClass
    {
        protected int ProtectedValue { get; set; }
    }

    [DeepClonable]
    public partial class DerivedClass : BaseClass
    {
        public int PublicValue { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("ProtectedValue", generated);
            Assert.Contains("PublicValue", generated);
        }

        [Fact]
        public void Handles_KnownImmutableTypes()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class ImmutableTypesClass
    {
        public DateTime DateTime { get; set; }
        public Guid Guid { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
        public Uri Uri { get; set; }
        public Version Version { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            // Should be direct copy for immutable types
            Assert.Contains("clone.DateTime = this.DateTime", generated);
            Assert.Contains("clone.Guid = this.Guid", generated);
            Assert.Contains("clone.TimeSpan = this.TimeSpan", generated);
        }

        [Fact]
        public void Handles_TupleTypes_AsValueCopy()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class TupleClass
    {
        public (int, string) ValueTuple { get; set; }
        public Tuple<int, string> RefTuple { get; set; }
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Single(generatedSources);

            var generated = generatedSources[0];
            Assert.Contains("clone.ValueTuple = this.ValueTuple", generated);
            Assert.Contains("clone.RefTuple = this.RefTuple", generated);
        }

        [Fact]
        public void Reports_DCG104_ForEventProperty()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    [DeepClonable]
    public partial class EventClass
    {
        public event EventHandler MyEvent;
    }
}";

            var (diagnostics, generatedSources) = GeneratorTestHelper.RunGenerator(source);

            var warningDiagnostics = diagnostics.Where(d => d.Id == "DCG104").ToList();
            Assert.Single(warningDiagnostics);
            Assert.Contains("MyEvent", warningDiagnostics[0].GetMessage());

            // Should still generate code
            Assert.Single(generatedSources);
        }

        [Fact]
        public void CompilesSuccessfully_WithExtendedFeatures()
        {
            var source = @"
using System;
using Tomato.DeepCloneGenerator;

namespace TestNamespace
{
    public class BaseEntity
    {
        public int Id { get; set; }
        protected DateTime CreatedAt { get; set; }
    }

    [DeepClonable]
    public partial class Person : BaseEntity
    {
        public string Name { get; set; }
        public Guid ExternalId { get; set; }

        [DeepCloneOption.Ignore]
        public int IgnoredValue { get; init; }
    }
}";

            var (compDiags, _) = GeneratorTestHelper.GetCompiledResult(source);

            Assert.Empty(compDiags);
        }
    }
}

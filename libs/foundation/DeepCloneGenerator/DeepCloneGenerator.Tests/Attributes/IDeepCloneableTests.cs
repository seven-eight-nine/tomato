using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Attributes
{
    public class IDeepCloneableTests
    {
        [Fact]
        public void IDeepCloneable_HasClassConstraint()
        {
            var interfaceType = typeof(IDeepCloneable<>);
            var typeParams = interfaceType.GetGenericArguments();

            Assert.Single(typeParams);

            var constraints = typeParams[0].GetGenericParameterConstraints();
            var attributes = typeParams[0].GenericParameterAttributes;

            // Check for reference type constraint (class)
            Assert.True(attributes.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint));
        }

        [Fact]
        public void IDeepCloneable_HasDeepCloneMethod()
        {
            var interfaceType = typeof(IDeepCloneable<object>);
            var method = interfaceType.GetMethod("DeepClone");

            Assert.NotNull(method);
            Assert.Equal(typeof(object), method.ReturnType);
            Assert.Empty(method.GetParameters());
        }
    }
}

using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Attributes
{
    public class DeepClonableAttributeTests
    {
        [Fact]
        public void DeepClonableAttribute_CanBeAppliedToClass()
        {
            var attr = new DeepClonableAttribute();
            Assert.NotNull(attr);
        }

        [Fact]
        public void DeepClonableAttribute_HasCorrectUsage()
        {
            var usageAttr = (System.AttributeUsageAttribute?)System.Attribute.GetCustomAttribute(
                typeof(DeepClonableAttribute), typeof(System.AttributeUsageAttribute));

            Assert.NotNull(usageAttr);
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Class));
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Struct));
            Assert.False(usageAttr.AllowMultiple);
            Assert.False(usageAttr.Inherited);
        }
    }
}

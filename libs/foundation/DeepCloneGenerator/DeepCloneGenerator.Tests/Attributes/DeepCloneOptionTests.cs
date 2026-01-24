using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Attributes
{
    public class DeepCloneOptionTests
    {
        [Fact]
        public void IgnoreAttribute_CanBeCreated()
        {
            var attr = new DeepCloneOption.IgnoreAttribute();
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShallowAttribute_CanBeCreated()
        {
            var attr = new DeepCloneOption.ShallowAttribute();
            Assert.NotNull(attr);
        }

        [Fact]
        public void CyclableAttribute_CanBeCreated()
        {
            var attr = new DeepCloneOption.CyclableAttribute();
            Assert.NotNull(attr);
        }

        [Fact]
        public void IgnoreAttribute_HasCorrectUsage()
        {
            var usageAttr = (System.AttributeUsageAttribute?)System.Attribute.GetCustomAttribute(
                typeof(DeepCloneOption.IgnoreAttribute), typeof(System.AttributeUsageAttribute));

            Assert.NotNull(usageAttr);
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Field));
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Property));
        }

        [Fact]
        public void ShallowAttribute_HasCorrectUsage()
        {
            var usageAttr = (System.AttributeUsageAttribute?)System.Attribute.GetCustomAttribute(
                typeof(DeepCloneOption.ShallowAttribute), typeof(System.AttributeUsageAttribute));

            Assert.NotNull(usageAttr);
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Field));
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Property));
        }

        [Fact]
        public void CyclableAttribute_HasCorrectUsage()
        {
            var usageAttr = (System.AttributeUsageAttribute?)System.Attribute.GetCustomAttribute(
                typeof(DeepCloneOption.CyclableAttribute), typeof(System.AttributeUsageAttribute));

            Assert.NotNull(usageAttr);
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Field));
            Assert.True(usageAttr.ValidOn.HasFlag(System.AttributeTargets.Property));
        }
    }
}

using System.Collections.Generic;
using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Integration
{
    public partial class CircularReferenceTests
    {
        [DeepClonable]
        public partial class Person
        {
            public string Name { get; set; } = "";
            public List<string> Hobbies { get; set; } = new List<string>();

            [DeepCloneOption.Cyclable]
            public Person? Spouse { get; set; }
        }

        [Fact]
        public void DeepClone_HandlesCircularReference()
        {
            var alice = new Person { Name = "Alice" };
            var bob = new Person { Name = "Bob" };
            alice.Spouse = bob;
            bob.Spouse = alice;

            var clonedAlice = alice.DeepClone();

            Assert.NotSame(alice, clonedAlice);
            Assert.Equal("Alice", clonedAlice.Name);

            Assert.NotNull(clonedAlice.Spouse);
            Assert.NotSame(bob, clonedAlice.Spouse);
            Assert.Equal("Bob", clonedAlice.Spouse.Name);

            Assert.Same(clonedAlice, clonedAlice.Spouse.Spouse);
        }

        [Fact]
        public void DeepClone_HandlesSelfReference()
        {
            var person = new Person { Name = "Self" };
            person.Spouse = person;

            var cloned = person.DeepClone();

            Assert.NotSame(person, cloned);
            Assert.Equal("Self", cloned.Name);
            Assert.Same(cloned, cloned.Spouse);
        }

        [Fact]
        public void DeepClone_DeepClonesListContents()
        {
            var person = new Person
            {
                Name = "Test",
                Hobbies = new List<string> { "Reading", "Gaming" }
            };

            var cloned = person.DeepClone();

            Assert.NotSame(person.Hobbies, cloned.Hobbies);
            Assert.Equal(person.Hobbies.Count, cloned.Hobbies.Count);
            Assert.Equal("Reading", cloned.Hobbies[0]);
            Assert.Equal("Gaming", cloned.Hobbies[1]);

            cloned.Hobbies.Add("Swimming");
            Assert.Equal(2, person.Hobbies.Count);
            Assert.Equal(3, cloned.Hobbies.Count);
        }

        [DeepClonable]
        public partial class TreeNode
        {
            public int Value { get; set; }

            [DeepCloneOption.Cyclable]
            public TreeNode? Parent { get; set; }

            public List<TreeNode>? Children { get; set; }
        }

        [Fact]
        public void DeepClone_HandlesTreeStructure()
        {
            var root = new TreeNode
            {
                Value = 1,
                Children = new List<TreeNode>()
            };

            var child1 = new TreeNode { Value = 2, Parent = root };
            var child2 = new TreeNode { Value = 3, Parent = root };
            root.Children.Add(child1);
            root.Children.Add(child2);

            var clonedRoot = root.DeepClone();

            Assert.NotSame(root, clonedRoot);
            Assert.Equal(1, clonedRoot.Value);
            Assert.Null(clonedRoot.Parent);

            Assert.NotNull(clonedRoot.Children);
            Assert.Equal(2, clonedRoot.Children.Count);

            Assert.NotSame(child1, clonedRoot.Children[0]);
            Assert.Equal(2, clonedRoot.Children[0].Value);

            Assert.NotSame(child2, clonedRoot.Children[1]);
            Assert.Equal(3, clonedRoot.Children[1].Value);
        }

        [DeepClonable]
        public partial class NodeWithCyclableList
        {
            public int Id { get; set; }

            [DeepCloneOption.Cyclable]
            public List<NodeWithCyclableList>? Related { get; set; }
        }

        [Fact]
        public void DeepClone_HandlesCircularCollection()
        {
            // A -> List<A> -> A (circular reference through collection)
            var nodeA = new NodeWithCyclableList { Id = 1, Related = new List<NodeWithCyclableList>() };
            var nodeB = new NodeWithCyclableList { Id = 2, Related = new List<NodeWithCyclableList>() };

            nodeA.Related.Add(nodeB);
            nodeB.Related.Add(nodeA);

            var clonedA = nodeA.DeepClone();

            Assert.NotSame(nodeA, clonedA);
            Assert.Equal(1, clonedA.Id);
            Assert.NotNull(clonedA.Related);
            Assert.Single(clonedA.Related);

            var clonedB = clonedA.Related[0];
            Assert.NotSame(nodeB, clonedB);
            Assert.Equal(2, clonedB.Id);

            Assert.NotNull(clonedB.Related);
            Assert.Single(clonedB.Related);
            Assert.Same(clonedA, clonedB.Related[0]);
        }

        [Fact]
        public void DeepClone_HandlesEmptyCollections()
        {
            var person = new Person
            {
                Name = "Test",
                Hobbies = new List<string>()
            };

            var cloned = person.DeepClone();

            Assert.NotSame(person.Hobbies, cloned.Hobbies);
            Assert.Empty(cloned.Hobbies);
        }
    }
}

using Tomato.EntityHandleSystem;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Integration
{
    // Test component - Position
    public struct PositionComponent
    {
        public float X;
        public float Y;
        public float Z;

        [EntityMethod]
        public void SetPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [EntityMethod]
        public float GetDistanceSquared()
        {
            return X * X + Y * Y + Z * Z;
        }
    }

    // Test component - Velocity
    public struct VelocityComponent
    {
        public float VX;
        public float VY;
        public float VZ;

        [EntityMethod]
        public void SetVelocity(float vx, float vy, float vz)
        {
            VX = vx;
            VY = vy;
            VZ = vz;
        }

        [EntityMethod]
        public void ApplyToPosition(ref PositionComponent pos, float deltaTime)
        {
            pos.X += VX * deltaTime;
            pos.Y += VY * deltaTime;
            pos.Z += VZ * deltaTime;
        }
    }

    // Test entity with components
    [Entity]
    [EntityComponent(typeof(PositionComponent))]
    [EntityComponent(typeof(VelocityComponent))]
    public partial class MovableEntity
    {
        public int EntityId;

        [EntityMethod]
        public void SetId(int id)
        {
            EntityId = id;
        }
    }

    // Test entity with single component
    [Entity]
    [EntityComponent(typeof(PositionComponent))]
    public partial class StaticEntity
    {
        public string Name;
    }

    public class ComponentSystemTests
    {
        [Fact]
        public void HandleCanAccessComponentMethod()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();

            // Set position via component method on handle
            bool result = handle.PositionComponent_TrySetPosition(1.0f, 2.0f, 3.0f);

            Assert.True(result);
        }

        [Fact]
        public void HandleCanGetComponentMethodReturnValue()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();

            handle.PositionComponent_TrySetPosition(3.0f, 4.0f, 0.0f);
            bool result = handle.PositionComponent_TryGetDistanceSquared(out float distance);

            Assert.True(result);
            Assert.Equal(25.0f, distance); // 3^2 + 4^2 = 25
        }

        [Fact]
        public void ComponentMethodWithCrossComponentRef_AutoFetchesComponent()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();

            // Set initial position and velocity
            handle.PositionComponent_TrySetPosition(0.0f, 0.0f, 0.0f);
            handle.VelocityComponent_TrySetVelocity(10.0f, 20.0f, 30.0f);

            // Apply velocity to position (cross-component reference)
            bool result = handle.VelocityComponent_TryApplyToPosition(1.0f); // deltaTime = 1.0

            Assert.True(result);

            // Verify position was updated
            handle.PositionComponent_TryGetDistanceSquared(out float distance);
            // Position should be (10, 20, 30), distance squared = 100 + 400 + 900 = 1400
            Assert.Equal(1400.0f, distance);
        }

        [Fact]
        public void HandleComponentMethodReturnsFalse_WhenEntityDestroyed()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();
            handle.Dispose();

            bool result = handle.PositionComponent_TrySetPosition(1.0f, 2.0f, 3.0f);

            Assert.False(result);
        }

        [Fact]
        public void VoidHandle_TryExecute_WorksWithComponent()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();
            handle.PositionComponent_TrySetPosition(5.0f, 5.0f, 5.0f);

            VoidHandle voidHandle = handle.ToVoidHandle();

            float capturedX = 0;
            bool result = voidHandle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
            {
                capturedX = pos.X;
                pos.X = 100.0f;
            });

            Assert.True(result);
            Assert.Equal(5.0f, capturedX);

            // Verify position was modified
            handle.PositionComponent_TryGetDistanceSquared(out float distance);
            // Position is now (100, 5, 5), distance squared = 10000 + 25 + 25 = 10050
            Assert.Equal(10050.0f, distance);
        }

        [Fact]
        public void VoidHandle_TryExecute_ReturnsFalse_WhenEntityDestroyed()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();
            VoidHandle voidHandle = handle.ToVoidHandle();
            handle.Dispose();

            bool result = voidHandle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
            {
                pos.X = 100.0f;
            });

            Assert.False(result);
        }

        [Fact]
        public void VoidHandle_TryExecute_ReturnsFalse_WhenArenaDoesNotHaveComponent()
        {
            var arena = new StaticEntityArena();
            var handle = arena.Create();
            VoidHandle voidHandle = handle.ToVoidHandle();

            // StaticEntity does not have VelocityComponent
            bool result = voidHandle.TryExecute<VelocityComponent>((ref VelocityComponent vel) =>
            {
                vel.VX = 100.0f;
            });

            Assert.False(result);
        }

        [Fact]
        public void TypedHandle_TryExecute_WorksWithComponent()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();
            handle.PositionComponent_TrySetPosition(5.0f, 5.0f, 5.0f);

            float capturedX = 0;
            bool result = handle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
            {
                capturedX = pos.X;
                pos.X = 200.0f;
            });

            Assert.True(result);
            Assert.Equal(5.0f, capturedX);

            // Verify position was modified
            handle.PositionComponent_TryGetDistanceSquared(out float distance);
            // Position is now (200, 5, 5), distance squared = 40000 + 25 + 25 = 40050
            Assert.Equal(40050.0f, distance);
        }

        [Fact]
        public void TypedHandle_TryExecute_ReturnsFalse_WhenEntityDestroyed()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();
            handle.Dispose();

            bool result = handle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
            {
                pos.X = 100.0f;
            });

            Assert.False(result);
        }

        [Fact]
        public void MultipleEntities_HaveIndependentComponentData()
        {
            var arena = new MovableEntityArena();
            var handle1 = arena.Create();
            var handle2 = arena.Create();

            handle1.PositionComponent_TrySetPosition(1.0f, 1.0f, 1.0f);
            handle2.PositionComponent_TrySetPosition(10.0f, 10.0f, 10.0f);

            handle1.PositionComponent_TryGetDistanceSquared(out float distance1);
            handle2.PositionComponent_TryGetDistanceSquared(out float distance2);

            Assert.Equal(3.0f, distance1);  // 1 + 1 + 1 = 3
            Assert.Equal(300.0f, distance2); // 100 + 100 + 100 = 300
        }

        [Fact]
        public void ComponentArrays_ExpandWithEntities()
        {
            var arena = new MovableEntityArena(2); // Small initial capacity
            var handles = new MovableEntityHandle[10];

            // Create more entities than initial capacity
            for (int i = 0; i < 10; i++)
            {
                handles[i] = arena.Create();
                handles[i].PositionComponent_TrySetPosition(i, i, i);
            }

            // Verify all entities have correct position data
            for (int i = 0; i < 10; i++)
            {
                handles[i].PositionComponent_TryGetDistanceSquared(out float distance);
                Assert.Equal(3.0f * i * i, distance);
            }
        }

        [Fact]
        public void EntityCanCallComponentMethod_ViaEntityMethods()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();

            // First set position via handle
            handle.PositionComponent_TrySetPosition(5.0f, 6.0f, 7.0f);

            // Verify via TryExecute
            float x = 0, y = 0, z = 0;
            handle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
            {
                x = pos.X;
                y = pos.Y;
                z = pos.Z;
            });

            Assert.Equal(5.0f, x);
            Assert.Equal(6.0f, y);
            Assert.Equal(7.0f, z);
        }

        [Fact]
        public void EntityAndComponentMethods_CoexistOnHandle()
        {
            var arena = new MovableEntityArena();
            var handle = arena.Create();

            // Call entity method
            bool entityMethodResult = handle.TrySetId(42);
            Assert.True(entityMethodResult);

            // Call component method
            bool componentMethodResult = handle.PositionComponent_TrySetPosition(1.0f, 2.0f, 3.0f);
            Assert.True(componentMethodResult);
        }

        [Fact]
        public void Arena_ImplementsIComponentArena()
        {
            var arena = new MovableEntityArena();

            Assert.True(arena is IComponentArena<PositionComponent>);
            Assert.True(arena is IComponentArena<VelocityComponent>);
        }

        [Fact]
        public void CrossEntityType_VoidHandle_TryExecute()
        {
            var movableArena = new MovableEntityArena();
            var staticArena = new StaticEntityArena();

            var movableHandle = movableArena.Create();
            var staticHandle = staticArena.Create();

            movableHandle.PositionComponent_TrySetPosition(1.0f, 2.0f, 3.0f);
            staticHandle.PositionComponent_TrySetPosition(10.0f, 20.0f, 30.0f);

            VoidHandle[] voidHandles = new[]
            {
                movableHandle.ToVoidHandle(),
                staticHandle.ToVoidHandle()
            };

            // Process all entities with PositionComponent (cross-entity-type)
            float sum = 0;
            foreach (var vh in voidHandles)
            {
                vh.TryExecute<PositionComponent>((ref PositionComponent pos) =>
                {
                    sum += pos.X + pos.Y + pos.Z;
                });
            }

            // movable: 1+2+3=6, static: 10+20+30=60, total: 66
            Assert.Equal(66.0f, sum);
        }
    }
}

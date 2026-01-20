using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// Entity にコンポーネント型を関連付けます。
/// Structure of Arrays (SoA) パターンでコンポーネントが Arena 内に管理されます。
///
/// <para>コンポーネントは Entity とは別の配列として Arena に格納され、
/// 同じインデックスでアクセスできます。これによりキャッシュ効率が向上し、
/// 同種のコンポーネントへの連続アクセスが高速になります。</para>
///
/// <para>コンポーネント型には [EntityMethod] を付けたメソッドを定義できます。
/// これらは Handle から {ComponentName}_Try{MethodName} としてアクセスできます。</para>
///
/// <example>
/// 使用例:
/// <code>
/// // コンポーネント定義
/// public struct PositionComponent
/// {
///     public float X, Y, Z;
///
///     [EntityMethod]
///     public void SetPosition(float x, float y, float z)
///     {
///         X = x; Y = y; Z = z;
///     }
/// }
///
/// // Entity定義
/// [Entity]
/// [EntityComponent(typeof(PositionComponent))]
/// [EntityComponent(typeof(VelocityComponent))]
/// public partial class MovableEntity
/// {
///     public int EntityId;
/// }
///
/// // 使用方法
/// var arena = new MovableEntityArena();
/// var handle = arena.Create();
/// handle.Position_TrySetPosition(1f, 2f, 3f);
///
/// // VoidHandle 経由での横串操作
/// voidHandle.TryExecute&lt;PositionComponent&gt;((ref PositionComponent p) =&gt;
/// {
///     p.Y -= 9.8f * deltaTime; // 重力適用
/// });
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class EntityComponentAttribute : Attribute
{
    /// <summary>
    /// 関連付けるコンポーネントの型。
    /// コンポーネント型は new() 制約を満たす必要があります。
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Entity にコンポーネント型を関連付けます。
    /// </summary>
    /// <param name="componentType">関連付けるコンポーネントの型</param>
    public EntityComponentAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}

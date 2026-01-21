using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// EntityにCommandQueueを関連付けます。
/// この属性を付けると、HandleからCommandQueueへのアクセサが自動生成されます。
///
/// <para>生成されるコード:</para>
/// <list type="bullet">
///   <item><description>{Handle}.GetCommandQueue&lt;{QueueType}&gt;() - キューを取得</description></item>
///   <item><description>{Arena} : IHasCommandQueue&lt;{QueueType}&gt; - インターフェース実装</description></item>
/// </list>
///
/// <example>
/// 使用例:
/// <code>
/// // CommandQueueの定義
/// [CommandQueue]
/// public partial class GameCommandQueue
/// {
///     [CommandMethod]
///     public partial void ExecuteCommand(AnyHandle handle);
/// }
///
/// // EntityにCommandQueueを関連付け
/// [Entity(InitialCapacity = 100)]
/// [HasCommandQueue(typeof(GameCommandQueue))]
/// public partial class Player { }
///
/// // 使用方法
/// var queue = new GameCommandQueue();
/// var arena = new PlayerArena(commandQueue: queue);
/// var handle = arena.Create();
///
/// // HandleからQueueにアクセス
/// handle.GetCommandQueue&lt;GameCommandQueue&gt;().Enqueue&lt;MoveCommand&gt;(cmd =>
/// {
///     cmd.X = 10;
///     cmd.Y = 20;
/// });
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class HasCommandQueueAttribute : Attribute
{
    /// <summary>
    /// 関連付けるCommandQueueの型
    /// </summary>
    public Type QueueType { get; }

    /// <summary>
    /// EntityにCommandQueueを関連付けます。
    /// </summary>
    /// <param name="queueType">CommandQueue型</param>
    public HasCommandQueueAttribute(Type queueType)
    {
        QueueType = queueType ?? throw new ArgumentNullException(nameof(queueType));
    }
}

/// <summary>
/// CommandQueueを持つArenaのマーカーインターフェース
/// </summary>
/// <typeparam name="TQueue">CommandQueue型</typeparam>
public interface IHasCommandQueue<TQueue>
{
    /// <summary>
    /// CommandQueueを取得
    /// </summary>
    TQueue GetCommandQueue();
}

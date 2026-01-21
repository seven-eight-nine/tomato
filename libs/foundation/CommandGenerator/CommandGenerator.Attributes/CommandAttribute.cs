using System;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドクラスを特定のキューに登録する属性。
/// Source Generatorによって、指定されたキューへのEnqueueメソッドと実行ロジックが自動生成されます。
///
/// <para>機能:</para>
/// <list type="bullet">
///   <item><description>コマンドパターンの実装を自動化</description></item>
///   <item><description>優先度ベースの実行順序制御</description></item>
///   <item><description>オブジェクトプーリングによるメモリ効率化</description></item>
///   <item><description>型安全なコマンド呼び出し</description></item>
/// </list>
///
/// <para>使用要件:</para>
/// <list type="bullet">
///   <item><description>コマンドクラスは partial として宣言する必要があります</description></item>
///   <item><description>Execute()メソッドを実装する必要があります</description></item>
///   <item><description>複数のキューに登録する場合は、複数の属性を付与できます</description></item>
/// </list>
///
/// <example>
/// 基本的な使用例:
/// <code>
/// // コマンドキューを定義
/// [CommandQueue]
/// public partial class GameCommandQueue
/// {
///     [CommandMethod]
///     public partial void Execute();
/// }
///
/// // コマンドを定義
/// [Command&lt;GameCommandQueue&gt;(Priority = 10)]
/// public partial class MoveCommand
/// {
///     public int X, Y;
///
///     public void Execute()
///     {
///         Console.WriteLine($"移動: ({X}, {Y})");
///     }
/// }
///
/// // 使用方法
/// var queue = new GameCommandQueue();
/// GameCommandQueue.Enqueue&lt;MoveCommand&gt;(cmd => {
///     cmd.X = 100;
///     cmd.Y = 200;
/// });
/// queue.Execute();  // "移動: (100, 200)" と出力
/// </code>
/// </example>
///
/// <example>
/// 複数のキューに登録する例:
/// <code>
/// [Command&lt;GameCommandQueue&gt;(Priority = 10)]
/// [Command&lt;UICommandQueue&gt;(Priority = 5)]
/// public partial class LogCommand
/// {
///     public string Message;
///     public void Execute() => Console.WriteLine(Message);
/// }
///
/// // どちらのキューからも実行可能
/// GameCommandQueue.Enqueue&lt;LogCommand&gt;(cmd => cmd.Message = "ゲームログ");
/// UICommandQueue.Enqueue&lt;LogCommand&gt;(cmd => cmd.Message = "UIログ");
/// </code>
/// </example>
/// </summary>
/// <typeparam name="TQueue">登録先のCommandQueueクラス（[CommandQueue]属性が付いている必要があります）</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CommandAttribute<TQueue> : Attribute
    where TQueue : class
{
    /// <summary>
    /// キュー内での実行優先度。
    /// 大きい値ほど先に実行されます。デフォルトは0です。
    ///
    /// <para>優先度の使用例:</para>
    /// <list type="bullet">
    ///   <item><description>高優先度（100以上）: 緊急の処理（エラーハンドリング等）</description></item>
    ///   <item><description>通常優先度（0-99）: 通常のゲームロジック</description></item>
    ///   <item><description>低優先度（負の値）: 後回しにしてよい処理（ログ出力等）</description></item>
    /// </list>
    ///
    /// <remarks>
    /// 同じ優先度の場合、Enqueueされた順序で実行されます。
    /// </remarks>
    ///
    /// <example>
    /// <code>
    /// [Command&lt;GameCommandQueue&gt;(Priority = 100)]
    /// public partial class CriticalCommand { /* ... */ }
    ///
    /// [Command&lt;GameCommandQueue&gt;(Priority = 0)]
    /// public partial class NormalCommand { /* ... */ }
    ///
    /// [Command&lt;GameCommandQueue&gt;(Priority = -10)]
    /// public partial class LowPriorityCommand { /* ... */ }
    /// </code>
    /// </example>
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// コマンドオブジェクトプールの初期容量。
    /// デフォルトは8です。
    ///
    /// <para>設定のガイドライン:</para>
    /// <list type="bullet">
    ///   <item><description>低頻度のコマンド（1フレームに数回）: 4-8</description></item>
    ///   <item><description>中頻度のコマンド（1フレームに数十回）: 16-32</description></item>
    ///   <item><description>高頻度のコマンド（1フレームに数百回）: 64以上</description></item>
    /// </list>
    ///
    /// <remarks>
    /// プールは自動的に拡張されますが、適切な初期容量を設定することで
    /// 頻繁な再割り当てを避けられます。
    /// </remarks>
    ///
    /// <example>
    /// <code>
    /// [Command&lt;GameCommandQueue&gt;(PoolInitialCapacity = 64)]
    /// public partial class HighFrequencyCommand { /* ... */ }
    /// </code>
    /// </example>
    /// </summary>
    public int PoolInitialCapacity { get; set; } = 8;
}

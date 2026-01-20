using System;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドキューのメソッドを定義する属性。
/// この属性を付与したpartialメソッドに対して、Source Generatorが自動的に実装を生成します。
///
/// <para>生成される実装:</para>
/// <list type="bullet">
///   <item><description>キューに登録されたすべてのコマンドを優先度順に実行</description></item>
///   <item><description>実行後のクリーンアップ処理（clearパラメータに依存）</description></item>
///   <item><description>例外処理とエラーハンドリング</description></item>
/// </list>
///
/// <para>使用要件:</para>
/// <list type="bullet">
///   <item><description>partialメソッドとして宣言する必要があります</description></item>
///   <item><description>[CommandQueue]属性が付いたクラス内で定義する必要があります</description></item>
///   <item><description>戻り値はvoid、またはコマンド実行結果を表す型である必要があります</description></item>
/// </list>
///
/// <example>
/// 基本的な使用例:
/// <code>
/// [CommandQueue]
/// public partial class GameCommandQueue
/// {
///     // 実行後に自動クリア
///     [CommandMethod]
///     public partial void Execute();
///
///     // 実行後もキューを保持（複数回実行可能）
///     [CommandMethod(clear: false)]
///     public partial void ExecuteAndKeep();
/// }
///
/// // 使用方法
/// var queue = new GameCommandQueue();
/// GameCommandQueue.Enqueue&lt;MoveCommand&gt;(cmd => { /* ... */ });
/// queue.Execute();  // 実行後、キューがクリアされる
///
/// GameCommandQueue.Enqueue&lt;JumpCommand&gt;(cmd => { /* ... */ });
/// queue.ExecuteAndKeep();  // 実行後もキューに残る
/// queue.ExecuteAndKeep();  // 同じコマンドを再度実行
/// </code>
/// </example>
///
/// <example>
/// 複数のメソッドを定義する例:
/// <code>
/// [CommandQueue]
/// public partial class GameCommandQueue
/// {
///     [CommandMethod]
///     public partial void ExecuteAll();
///
///     [CommandMethod(clear: false)]
///     public partial void ExecuteOnce();
///
///     // カスタムロジックを追加できる
///     public void ExecuteWithLogging()
///     {
///         Console.WriteLine("実行開始");
///         ExecuteAll();
///         Console.WriteLine("実行完了");
///     }
/// }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandMethodAttribute : Attribute
{
    /// <summary>
    /// メソッド実行後にキューをクリアするかどうかを指定します。
    ///
    /// <para>動作:</para>
    /// <list type="bullet">
    ///   <item><description>true（デフォルト）: 実行後にキューをクリアし、コマンドをプールに返却します</description></item>
    ///   <item><description>false: 実行後もキューを保持し、同じコマンドを再度実行できます</description></item>
    /// </list>
    ///
    /// <para>使用場面:</para>
    /// <list type="bullet">
    ///   <item><description>clear = true: 通常のコマンド実行（1回実行したら削除）</description></item>
    ///   <item><description>clear = false: リプレイ機能、デバッグ、繰り返し実行が必要な場合</description></item>
    /// </list>
    ///
    /// <remarks>
    /// clearをfalseにした場合、メモリリークを防ぐために
    /// 適切なタイミングで手動でキューをクリアする必要があります。
    /// </remarks>
    ///
    /// <example>
    /// <code>
    /// [CommandQueue]
    /// public partial class ReplayQueue
    /// {
    ///     [CommandMethod(clear: false)]
    ///     public partial void Replay();
    ///
    ///     public void Clear()
    ///     {
    ///         // 手動でクリア処理を実装
    ///     }
    /// }
    ///
    /// // リプレイ機能の実装
    /// replayQueue.Replay();  // 1回目
    /// replayQueue.Replay();  // 同じコマンドを再実行
    /// replayQueue.Clear();   // 完了後にクリア
    /// </code>
    /// </example>
    /// </summary>
    public bool Clear { get; }

    /// <summary>
    /// CommandMethodAttributeの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="clear">メソッド実行後にキューをクリアする場合はtrue、保持する場合はfalse。デフォルトはtrue。</param>
    public CommandMethodAttribute(bool clear = true)
    {
        Clear = clear;
    }
}

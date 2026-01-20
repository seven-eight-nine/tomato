using System;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドキュークラスを定義する属性。
/// この属性を付与したpartialクラスに対して、Source Generatorが自動的にキュー管理機能を生成します。
///
/// <para>生成されるコード:</para>
/// <list type="bullet">
///   <item><description>静的Enqueue&lt;T&gt;メソッド - コマンドをキューに追加</description></item>
///   <item><description>優先度ベースのキュー管理</description></item>
///   <item><description>オブジェクトプーリング機能</description></item>
///   <item><description>CommandMethodで指定されたメソッドの実装</description></item>
/// </list>
///
/// <para>使用要件:</para>
/// <list type="bullet">
///   <item><description>対象クラスは partial として宣言する必要があります</description></item>
///   <item><description>[CommandMethod]属性を付けたpartialメソッドを定義する必要があります</description></item>
///   <item><description>このキューに登録するコマンドは[Command&lt;このクラス&gt;]属性が必要です</description></item>
/// </list>
///
/// <example>
/// 基本的な使用例:
/// <code>
/// [CommandQueue]
/// public partial class GameCommandQueue
/// {
///     [CommandMethod]
///     public partial void Execute();
///
///     [CommandMethod(clear: false)]
///     public partial void ProcessWithoutClear();
/// }
///
/// // このキューに登録するコマンド
/// [Command&lt;GameCommandQueue&gt;]
/// public partial class MoveCommand
/// {
///     public int X, Y;
///     public void Execute() => Console.WriteLine($"({X}, {Y})");
/// }
///
/// // 使用方法
/// var queue = new GameCommandQueue();
/// GameCommandQueue.Enqueue&lt;MoveCommand&gt;(cmd => { cmd.X = 10; cmd.Y = 20; });
/// queue.Execute();  // コマンド実行後、自動的にクリア
/// </code>
/// </example>
///
/// <example>
/// 複数のキューを定義する例:
/// <code>
/// [CommandQueue]
/// public partial class GameLogicQueue
/// {
///     [CommandMethod]
///     public partial void Execute();
/// }
///
/// [CommandQueue]
/// public partial class UICommandQueue
/// {
///     [CommandMethod]
///     public partial void Execute();
/// }
///
/// // 異なるキューで異なるコマンドを管理
/// [Command&lt;GameLogicQueue&gt;]
/// public partial class AttackCommand { /* ... */ }
///
/// [Command&lt;UICommandQueue&gt;]
/// public partial class ShowDialogCommand { /* ... */ }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandQueueAttribute : Attribute
{
}

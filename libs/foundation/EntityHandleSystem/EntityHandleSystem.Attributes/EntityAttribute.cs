using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// クラスまたは構造体をエンティティ型としてマークし、型固有のHandleとArenaクラスを自動生成します。
/// Source Generatorにより、安全なエンティティ参照システムが自動的に構築されます。
///
/// <para>生成されるコード:</para>
/// <list type="bullet">
///   <item><description>{TypeName}Handle - 型安全なハンドル構造体（世代番号による無効化検出機能付き）</description></item>
///   <item><description>{TypeName}Arena - エンティティのプール管理クラス（作成・削除・取得を管理）</description></item>
///   <item><description>TryGetメソッド - ハンドルからエンティティを安全に取得</description></item>
///   <item><description>各エンティティメソッド用のTry*メソッド - ハンドル経由で安全にメソッド呼び出し</description></item>
/// </list>
///
/// <para>使用要件:</para>
/// <list type="bullet">
///   <item><description>対象型は partial として宣言されている必要があります</description></item>
///   <item><description>[EntityMethod]属性でメソッドをマークすると、ハンドル経由で呼び出せるようになります</description></item>
/// </list>
///
/// <example>
/// 使用例:
/// <code>
/// [Entity(InitialCapacity = 100)]
/// public partial class Enemy
/// {
///     public int Health;
///     public Vector3 Position;
///
///     [EntityMethod]
///     public void TakeDamage(int damage) => Health -= damage;
///
///     [EntityMethod]
///     public void MoveTo(Vector3 newPos) => Position = newPos;
/// }
///
/// // 使用方法
/// var arena = new EnemyArena();
/// var handle = arena.Create();  // エンティティを作成
/// handle.TryTakeDamage(10);     // 安全にメソッド呼び出し
/// handle.Dispose();             // エンティティを削除（ハンドルは無効化される）
/// handle.TryTakeDamage(5);      // falseを返す（既に削除済み）
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class EntityAttribute : Attribute
{
    /// <summary>
    /// エンティティプールの初期容量。
    /// 予想されるエンティティ数に応じて設定してください。デフォルトは256です。
    ///
    /// <para>設定のガイドライン:</para>
    /// <list type="bullet">
    ///   <item><description>少数（10-50個）のエンティティ: 32-64</description></item>
    ///   <item><description>中規模（50-200個）のエンティティ: 128-256</description></item>
    ///   <item><description>大規模（200個以上）のエンティティ: 512以上</description></item>
    /// </list>
    ///
    /// <remarks>
    /// プールは自動的に拡張されるため、初期容量を超えても問題ありませんが、
    /// 適切な値を設定することで初期の再割り当てを避けられます。
    /// </remarks>
    /// </summary>
    public int InitialCapacity { get; set; } = 256;

    /// <summary>
    /// 生成されるArenaクラスのカスタム名。
    /// nullの場合、"{TypeName}Arena"という名前が自動的に使用されます。
    ///
    /// <example>
    /// 使用例:
    /// <code>
    /// [Entity(ArenaName = "EnemyPool")]
    /// public partial class Enemy { }
    ///
    /// // 生成されるクラス名: EnemyPool（デフォルトのEnemyArenaではなく）
    /// var pool = new EnemyPool();
    /// </code>
    /// </example>
    /// </summary>
    public string ArenaName { get; set; } = null;

    /// <summary>
    /// trueに設定すると、エンティティのスナップショット/復元機能が自動生成されます。
    ///
    /// <para>生成されるコード:</para>
    /// <list type="bullet">
    ///   <item><description>{TypeName}Snapshot - エンティティ状態のスナップショット構造体</description></item>
    ///   <item><description>{ArenaName}Snapshot - Arena全体のスナップショット構造体</description></item>
    ///   <item><description>ISnapshotableArena&lt;{ArenaName}Snapshot&gt; - Arena のインターフェース実装</description></item>
    ///   <item><description>CaptureSnapshot() - 現在の状態をキャプチャ</description></item>
    ///   <item><description>RestoreSnapshot() - 保存した状態を復元</description></item>
    /// </list>
    ///
    /// <example>
    /// 使用例:
    /// <code>
    /// [Entity(Snapshotable = true)]
    /// public partial class Enemy
    /// {
    ///     public int Health;
    ///     public Vector3 Position;
    /// }
    ///
    /// // スナップショット取得
    /// var snapshot = arena.CaptureSnapshot();
    ///
    /// // ゲーム進行...
    ///
    /// // 状態を復元
    /// arena.RestoreSnapshot(snapshot);
    /// </code>
    /// </example>
    /// </summary>
    public bool Snapshotable { get; set; } = false;
}

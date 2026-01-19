using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// メソッドを生成されるHandle型経由で呼び出し可能にします。
/// このメソッドは、スレッドセーフなアクセスと世代番号による検証でラップされます。
///
/// <para>動作:</para>
/// <list type="bullet">
///   <item><description>Handle経由でメソッドを呼び出せる Try{MethodName} メソッドが生成されます</description></item>
///   <item><description>呼び出し時に世代番号を自動検証（削除済みエンティティへのアクセスを防止）</description></item>
///   <item><description>スレッドセーフ（内部でロックを使用）</description></item>
///   <item><description>戻り値がある場合は out パラメータで受け取ります</description></item>
/// </list>
///
/// <example>
/// 使用例:
/// <code>
/// [Entity]
/// public partial class Enemy
/// {
///     public int Health;
///
///     [EntityMethod]
///     public void TakeDamage(int damage)
///     {
///         Health -= damage;
///     }
///
///     [EntityMethod]
///     public int GetHealth()
///     {
///         return Health;
///     }
/// }
///
/// // 生成されるメソッド（Handle型に）
/// // bool TryTakeDamage(int damage)  // 成功時true、無効なハンドルの場合false
/// // bool TryGetHealth(out int result)
///
/// // 使用方法
/// EnemyHandle handle = arena.Create();
/// if (handle.TryTakeDamage(10))  // 成功
/// {
///     if (handle.TryGetHealth(out int health))
///     {
///         Console.WriteLine($"Health: {health}");
///     }
/// }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EntityMethodAttribute : Attribute
{
    /// <summary>
    /// trueの場合、追加で _Unsafe バリアントのメソッドを生成します。
    /// _Unsafe バリアントはロックと世代番号チェックをスキップするため、パフォーマンス重視のシナリオで使用できます。
    ///
    /// <para>警告:</para>
    /// <list type="bullet">
    ///   <item><description>呼び出し側がハンドルの有効性を保証する責任があります</description></item>
    ///   <item><description>無効なハンドルでの呼び出しは未定義動作を引き起こす可能性があります</description></item>
    ///   <item><description>スレッドセーフではありません</description></item>
    /// </list>
    ///
    /// <para>使用場面:</para>
    /// <list type="bullet">
    ///   <item><description>ゲームループ内の頻繁な呼び出しで、ハンドルの有効性が保証されている場合</description></item>
    ///   <item><description>プロファイリングでロックがボトルネックと判明した場合</description></item>
    ///   <item><description>シングルスレッド環境で、ハンドルの削除がないことが確実な場合</description></item>
    /// </list>
    ///
    /// <example>
    /// 使用例:
    /// <code>
    /// [Entity]
    /// public partial class Enemy
    /// {
    ///     [EntityMethod(Unsafe = true)]
    ///     public void UpdatePosition(Vector3 newPos) { /* ... */ }
    /// }
    ///
    /// // 生成されるメソッド
    /// // bool TryUpdatePosition(Vector3 newPos)         // 安全版（ロック+検証）
    /// // void TryUpdatePosition_Unsafe(Vector3 newPos)  // 高速版（検証なし）
    ///
    /// // 使用方法（ゲームループ内で、ハンドルが有効と保証されている場合）
    /// foreach (var handle in activeEnemies)
    /// {
    ///     handle.TryUpdatePosition_Unsafe(newPos);  // 高速だが、ハンドルの有効性は呼び出し側が保証
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public bool Unsafe { get; set; } = false;
}

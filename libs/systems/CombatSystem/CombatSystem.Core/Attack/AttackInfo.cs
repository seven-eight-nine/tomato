namespace Tomato.CombatSystem;

/// <summary>
/// 攻撃パラメータの基底クラス。アプリ側で継承して使う。
///
/// <example>
/// <code>
/// public class MyAttackInfo : AttackInfo
/// {
///     public float Damage { get; set; }
///
///     public override bool CanTarget(IDamageReceiver target)
///         => !target.Equals(Attacker);  // 自傷防止
/// }
/// </code>
/// </example>
/// </summary>
public abstract class AttackInfo
{
    /// <summary>攻撃者。CanTarget での自傷防止に使う。</summary>
    public IDamageReceiver? Attacker { get; set; }

    /// <summary>
    /// ヒットグループID。
    ///
    /// 同じ HitGroup を持つ攻撃は HitHistory を共有する。
    /// 例: 剣の振り1回で3つのヒット判定がある場合、全て同じ HitGroup にすると
    /// 同一ターゲットには最初の1回しかヒットしない。
    ///
    /// 0以下だと CombatManager が一意のIDを自動生成する。
    /// </summary>
    public int HitGroup { get; set; }

    /// <summary>
    /// 再ヒット間隔（秒）。
    ///
    /// 前回ヒットからこの秒数が経過するまで、同一ターゲットに再ヒットしない。
    /// 0以下だとチェックしない（HittableCount のみで制御）。
    ///
    /// 使う場合は HitHistory.Update を毎フレーム呼ぶこと。
    ///
    /// <example>
    /// 0.2秒間隔で最大5回ヒットする攻撃:
    /// <code>
    /// info.IntervalTime = 0.2f;
    /// info.HittableCount = 5;
    /// </code>
    /// </example>
    /// </summary>
    public float IntervalTime { get; set; }

    /// <summary>
    /// 同一ターゲットへの最大ヒット数。
    ///
    /// この回数ヒットすると、そのターゲットには以降ヒットしない。
    /// 0だと無制限（IntervalTime 経過ごとに何度でもヒット）。
    ///
    /// <example>
    /// 1回だけヒットする通常攻撃:
    /// <code>
    /// info.HittableCount = 1;
    /// </code>
    /// </example>
    /// </summary>
    public int HittableCount { get; set; }

    /// <summary>
    /// 全体での最大ヒット数（貫通数）。
    ///
    /// 全ターゲット合計でこの回数ヒットすると、以降どのターゲットにもヒットしない。
    /// 0だと無制限。
    ///
    /// <example>
    /// 最大3体まで貫通する攻撃:
    /// <code>
    /// info.AttackableCount = 3;
    /// </code>
    /// </example>
    /// </summary>
    public int AttackableCount { get; set; }

    /// <summary>
    /// ターゲットに攻撃可能か判定する。
    /// 自傷防止、陣営チェック、無敵判定などを実装する。
    /// </summary>
    public abstract bool CanTarget(IDamageReceiver target);
}

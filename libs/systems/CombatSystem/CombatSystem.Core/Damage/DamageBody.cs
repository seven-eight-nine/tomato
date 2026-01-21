using System.Threading;

namespace Tomato.CombatSystem;

/// <summary>
/// ダメージ判定の本体。衝突形状と<see cref="IDamageReceiver"/>を紐づける。
/// 1つのキャラクターが複数のDamageBodyを持てる（頭、胴、足など部位ごと）。
/// </summary>
public class DamageBody
{
    private static int _nextBodyId = 1;

    /// <summary>ユニークID。自動割り当て。</summary>
    public int BodyId { get; }

    /// <summary>所有者。<see cref="BindOwner"/>で設定。</summary>
    public IDamageReceiver? Owner { get; private set; }

    /// <summary>
    /// 処理優先度。複数部位に同時ヒット時、高い順に処理される。
    /// 同一Ownerの重複は最高Priorityのものだけが採用される。
    /// </summary>
    public int Priority { get; set; }

    public DamageBody()
    {
        BodyId = Interlocked.Increment(ref _nextBodyId);
    }

    /// <summary>オーナーをバインド。</summary>
    public void BindOwner(IDamageReceiver owner) => Owner = owner;

    /// <summary>オーナーをアンバインド。</summary>
    public void UnbindOwner() => Owner = null;

    /// <summary>オーナーがバインドされているか。</summary>
    public bool IsBound => Owner != null;
}

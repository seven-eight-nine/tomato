using System;

namespace Tomato.UnitLODSystem
{

/// <summary>
/// LODの各詳細レベルを実装するインターフェース。
/// Unitが管理し、フェーズ変更時とTick時にコールバックを受け取る。
/// </summary>
/// <typeparam name="TOwner">所有者のUnit型</typeparam>
public interface IUnitDetail<TOwner> : IDisposable where TOwner : Unit<TOwner>
{
    /// <summary>
    /// 現在のフェーズ。実装側が内部状態に基づいて更新する。
    /// </summary>
    UnitPhase Phase { get; }

    /// <summary>
    /// 毎Tick呼び出される。非同期処理の進行やフェーズ遷移を行う。
    /// </summary>
    /// <param name="owner">所有者のUnit</param>
    /// <param name="phase">現在のフェーズ</param>
    void OnUpdatePhase(TOwner owner, UnitPhase phase);

    /// <summary>
    /// フェーズ変更時に呼び出される。Unitが遷移を開始するときに呼ばれる。
    /// </summary>
    /// <param name="owner">所有者のUnit</param>
    /// <param name="prev">変更前のフェーズ</param>
    /// <param name="next">変更後のフェーズ</param>
    void OnChangePhase(TOwner owner, UnitPhase prev, UnitPhase next);
}

}

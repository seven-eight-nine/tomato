using System;
using System.Collections.Generic;
using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果マネージャーインターフェース
    /// </summary>
    public interface IEffectManager
    {
        #region Apply

        /// <summary>効果を付与</summary>
        ApplyResult TryApply(ulong targetId, EffectId effectId, ulong sourceId, ApplyOptions? options = null);

        #endregion

        #region Remove

        /// <summary>インスタンスを除去</summary>
        void Remove(EffectInstanceId instanceId, RemovalReasonId reason);

        /// <summary>対象の全効果を除去</summary>
        void RemoveAll(ulong targetId, RemovalReasonId reason);

        /// <summary>タグで効果を除去</summary>
        int RemoveByTag(ulong targetId, TagId tag, RemovalReasonId reason);

        #endregion

        #region Modify

        /// <summary>スタックを追加</summary>
        void AddStacks(EffectInstanceId instanceId, int count);

        /// <summary>スタックを設定</summary>
        void SetStacks(EffectInstanceId instanceId, int count);

        /// <summary>時間を延長</summary>
        void ExtendDuration(EffectInstanceId instanceId, TickDuration extension);

        /// <summary>フラグを設定</summary>
        void SetFlag(EffectInstanceId instanceId, FlagId flag, bool value);

        #endregion

        #region Query

        /// <summary>インスタンスを取得</summary>
        EffectInstance? GetInstance(EffectInstanceId instanceId);

        /// <summary>対象の全効果を取得</summary>
        IEnumerable<EffectInstance> GetEffects(ulong targetId);

        /// <summary>タグで効果を取得</summary>
        IEnumerable<EffectInstance> GetEffectsByTag(ulong targetId, TagId tag);

        /// <summary>効果を持っているか</summary>
        bool HasEffect(ulong targetId, EffectId effectId);

        /// <summary>タグを持つ効果があるか</summary>
        bool HasEffectWithTag(ulong targetId, TagId tag);

        #endregion

        #region Result

        /// <summary>
        /// 対象の全効果を適用した結果を計算する
        /// </summary>
        /// <typeparam name="TResult">結果の型</typeparam>
        /// <param name="targetId">対象ID</param>
        /// <param name="initial">初期値</param>
        /// <returns>全効果を適用後の結果</returns>
        TResult CalculateResult<TResult>(ulong targetId, TResult initial) where TResult : struct;

        #endregion

        #region Tick

        /// <summary>ティック処理</summary>
        void ProcessTick(GameTick currentTick);

        #endregion

        #region Events

        event Action<EffectAppliedEvent>? OnEffectApplied;
        event Action<EffectRemovedEvent>? OnEffectRemoved;
        event Action<StackChangedEvent>? OnStackChanged;

        #endregion
    }
}

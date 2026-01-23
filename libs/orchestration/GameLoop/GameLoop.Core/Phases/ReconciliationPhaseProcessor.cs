using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline;

namespace Tomato.GameLoop.Phases;

/// <summary>
/// 位置調停システム。
/// 依存順を計算し、押し出し処理を実行。
/// </summary>
public sealed class ReconciliationSystem : IOrderedSerialSystem
{
    private readonly IDependencyResolver _dependencyResolver;
    private readonly IPositionReconciler _positionReconciler;

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public SystemPipeline.Query.IEntityQuery? Query => null;

    /// <summary>
    /// 循環依存検出時のコールバック。
    /// </summary>
    public Action? OnCycleDetected { get; set; }

    /// <summary>
    /// ReconciliationSystemを生成する。
    /// </summary>
    public ReconciliationSystem(
        IDependencyResolver dependencyResolver,
        IPositionReconciler positionReconciler)
    {
        _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
        _positionReconciler = positionReconciler ?? throw new ArgumentNullException(nameof(positionReconciler));
    }

    /// <inheritdoc/>
    public void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output)
    {
        // 依存順を計算（トポロジカルソート）
        var result = _dependencyResolver.ResolveDependencies(input, output);

        if (result == DependencyResolutionResult.CycleDetected)
        {
            // 循環依存を検出 - 開発ビルドでは警告
            OnCycleDetected?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // 依存順に位置調停
        foreach (var handle in entities)
        {
            _positionReconciler.Reconcile(handle);
        }
    }
}

/// <summary>
/// 依存順を解決するインターフェース。
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// Entityの依存順を解決する。
    /// </summary>
    /// <param name="entities">全Entity</param>
    /// <param name="sortedResult">ソート結果の出力先</param>
    /// <returns>解決結果</returns>
    DependencyResolutionResult ResolveDependencies(
        IReadOnlyList<AnyHandle> entities,
        List<AnyHandle> sortedResult);
}

/// <summary>
/// 依存解決の結果。
/// </summary>
public enum DependencyResolutionResult
{
    /// <summary>成功。</summary>
    Success,

    /// <summary>循環依存を検出。</summary>
    CycleDetected
}

/// <summary>
/// 位置調停を行うインターフェース。
/// </summary>
public interface IPositionReconciler
{
    /// <summary>
    /// Entityの位置を調停する。
    /// </summary>
    /// <param name="handle">EntityのAnyHandle</param>
    void Reconcile(AnyHandle handle);
}

using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline.Query;

/// <summary>
/// 特定のコンポーネントを持つエンティティをフィルタリングするクエリ。
/// </summary>
/// <typeparam name="TComponent">フィルタリング対象のコンポーネント型</typeparam>
public sealed class HasComponentQuery<TComponent> : IEntityQuery
{
    private readonly Func<VoidHandle, bool> _hasComponentCheck;

    /// <summary>
    /// HasComponentQueryを作成します。
    /// </summary>
    /// <param name="hasComponentCheck">エンティティがコンポーネントを持っているかチェックする関数</param>
    public HasComponentQuery(Func<VoidHandle, bool> hasComponentCheck)
    {
        _hasComponentCheck = hasComponentCheck ?? throw new ArgumentNullException(nameof(hasComponentCheck));
    }

    /// <inheritdoc/>
    public IEnumerable<VoidHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<VoidHandle> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.IsValid && _hasComponentCheck(entity))
            {
                yield return entity;
            }
        }
    }
}

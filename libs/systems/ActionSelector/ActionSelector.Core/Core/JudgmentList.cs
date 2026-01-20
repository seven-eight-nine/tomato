using System;
using System.Collections;
using System.Collections.Generic;

namespace Tomato.ActionSelector;

/// <summary>
/// ジャッジメントと実行時優先度のペア。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public readonly struct JudgmentEntry<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    /// <summary>
    /// ジャッジメント。
    /// </summary>
    public readonly IActionJudgment<TCategory, TInput, TContext> Judgment;

    /// <summary>
    /// 実行時に上書きする優先度。nullの場合はジャッジメント本来の優先度を使用。
    /// </summary>
    public readonly ActionPriority? OverridePriority;

    /// <summary>
    /// エントリを作成する。
    /// </summary>
    public JudgmentEntry(IActionJudgment<TCategory, TInput, TContext> judgment, ActionPriority? overridePriority = null)
    {
        Judgment = judgment ?? throw new ArgumentNullException(nameof(judgment));
        OverridePriority = overridePriority;
    }

    /// <summary>
    /// 実効優先度を取得する。
    /// </summary>
    public ActionPriority GetEffectivePriority(in FrameState<TInput, TContext> state)
    {
        return OverridePriority ?? Judgment.GetPriority(in state);
    }
}

/// <summary>
/// 実行時優先度の上書きをサポートするジャッジメントリスト。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public class JudgmentList<TCategory, TInput, TContext>
    : IReadOnlyList<JudgmentEntry<TCategory, TInput, TContext>>
    where TCategory : struct, Enum
{
    private readonly List<JudgmentEntry<TCategory, TInput, TContext>> _entries;

    public JudgmentList()
    {
        _entries = new List<JudgmentEntry<TCategory, TInput, TContext>>();
    }

    public JudgmentList(int capacity)
    {
        _entries = new List<JudgmentEntry<TCategory, TInput, TContext>>(capacity);
    }

    public int Count => _entries.Count;

    public JudgmentEntry<TCategory, TInput, TContext> this[int index] => _entries[index];

    public JudgmentList<TCategory, TInput, TContext> Add(
        IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        _entries.Add(new JudgmentEntry<TCategory, TInput, TContext>(judgment));
        return this;
    }

    public JudgmentList<TCategory, TInput, TContext> Add(
        IActionJudgment<TCategory, TInput, TContext> judgment,
        ActionPriority overridePriority)
    {
        _entries.Add(new JudgmentEntry<TCategory, TInput, TContext>(judgment, overridePriority));
        return this;
    }

    public JudgmentList<TCategory, TInput, TContext> AddRange(
        JudgmentList<TCategory, TInput, TContext> other)
    {
        for (int i = 0; i < other.Count; i++)
        {
            _entries.Add(other[i]);
        }
        return this;
    }

    public bool Remove(IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Judgment, judgment))
            {
                _entries.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public JudgmentList<TCategory, TInput, TContext> Clear()
    {
        _entries.Clear();
        return this;
    }

    public bool Contains(IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Judgment, judgment))
            {
                return true;
            }
        }
        return false;
    }

    public JudgmentList<TCategory, TInput, TContext> SetPriority(
        IActionJudgment<TCategory, TInput, TContext> judgment,
        ActionPriority overridePriority)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Judgment, judgment))
            {
                _entries[i] = new JudgmentEntry<TCategory, TInput, TContext>(judgment, overridePriority);
                return this;
            }
        }
        _entries.Add(new JudgmentEntry<TCategory, TInput, TContext>(judgment, overridePriority));
        return this;
    }

    public JudgmentList<TCategory, TInput, TContext> ClearPriority(
        IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Judgment, judgment))
            {
                _entries[i] = new JudgmentEntry<TCategory, TInput, TContext>(judgment);
                return this;
            }
        }
        return this;
    }

    public IEnumerator<JudgmentEntry<TCategory, TInput, TContext>> GetEnumerator()
        => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// GameState/InputState用のJudgmentListエイリアス。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class JudgmentList<TCategory>
    : IReadOnlyList<JudgmentEntry<TCategory, InputState, GameState>>
    where TCategory : struct, Enum
{
    private readonly JudgmentList<TCategory, InputState, GameState> _inner;

    public JudgmentList()
    {
        _inner = new JudgmentList<TCategory, InputState, GameState>();
    }

    public JudgmentList(int capacity)
    {
        _inner = new JudgmentList<TCategory, InputState, GameState>(capacity);
    }

    public int Count => _inner.Count;

    public JudgmentEntry<TCategory, InputState, GameState> this[int index] => _inner[index];

    public JudgmentList<TCategory> Add(
        IActionJudgment<TCategory, InputState, GameState> judgment)
    {
        _inner.Add(judgment);
        return this;
    }

    public JudgmentList<TCategory> Add(
        IActionJudgment<TCategory, InputState, GameState> judgment,
        ActionPriority overridePriority)
    {
        _inner.Add(judgment, overridePriority);
        return this;
    }

    public JudgmentList<TCategory> AddRange(JudgmentList<TCategory> other)
    {
        _inner.AddRange(other._inner);
        return this;
    }

    public bool Remove(IActionJudgment<TCategory, InputState, GameState> judgment)
        => _inner.Remove(judgment);

    public JudgmentList<TCategory> Clear()
    {
        _inner.Clear();
        return this;
    }

    public bool Contains(IActionJudgment<TCategory, InputState, GameState> judgment)
        => _inner.Contains(judgment);

    public JudgmentList<TCategory> SetPriority(
        IActionJudgment<TCategory, InputState, GameState> judgment,
        ActionPriority overridePriority)
    {
        _inner.SetPriority(judgment, overridePriority);
        return this;
    }

    public JudgmentList<TCategory> ClearPriority(
        IActionJudgment<TCategory, InputState, GameState> judgment)
    {
        _inner.ClearPriority(judgment);
        return this;
    }

    /// <summary>
    /// 内部のジェネリックリストを取得する。
    /// </summary>
    public JudgmentList<TCategory, InputState, GameState> AsGeneric() => _inner;

    public IEnumerator<JudgmentEntry<TCategory, InputState, GameState>> GetEnumerator()
        => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

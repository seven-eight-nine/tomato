using System;

namespace Tomato.FlowTree;

/// <summary>
/// 固定サイズのコールスタック（ゼロGC）。
/// </summary>
public sealed class FlowCallStack
{
    private readonly CallFrame[] _frames;
    private int _count;

    /// <summary>
    /// 現在のスタック深度。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 最大スタック深度。
    /// </summary>
    public int Capacity => _frames.Length;

    /// <summary>
    /// スタックが空かどうか。
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// スタックが満杯かどうか。
    /// </summary>
    public bool IsFull => _count >= _frames.Length;

    /// <summary>
    /// FlowCallStackを作成する。
    /// </summary>
    /// <param name="maxDepth">最大深度</param>
    public FlowCallStack(int maxDepth = 32)
    {
        if (maxDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Must be positive.");
        _frames = new CallFrame[maxDepth];
        _count = 0;
    }

    /// <summary>
    /// フレームをプッシュする。
    /// </summary>
    /// <param name="frame">プッシュするフレーム</param>
    /// <returns>成功した場合はtrue、スタックオーバーフローの場合はfalse</returns>
    public bool TryPush(CallFrame frame)
    {
        if (_count >= _frames.Length)
            return false;

        _frames[_count++] = frame;
        return true;
    }

    /// <summary>
    /// フレームをプッシュする（例外版）。
    /// </summary>
    /// <param name="frame">プッシュするフレーム</param>
    /// <exception cref="InvalidOperationException">スタックオーバーフロー時</exception>
    public void Push(CallFrame frame)
    {
        if (!TryPush(frame))
            throw new InvalidOperationException($"Call stack overflow. Max depth: {_frames.Length}");
    }

    /// <summary>
    /// フレームをポップする。
    /// </summary>
    /// <param name="frame">ポップしたフレーム</param>
    /// <returns>成功した場合はtrue、スタックが空の場合はfalse</returns>
    public bool TryPop(out CallFrame frame)
    {
        if (_count <= 0)
        {
            frame = default;
            return false;
        }

        frame = _frames[--_count];
        return true;
    }

    /// <summary>
    /// フレームをポップする（例外版）。
    /// </summary>
    /// <returns>ポップしたフレーム</returns>
    /// <exception cref="InvalidOperationException">スタックが空の場合</exception>
    public CallFrame Pop()
    {
        if (!TryPop(out var frame))
            throw new InvalidOperationException("Call stack is empty.");
        return frame;
    }

    /// <summary>
    /// 先頭のフレームを取得（ポップなし）。
    /// </summary>
    /// <param name="frame">先頭のフレーム</param>
    /// <returns>成功した場合はtrue、スタックが空の場合はfalse</returns>
    public bool TryPeek(out CallFrame frame)
    {
        if (_count <= 0)
        {
            frame = default;
            return false;
        }

        frame = _frames[_count - 1];
        return true;
    }

    /// <summary>
    /// 指定したツリーがスタック内に存在するか確認（再帰検出用）。
    /// </summary>
    /// <param name="tree">確認するツリー</param>
    /// <returns>存在する場合はtrue</returns>
    public bool Contains(FlowTree tree)
    {
        for (int i = 0; i < _count; i++)
        {
            if (ReferenceEquals(_frames[i].Tree, tree))
                return true;
        }
        return false;
    }

    /// <summary>
    /// スタックをクリアする。
    /// </summary>
    public void Clear()
    {
        _count = 0;
    }

    /// <summary>
    /// 指定インデックスのフレームを取得。
    /// </summary>
    /// <param name="index">インデックス（0が最下層）</param>
    /// <returns>フレーム</returns>
    public CallFrame this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _frames[index];
        }
    }
}

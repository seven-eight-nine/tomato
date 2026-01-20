using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンド用オブジェクトプール（スレッドセーフ）
/// </summary>
/// <typeparam name="T">プールするコマンド型</typeparam>
public static class CommandPool<T> where T : class, ICommandPoolable<T>, new()
{
    private static readonly Stack<T> s_pool;
    private static readonly object s_lock = new object();
    private static readonly int s_initialCapacity;

    static CommandPool()
    {
        // 初期容量はコマンド側で定義される静的プロパティから取得
        s_initialCapacity = CommandPoolConfig<T>.InitialCapacity;
        s_pool = new Stack<T>(s_initialCapacity);

        // 初期プール生成
        for (int i = 0; i < s_initialCapacity; i++)
        {
            s_pool.Push(new T());
        }
    }

    /// <summary>
    /// プールからインスタンスを取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Rent()
    {
        lock (s_lock)
        {
            if (s_pool.Count > 0)
            {
                return s_pool.Pop();
            }
        }
        // プールが空の場合は新規作成
        return new T();
    }

    /// <summary>
    /// インスタンスをプールに返却
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T instance)
    {
        instance.ResetToDefault();
        lock (s_lock)
        {
            s_pool.Push(instance);
        }
    }

    /// <summary>
    /// プールを事前に指定数まで拡張
    /// </summary>
    public static void Prewarm(int count)
    {
        lock (s_lock)
        {
            int toCreate = count - s_pool.Count;
            for (int i = 0; i < toCreate; i++)
            {
                s_pool.Push(new T());
            }
        }
    }

    /// <summary>
    /// 現在のプールサイズ
    /// </summary>
    public static int PooledCount
    {
        get
        {
            lock (s_lock) return s_pool.Count;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// tick制のクラフトを管理するクラス。
/// キューにジョブを追加し、Tick()を呼ぶたびに進行する。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class TickBasedCrafter<TItem>
    where TItem : class, IInventoryItem
{
    private readonly CraftingManager<TItem> _craftingManager;
    private readonly Queue<CraftingJob> _jobQueue = new();
    private CraftingJob? _currentJob;
    private int _currentProgress;

    /// <summary>現在処理中のジョブ</summary>
    public CraftingJob? CurrentJob => _currentJob;

    /// <summary>現在の進行状況（tick数）</summary>
    public int CurrentProgress => _currentProgress;

    /// <summary>現在のジョブの完了率（0.0〜1.0）</summary>
    public float CurrentProgressRatio => _currentJob != null && _currentJob.Recipe.CraftingTicks > 0
        ? (float)_currentProgress / _currentJob.Recipe.CraftingTicks
        : 0f;

    /// <summary>キューに入っているジョブ数</summary>
    public int QueuedJobCount => _jobQueue.Count;

    /// <summary>処理中かどうか</summary>
    public bool IsBusy => _currentJob != null;

    /// <summary>ジョブ開始時に発火するイベント</summary>
    public event Action<CraftingJobStartedEvent<TItem>>? OnJobStarted;

    /// <summary>ジョブ進行時に発火するイベント</summary>
    public event Action<CraftingJobProgressEvent<TItem>>? OnJobProgress;

    /// <summary>ジョブ完了時に発火するイベント</summary>
    public event Action<CraftingJobCompletedEvent<TItem>>? OnJobCompleted;

    /// <summary>ジョブ失敗時に発火するイベント</summary>
    public event Action<CraftingJobFailedEvent<TItem>>? OnJobFailed;

    /// <summary>
    /// TickBasedCrafterを作成する。
    /// </summary>
    /// <param name="craftingManager">クラフトマネージャー</param>
    public TickBasedCrafter(CraftingManager<TItem> craftingManager)
    {
        _craftingManager = craftingManager;
    }

    /// <summary>
    /// 指定したレシピをキューに追加可能かどうかを確認する。
    /// 材料が十分にあるかをチェックする。
    /// </summary>
    /// <param name="recipe">レシピ</param>
    /// <param name="sourceInventory">材料を取得するインベントリ</param>
    /// <param name="count">クラフト回数</param>
    /// <returns>クラフト可能ならtrue</returns>
    public bool CanEnqueue(ICraftingRecipe recipe, IInventory<TItem> sourceInventory, int count = 1)
    {
        return _craftingManager.CanCraft(recipe, sourceInventory, count);
    }

    /// <summary>
    /// 指定したレシピをキューに追加可能かどうかを確認し、不足材料を取得する。
    /// </summary>
    /// <param name="recipe">レシピ</param>
    /// <param name="sourceInventory">材料を取得するインベントリ</param>
    /// <param name="count">クラフト回数</param>
    /// <returns>不足している材料のリスト（空なら追加可能）</returns>
    public IReadOnlyList<CraftingIngredient> CheckEnqueueIngredients(
        ICraftingRecipe recipe,
        IInventory<TItem> sourceInventory,
        int count = 1)
    {
        return _craftingManager.CheckIngredients(recipe, sourceInventory, count);
    }

    /// <summary>
    /// クラフトジョブをキューに追加する。
    /// </summary>
    /// <param name="recipe">レシピ</param>
    /// <param name="sourceInventory">材料を取得するインベントリ</param>
    /// <param name="outputInventory">結果を出力するインベントリ</param>
    /// <param name="count">クラフト回数</param>
    /// <returns>追加されたジョブ</returns>
    public CraftingJob Enqueue(
        ICraftingRecipe recipe,
        IInventory<TItem> sourceInventory,
        IInventory<TItem>? outputInventory = null,
        int count = 1)
    {
        var job = new CraftingJob(
            CraftingJobId.Generate(),
            recipe,
            sourceInventory,
            outputInventory ?? sourceInventory,
            count);

        _jobQueue.Enqueue(job);
        return job;
    }

    /// <summary>
    /// 複数回のクラフトを個別のジョブとしてキューに追加する。
    /// </summary>
    public IReadOnlyList<CraftingJob> EnqueueMultiple(
        ICraftingRecipe recipe,
        IInventory<TItem> sourceInventory,
        IInventory<TItem>? outputInventory = null,
        int count = 1)
    {
        var jobs = new List<CraftingJob>();
        for (int i = 0; i < count; i++)
        {
            jobs.Add(Enqueue(recipe, sourceInventory, outputInventory, 1));
        }
        return jobs;
    }

    /// <summary>
    /// 1 tickを処理する。
    /// </summary>
    /// <returns>このtickで完了したジョブがあればtrue</returns>
    public bool Tick()
    {
        // 現在のジョブがなければ次を取得
        if (_currentJob == null)
        {
            if (_jobQueue.Count == 0)
            {
                return false;
            }

            _currentJob = _jobQueue.Dequeue();
            _currentProgress = 0;

            // 材料チェック
            var source = (IInventory<TItem>)_currentJob.SourceInventory;
            if (!_craftingManager.CanCraft(_currentJob.Recipe, source, _currentJob.Count))
            {
                var missing = _craftingManager.CheckIngredients(_currentJob.Recipe, source, _currentJob.Count);
                OnJobFailed?.Invoke(new CraftingJobFailedEvent<TItem>(
                    _currentJob,
                    CraftingResult.InsufficientMaterials(missing)));
                _currentJob = null;
                return Tick(); // 次のジョブを試す
            }

            OnJobStarted?.Invoke(new CraftingJobStartedEvent<TItem>(_currentJob));

            // 即時クラフト（CraftingTicks == 0）の場合
            if (_currentJob.Recipe.CraftingTicks == 0)
            {
                return CompleteCurrentJob();
            }
        }

        // 進行
        _currentProgress++;

        OnJobProgress?.Invoke(new CraftingJobProgressEvent<TItem>(
            _currentJob,
            _currentProgress,
            _currentJob.Recipe.CraftingTicks));

        // 完了チェック
        if (_currentProgress >= _currentJob.Recipe.CraftingTicks)
        {
            return CompleteCurrentJob();
        }

        return false;
    }

    /// <summary>
    /// 指定したtick数だけ処理を進める。
    /// </summary>
    /// <param name="ticks">処理するtick数</param>
    /// <returns>完了したジョブ数</returns>
    public int TickMultiple(int ticks)
    {
        int completedCount = 0;
        for (int i = 0; i < ticks; i++)
        {
            if (Tick())
            {
                completedCount++;
            }
        }
        return completedCount;
    }

    /// <summary>
    /// 現在のジョブをキャンセルする。
    /// </summary>
    /// <returns>キャンセルされたジョブ、またはnull</returns>
    public CraftingJob? CancelCurrent()
    {
        if (_currentJob == null)
        {
            return null;
        }

        var cancelled = _currentJob;
        OnJobFailed?.Invoke(new CraftingJobFailedEvent<TItem>(
            cancelled,
            CraftingResult.Failed(CraftingFailureReason.Cancelled, "Job cancelled")));

        _currentJob = null;
        _currentProgress = 0;
        return cancelled;
    }

    /// <summary>
    /// キュー内のすべてのジョブをクリアする。
    /// </summary>
    /// <returns>クリアされたジョブ数</returns>
    public int ClearQueue()
    {
        int count = _jobQueue.Count;
        _jobQueue.Clear();
        return count;
    }

    /// <summary>
    /// すべてのジョブ（処理中含む）をクリアする。
    /// </summary>
    public void ClearAll()
    {
        CancelCurrent();
        ClearQueue();
    }

    /// <summary>
    /// キュー内のジョブを取得する（読み取り専用）。
    /// </summary>
    public IEnumerable<CraftingJob> GetQueuedJobs()
    {
        return _jobQueue;
    }

    private bool CompleteCurrentJob()
    {
        if (_currentJob == null) return false;

        var source = (IInventory<TItem>)_currentJob.SourceInventory;
        var output = (IInventory<TItem>)_currentJob.OutputInventory;

        var result = _craftingManager.TryCraft(_currentJob.Recipe, source, output, _currentJob.Count);

        if (result.Success)
        {
            OnJobCompleted?.Invoke(new CraftingJobCompletedEvent<TItem>(_currentJob, result));
        }
        else
        {
            OnJobFailed?.Invoke(new CraftingJobFailedEvent<TItem>(_currentJob, result));
        }

        _currentJob = null;
        _currentProgress = 0;
        return result.Success;
    }
}

/// <summary>
/// クラフトジョブのID。
/// </summary>
public readonly struct CraftingJobId : IEquatable<CraftingJobId>
{
    private static long _nextId;

    public readonly long Value;

    public CraftingJobId(long value) => Value = value;

    public static CraftingJobId Generate() =>
        new(System.Threading.Interlocked.Increment(ref _nextId));

    public bool Equals(CraftingJobId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CraftingJobId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(CraftingJobId left, CraftingJobId right) => left.Equals(right);
    public static bool operator !=(CraftingJobId left, CraftingJobId right) => !left.Equals(right);

    public override string ToString() => $"JobId({Value})";
}

/// <summary>
/// クラフトジョブ。
/// </summary>
public sealed class CraftingJob
{
    /// <summary>ジョブID</summary>
    public CraftingJobId Id { get; }

    /// <summary>レシピ</summary>
    public ICraftingRecipe Recipe { get; }

    /// <summary>材料を取得するインベントリ</summary>
    public object SourceInventory { get; }

    /// <summary>結果を出力するインベントリ</summary>
    public object OutputInventory { get; }

    /// <summary>クラフト回数</summary>
    public int Count { get; }

    /// <summary>作成日時</summary>
    public DateTime CreatedAt { get; }

    public CraftingJob(
        CraftingJobId id,
        ICraftingRecipe recipe,
        object sourceInventory,
        object outputInventory,
        int count)
    {
        Id = id;
        Recipe = recipe;
        SourceInventory = sourceInventory;
        OutputInventory = outputInventory;
        Count = count;
        CreatedAt = DateTime.UtcNow;
    }

    public override string ToString() => $"Job({Id}, {Recipe.Name} x{Count})";
}

/// <summary>
/// ジョブ開始イベント。
/// </summary>
public readonly struct CraftingJobStartedEvent<TItem>
    where TItem : class, IInventoryItem
{
    public readonly CraftingJob Job;

    public CraftingJobStartedEvent(CraftingJob job) => Job = job;
}

/// <summary>
/// ジョブ進行イベント。
/// </summary>
public readonly struct CraftingJobProgressEvent<TItem>
    where TItem : class, IInventoryItem
{
    public readonly CraftingJob Job;
    public readonly int CurrentTick;
    public readonly int TotalTicks;
    public float Progress => TotalTicks > 0 ? (float)CurrentTick / TotalTicks : 1f;

    public CraftingJobProgressEvent(CraftingJob job, int currentTick, int totalTicks)
    {
        Job = job;
        CurrentTick = currentTick;
        TotalTicks = totalTicks;
    }
}

/// <summary>
/// ジョブ完了イベント。
/// </summary>
public readonly struct CraftingJobCompletedEvent<TItem>
    where TItem : class, IInventoryItem
{
    public readonly CraftingJob Job;
    public readonly CraftingResult Result;

    public CraftingJobCompletedEvent(CraftingJob job, CraftingResult result)
    {
        Job = job;
        Result = result;
    }
}

/// <summary>
/// ジョブ失敗イベント。
/// </summary>
public readonly struct CraftingJobFailedEvent<TItem>
    where TItem : class, IInventoryItem
{
    public readonly CraftingJob Job;
    public readonly CraftingResult Result;

    public CraftingJobFailedEvent(CraftingJob job, CraftingResult result)
    {
        Job = job;
        Result = result;
    }
}

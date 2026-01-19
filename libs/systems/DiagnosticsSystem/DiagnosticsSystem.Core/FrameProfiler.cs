using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomato.DiagnosticsSystem;

/// <summary>
/// フレーム単位のプロファイラ。
/// </summary>
public sealed class FrameProfiler
{
    private readonly Dictionary<string, PhaseTimer> _timers = new();
    private readonly CircularBuffer<FrameReport> _history;
    private readonly List<string> _phaseOrder = new();

    public FrameProfiler(int historySize = 300)
    {
        _history = new CircularBuffer<FrameReport>(historySize);
    }

    /// <summary>履歴サイズ</summary>
    public int HistorySize => _history.Capacity;

    /// <summary>記録されたフレーム数</summary>
    public int RecordedFrameCount => _history.Count;

    /// <summary>計測を開始し、Disposeで停止するスコープを返す</summary>
    public IDisposable Measure(string phaseName)
    {
        if (!_timers.TryGetValue(phaseName, out var timer))
        {
            timer = new PhaseTimer(phaseName);
            _timers[phaseName] = timer;
            _phaseOrder.Add(phaseName);
        }

        return timer.Start();
    }

    /// <summary>フレーム終了時に呼び出し</summary>
    public void EndFrame(int frameNumber)
    {
        var timings = new List<PhaseTiming>(_timers.Count);

        foreach (var phaseName in _phaseOrder)
        {
            if (_timers.TryGetValue(phaseName, out var timer))
            {
                timings.Add(timer.GetAndReset());
            }
        }

        var report = new FrameReport(frameNumber, timings);
        _history.Add(report);
    }

    /// <summary>診断レポートを生成</summary>
    public DiagnosticsReport GenerateReport()
    {
        var reports = _history.ToArray();
        if (reports.Length == 0)
        {
            return new DiagnosticsReport
            {
                FrameCount = 0,
                AverageFrameTimeMs = 0,
                MaxFrameTimeMs = 0,
                MinFrameTimeMs = 0,
            };
        }

        var frameTimes = reports.Select(r => r.TotalTimeMs).ToList();

        // フェーズ別統計
        var phaseAverages = new Dictionary<string, double>();
        var phaseMax = new Dictionary<string, double>();

        foreach (var phaseName in _phaseOrder)
        {
            var phaseTimes = reports
                .SelectMany(r => r.PhaseTimings)
                .Where(t => t.PhaseName == phaseName)
                .Select(t => t.ElapsedMs)
                .ToList();

            if (phaseTimes.Count > 0)
            {
                phaseAverages[phaseName] = phaseTimes.Average();
                phaseMax[phaseName] = phaseTimes.Max();
            }
        }

        return new DiagnosticsReport
        {
            FrameCount = reports.Length,
            AverageFrameTimeMs = frameTimes.Average(),
            MaxFrameTimeMs = frameTimes.Max(),
            MinFrameTimeMs = frameTimes.Min(),
            PhaseAveragesMs = phaseAverages,
            PhaseMaxMs = phaseMax,
        };
    }

    /// <summary>直近のフレームレポートを取得</summary>
    public FrameReport? GetLatestReport()
    {
        if (_history.Count == 0) return null;
        return _history[_history.Count - 1];
    }

    /// <summary>履歴をクリア</summary>
    public void Clear()
    {
        _history.Clear();
        foreach (var timer in _timers.Values)
        {
            timer.GetAndReset(); // 累積をリセット
        }
    }

    /// <summary>登録されたフェーズ名一覧</summary>
    public IReadOnlyList<string> PhaseNames => _phaseOrder;
}

using System;
using System.Diagnostics;

namespace Tomato.DiagnosticsSystem;

/// <summary>
/// フェーズ計測タイマー。
/// </summary>
public sealed class PhaseTimer
{
    private readonly string _phaseName;
    private readonly Stopwatch _stopwatch = new();
    private double _accumulated;

    public PhaseTimer(string phaseName)
    {
        _phaseName = phaseName;
    }

    /// <summary>フェーズ名</summary>
    public string PhaseName => _phaseName;

    /// <summary>計測を開始し、Disposeで停止するスコープを返す</summary>
    public IDisposable Start()
    {
        _stopwatch.Restart();
        return new TimerScope(this);
    }

    internal void Stop()
    {
        _stopwatch.Stop();
        _accumulated += _stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>計測結果を取得し、累積をリセット</summary>
    public PhaseTiming GetAndReset()
    {
        var timing = new PhaseTiming(_phaseName, _accumulated);
        _accumulated = 0;
        return timing;
    }

    /// <summary>現在の累積時間（ミリ秒）</summary>
    public double AccumulatedMs => _accumulated;

    private sealed class TimerScope : IDisposable
    {
        private readonly PhaseTimer _timer;
        private bool _disposed;

        public TimerScope(PhaseTimer timer)
        {
            _timer = timer;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer.Stop();
                _disposed = true;
            }
        }
    }
}

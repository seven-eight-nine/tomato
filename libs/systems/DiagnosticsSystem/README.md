# DiagnosticsSystem

フレーム単位のパフォーマンス計測システム。ゲームループの各フェーズの処理時間を計測し、統計情報を生成する。

## 概要

- **フェーズ計測** - 各処理フェーズの時間を計測
- **履歴管理** - 過去N フレームの履歴を保持
- **統計生成** - 平均/最大/最小時間のレポート
- **低オーバーヘッド** - Stopwatch ベースの軽量計測

## 使用例

### 基本的な使用法

```csharp
var profiler = new FrameProfiler(historySize: 300);

// 毎フレーム
void Update(int frameNumber)
{
    using (profiler.Measure("CollisionPhase"))
    {
        ProcessCollisions();
    }

    using (profiler.Measure("MessagePhase"))
    {
        ProcessMessages();
    }

    using (profiler.Measure("RenderPhase"))
    {
        Render();
    }

    profiler.EndFrame(frameNumber);
}
```

### レポート生成

```csharp
// 定期的にレポートを出力
if (frameNumber % 300 == 0)
{
    var report = profiler.GenerateReport();
    Console.WriteLine(report);
}

// 出力例:
// === Diagnostics Report (300 frames) ===
// Frame Time: avg=16.234ms, max=22.451ms, min=14.123ms
// Phase Breakdown:
//   CollisionPhase: avg=2.345ms, max=5.678ms
//   MessagePhase: avg=1.234ms, max=3.456ms
//   RenderPhase: avg=12.655ms, max=18.765ms
```

### GameLoopOrchestratorとの統合

```csharp
public void ProcessFrame(float deltaTime)
{
    using (_profiler.Measure("CollisionPhase"))
    {
        ProcessCollisionPhase();
    }

    using (_profiler.Measure("MessagePhase"))
    {
        ProcessMessagePhase();
    }

    // ... 他のフェーズ

    _profiler.EndFrame(_frameContext.FrameNumber);
}
```

## API

### FrameProfiler

| メソッド | 説明 |
|---------|------|
| `Measure(string phaseName)` | 計測開始。Dispose で停止 |
| `EndFrame(int frameNumber)` | フレーム終了を記録 |
| `GenerateReport()` | 統計レポートを生成 |
| `GetLatestReport()` | 直近のフレームレポートを取得 |
| `Clear()` | 履歴をクリア |

### DiagnosticsReport

| プロパティ | 説明 |
|-----------|------|
| `FrameCount` | 計測フレーム数 |
| `AverageFrameTimeMs` | 平均フレーム時間 |
| `MaxFrameTimeMs` | 最大フレーム時間 |
| `MinFrameTimeMs` | 最小フレーム時間 |
| `PhaseAveragesMs` | フェーズ別平均時間 |
| `PhaseMaxMs` | フェーズ別最大時間 |

## テスト

```bash
dotnet test libs/DiagnosticsSystem/DiagnosticsSystem.Tests/
```

## ディレクトリ構造

```
DiagnosticsSystem/
├── DiagnosticsSystem.Core/
│   ├── DiagnosticsSystem.Core.csproj
│   ├── FrameProfiler.cs
│   ├── PhaseTimer.cs
│   ├── PhaseTiming.cs
│   ├── FrameReport.cs
│   ├── DiagnosticsReport.cs
│   └── CircularBuffer.cs
├── DiagnosticsSystem.Tests/
│   ├── DiagnosticsSystem.Tests.csproj
│   ├── FrameProfilerTests.cs
│   ├── PhaseTimerTests.cs
│   └── CircularBufferTests.cs
└── README.md
```

## ライセンス

MIT License

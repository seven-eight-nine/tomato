# SchedulerSystem

フレームベースのタスクスケジューリングとクールダウン管理システム。

## 概要

- **遅延実行** - 指定フレーム後にタスクを実行
- **定期実行** - 指定間隔でタスクを繰り返し実行
- **クールダウン** - アクションの使用間隔を管理
- **Entity別クールダウン** - Entity単位でのクールダウン管理

## 使用例

### 遅延実行

```csharp
var scheduler = new FrameScheduler();

// 30フレーム後に爆発
scheduler.Schedule(30, () => SpawnExplosion(position));

// キャンセル可能
var handle = scheduler.Schedule(60, () => DoSomething());
handle.Cancel();

// 毎フレーム呼び出し
void Update()
{
    scheduler.Update();
}
```

### 定期実行

```csharp
// 300フレームごとに敵をスポーン（無限）
scheduler.ScheduleRepeating(300, () => SpawnEnemy());

// 最大10回まで
scheduler.ScheduleRepeating(60, () => HealPlayer(10), maxRepetitions: 10);
```

### クールダウン管理

```csharp
var cooldownManager = new CooldownManager();

void UseSkill()
{
    if (cooldownManager.IsOnCooldown("fireball"))
    {
        var remaining = cooldownManager.GetRemainingFrames("fireball");
        ShowCooldownUI(remaining);
        return;
    }

    CastFireball();
    cooldownManager.StartCooldown("fireball", durationFrames: 300);
}

void Update()
{
    cooldownManager.Update();
}
```

### Entity別クールダウン

```csharp
var entityCooldowns = new EntityCooldownManager();

void EntityUseSkill(AnyHandle entity, string skillId)
{
    if (entityCooldowns.IsOnCooldown(entity, skillId))
    {
        return; // クールダウン中
    }

    PerformSkill(entity, skillId);
    entityCooldowns.StartCooldown(entity, skillId, durationFrames: 180);
}

// Entity削除時
void OnEntityRemoved(AnyHandle entity)
{
    entityCooldowns.OnEntityRemoved(entity);
}
```

## API

### FrameScheduler

| メソッド | 説明 |
|---------|------|
| `Schedule(int delay, Action action)` | 遅延実行をスケジュール |
| `ScheduleRepeating(int interval, Action action, int maxRepetitions = -1)` | 定期実行をスケジュール |
| `Cancel(int taskId)` | タスクをキャンセル |
| `Update()` | 毎フレーム呼び出し |
| `Clear()` | 全タスクをクリア |

### CooldownManager

| メソッド | 説明 |
|---------|------|
| `StartCooldown(string key, int durationFrames)` | クールダウン開始 |
| `IsOnCooldown(string key)` | クールダウン中か確認 |
| `GetRemainingFrames(string key)` | 残りフレーム数を取得 |
| `Reset(string key)` | クールダウンをリセット |
| `Update()` | 毎フレーム呼び出し |

### EntityCooldownManager

| メソッド | 説明 |
|---------|------|
| `StartCooldown(AnyHandle entity, string actionId, int durationFrames)` | Entity別クールダウン開始 |
| `IsOnCooldown(AnyHandle entity, string actionId)` | クールダウン中か確認 |
| `OnEntityRemoved(AnyHandle entity)` | Entity削除時のクリーンアップ |

## 依存関係

- EntityHandleSystem.Attributes (AnyHandle - EntityCooldownManagerのみ)

## テスト

```bash
dotnet test libs/SchedulerSystem/SchedulerSystem.Tests/
```

## ディレクトリ構造

```
SchedulerSystem/
├── SchedulerSystem.Core/
│   ├── SchedulerSystem.Core.csproj
│   ├── FrameScheduler.cs
│   ├── ScheduledTask.cs
│   ├── RepeatingTask.cs
│   ├── TaskHandle.cs
│   └── Cooldown/
│       ├── CooldownManager.cs
│       └── EntityCooldownManager.cs
├── SchedulerSystem.Tests/
│   ├── SchedulerSystem.Tests.csproj
│   ├── FrameSchedulerTests.cs
│   └── CooldownManagerTests.cs
└── README.md
```

## ライセンス

MIT License

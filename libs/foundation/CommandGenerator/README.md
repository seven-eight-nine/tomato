# CommandGenerator - コマンドパターン・メッセージシステム

C# Source Generatorを使用して、コマンドパターンの実装を自動生成するライブラリです。優先度ベースの実行とオブジェクトプーリングを備えています。

## ドキュメント

- **[テストコード](./CommandGenerator.Tests/)** - 実践的なサンプル

## 概要

CommandGeneratorは、コマンドパターンの実装を自動化し、ゲーム向けのメッセージシステム機能も提供します。

### なぜCommandGeneratorを使うのか？

- **手動実装不要** - コマンドパターンのboilerplateコード不要
- **優先度制御** - コマンドの実行順序を細かく制御
- **メモリ効率** - オブジェクトプーリングによる最適化
- **型安全** - コンパイル時の型チェック
- **メッセージシステム** - Entity間通信のためのWave型メッセージ処理

## 特徴

- **自動コード生成** - キュー管理コードを自動生成
- **優先度ベース実行** - コマンドを優先度順に実行
- **オブジェクトプーリング** - GC負荷を最小化
- **複数キュー対応** - 1つのコマンドを複数のキューに登録可能
- **型安全** - ジェネリクスによる型安全性
- **メッセージ配送** - AnyHandleベースのEntity間メッセージング
- **Wave処理** - 決定論的なメッセージ処理

## クイックスタート

### 1. コマンドキューの定義

```csharp
using CommandGenerator;

[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void Execute();

    [CommandMethod(clear: false)]
    public partial void ExecuteAndKeep();
}
```

### 2. コマンドの定義

```csharp
[Command<GameCommandQueue>(Priority = 10)]
public partial class MoveCommand
{
    public int X;
    public int Y;

    public void Execute()
    {
        Console.WriteLine($"移動: ({X}, {Y})");
    }
}

[Command<GameCommandQueue>(Priority = 5)]
public partial class AttackCommand
{
    public int Damage;

    public void Execute()
    {
        Console.WriteLine($"攻撃: {Damage}ダメージ");
    }
}
```

### 3. コマンドの使用

```csharp
// キューを作成
var queue = new GameCommandQueue();

// コマンドをエンキュー（インスタンスメソッド）
queue.Enqueue<MoveCommand>(cmd => {
    cmd.X = 10;
    cmd.Y = 20;
});

queue.Enqueue<AttackCommand>(cmd => {
    cmd.Damage = 50;
});

// 実行（優先度順: Move → Attack）
queue.Execute();
```

## 詳細ガイド

### 優先度の制御

```csharp
// 高優先度（緊急処理）
[Command<GameCommandQueue>(Priority = 100)]
public partial class CriticalErrorCommand { /* ... */ }

// 通常優先度
[Command<GameCommandQueue>(Priority = 0)]
public partial class NormalCommand { /* ... */ }

// 低優先度（後回し可能）
[Command<GameCommandQueue>(Priority = -10)]
public partial class LogCommand { /* ... */ }

// 実行順序: Critical → Normal → Log
```

### 複数キューへの登録

```csharp
// ゲームロジックキュー
[CommandQueue]
public partial class GameLogicQueue
{
    [CommandMethod]
    public partial void Execute();
}

// UIキュー
[CommandQueue]
public partial class UIQueue
{
    [CommandMethod]
    public partial void Execute();
}

// 両方のキューに登録
[Command<GameLogicQueue>(Priority = 10)]
[Command<UIQueue>(Priority = 5)]
public partial class LogCommand
{
    public string Message;
    public void Execute() => Console.WriteLine(Message);
}

// 使用例
var gameQueue = new GameLogicQueue();
var uiQueue = new UIQueue();
gameQueue.Enqueue<LogCommand>(cmd => cmd.Message = "ゲームログ");
uiQueue.Enqueue<LogCommand>(cmd => cmd.Message = "UIログ");
```

### プーリングの最適化

```csharp
// 高頻度で使用されるコマンドのプール容量を増やす
[Command<GameCommandQueue>(
    Priority = 0,
    PoolInitialCapacity = 64  // デフォルトは8
)]
public partial class HighFrequencyCommand
{
    public void Execute() { /* ... */ }
}
```

### Clearオプション

```csharp
[CommandQueue]
public partial class ReplayQueue
{
    // 実行後にクリア（通常のコマンド実行）
    [CommandMethod(clear: true)]
    public partial void Execute();

    // 実行後もキューを保持（リプレイ機能など）
    [CommandMethod(clear: false)]
    public partial void Replay();
}

// 使用例
replayQueue.Replay();  // 1回目
replayQueue.Replay();  // 同じコマンドを再実行
```

### シグナルコマンド

シグナルコマンドは、キューに1つしか存在できないコマンドです。複数回Enqueueしても、最初の1つだけがキューに入り、2回目以降は無視されます。

```csharp
// シグナルコマンドの定義
[Command<GameCommandQueue>(Signal = true)]
public partial class ReconcileCommand
{
    public void Execute()
    {
        // イベントを集計・調停する処理
        // このコマンドは1回だけ実行される
    }
}

// 使用例
var queue = new GameCommandQueue();

// 複数回Enqueueしても1つしかキューに入らない
queue.Enqueue<ReconcileCommand>(_ => { }); // true（成功）
queue.Enqueue<ReconcileCommand>(_ => { }); // false（無視）
queue.Enqueue<ReconcileCommand>(_ => { }); // false（無視）

queue.Execute(); // ReconcileCommandは1回だけ実行される

// 実行後はクリアされるので、再度Enqueue可能
queue.Enqueue<ReconcileCommand>(_ => { }); // true（成功）
```

シグナルコマンドの主な用途：
- **イベント調停**: 複数のイベントを受け取った時に、まとめて1回だけ処理したい場合
- **重複抑制**: 同じ処理リクエストが短時間に複数来た時に、1回だけ実行したい場合
- **状態同期**: フレーム終了時に1回だけ状態を同期したい場合

## 応用例

### Undo/Redo システム

```csharp
[Command<GameCommandQueue>]
public partial class MovePlayerCommand
{
    public Vector3 From;
    public Vector3 To;
    private Player player;

    public void Execute()
    {
        player.Position = To;
        // Undoスタックに逆コマンドを追加
        UndoStack.Push(new MovePlayerCommand { From = To, To = From });
    }
}

// Undo実行
void Undo()
{
    if (UndoStack.TryPop(out var command))
    {
        GameCommandQueue.Enqueue(command);
        queue.Execute();
    }
}
```

### イベント駆動型ゲームロジック

```csharp
[CommandQueue]
public partial class EventQueue
{
    [CommandMethod]
    public partial void ProcessEvents();
}

[Command<EventQueue>(Priority = 100)]
public partial class PlayerDiedEvent
{
    public EventQueue Queue; // キューへの参照を保持

    public void Execute()
    {
        // ゲームオーバー処理（同じキューに追加）
        Queue.Enqueue<ShowGameOverCommand>(cmd => { });
        Queue.Enqueue<SaveScoreCommand>(cmd => { });
    }
}

// 使用例
var eventQueue = new EventQueue();
eventQueue.Enqueue<PlayerDiedEvent>(cmd => cmd.Queue = eventQueue);

void Update()
{
    // 毎フレームイベントを処理
    eventQueue.ProcessEvents();
}
```

### ネットワークコマンド

```csharp
[CommandQueue]
public partial class NetworkCommandQueue
{
    [CommandMethod]
    public partial void ProcessCommands();
}

[Command<NetworkCommandQueue>]
public partial class ServerUpdateCommand
{
    public byte[] Data;

    public void Execute()
    {
        // サーバーからの更新を適用
        ApplyServerUpdate(Data);
    }
}

// 使用例
var networkQueue = new NetworkCommandQueue();

void OnReceivePacket(byte[] data)
{
    networkQueue.Enqueue<ServerUpdateCommand>(cmd => cmd.Data = data);
}
```

## API リファレンス

### Command属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `Priority` | 実行優先度（大きいほど先に実行） | 0 |
| `PoolInitialCapacity` | オブジェクトプールの初期容量 | 8 |
| `Signal` | シグナルコマンド（キューに1つしか入らない） | false |

### CommandMethod属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `clear` | 実行後にキューをクリアするか | true |

### 生成されるメソッド

| メソッド | 説明 |
|---------|------|
| `Queue.Enqueue<T>(Action<T>)` | コマンドをキューに追加。戻り値はbool（Signalコマンドが重複した場合false） |
| `Queue.Execute()` | キューのコマンドを実行 |
| `Queue.Clear()` | Pending/NextFrameキューをクリア |
| `Queue.ForceClear()` | 全キューを強制クリア |

## ベストプラクティス

### 推奨

```csharp
// ✅ 優先度を適切に設定
[Command<GameCommandQueue>(Priority = 100)]  // 緊急
public partial class CriticalCommand { }

// ✅ プール容量を適切に設定
[Command<GameCommandQueue>(PoolInitialCapacity = 64)]
public partial class FrequentCommand { }

// ✅ Executeメソッドはシンプルに
public void Execute()
{
    DoOneThingWell();  // 単一責任
}
```

### 避けるべき

```csharp
// ❌ Executeで長時間処理
public void Execute()
{
    Thread.Sleep(1000);  // NG: ブロッキング
}

// ❌ Executeで例外を投げっぱなし
public void Execute()
{
    throw new Exception();  // NG: 適切に処理する
}
```

## Entity単位のキュー管理

EntityHandleSystemの`[HasCommandQueue]`属性と連携して、各Entityインスタンスが独自のCommandQueueを持つように設計できます。

```csharp
using EntityHandleSystem;
using CommandGenerator;

// CommandQueue定義
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// Entity定義（各Entityが独自のキューを持つ）
[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]
public partial class Player
{
    public int Health;
}

// 使用例
var arena = new PlayerArena();
var handle = arena.Create();

// プロパティでキューにアクセス
handle.GameCommandQueue.Enqueue<DamageCommand>(cmd => {
    cmd.Amount = 50;
});
```

詳細は[EntityHandleSystem README](../EntityHandleSystem/README.md)を参照。

## プロジェクト構成

```
CommandGenerator/
├── CommandGenerator.Attributes/  # 属性・基本型（netstandard2.0）
├── CommandGenerator.Generator/   # Source Generator
├── CommandGenerator.Core/        # メッセージシステム（net8.0）
└── CommandGenerator.Tests/       # テスト
```

## さらに詳しく

- **[テストコード](./CommandGenerator.Tests/)** - 実践的なサンプル

## ライセンス

MIT License

# UnitLODSystem 設計書

ユニットベースのLODライフサイクル管理ライブラリの詳細設計ドキュメント。

namespace: `Tomato.UnitLODSystem`

---

## 目次

1. [コア概念](#コア概念)
2. [UnitPhase](#unitphase)
3. [IUnitDetail](#iunitdetail)
4. [Unit](#unit)
5. [フロー詳細](#フロー詳細)
6. [UnitHelper](#unithelper)
7. [使用例](#使用例)
8. [ディレクトリ構造](#ディレクトリ構造)

---

## コア概念

### 目標レベルと詳細レベルの関係

```
目標レベル (int)        必要なインスタンス
─────────────────────────────────────────
0                    → []
1                    → [DataA, DataB]      ← requiredAt=1
2                    → [DataA, DataB, ModelC]   ← requiredAt=1,2
```

- 目標レベルは`int`（enumから変換可能）
- 各詳細レベルは「何番以上で必要か」を`requiredAt`で登録
- 同じ`requiredAt`の詳細レベルは**グループ**として扱う

### グループ処理

同じ`requiredAt`の詳細レベルは以下のように処理される：

| 処理 | 動作 |
|:-----|:-----|
| 生成 | まとめて`new()` |
| ロード | 並行して`Loading` |
| ロード完了待ち | 全員が`Loaded`になるまで待機 |
| Creating → Ready | グループ内で順番に |
| アンロード | まとめて`OnChangePhase()` |

---

## UnitPhase

詳細レベルのライフサイクルを表すenum。

```csharp
public enum UnitPhase
{
    None = 0,       // new直後
    Loading = 1,    // ロード中
    Loaded = 2,     // ロード完了
    Creating = 3,   // 生成中
    Ready = 4,      // 安定状態（取得可能）
    Unloading = 5,  // アンロード中
    Unloaded = 6,   // アンロード完了（Dispose可能）
}
```

### フェーズ遷移図

```
前進フロー:
None → Loading → Loaded → Creating → Ready

後退フロー:
Ready → Unloading → Unloaded → Dispose
```

---

## IUnitDetail<TOwner>

詳細レベルが実装するジェネリックインターフェース。
`TOwner`で所有者のUnit型を指定し、コールバックで所有者にアクセスできる。

```csharp
public interface IUnitDetail<TOwner> : IDisposable where TOwner : Unit<TOwner>
{
    UnitPhase Phase { get; }
    void OnUpdatePhase(TOwner owner, UnitPhase phase);
    void OnChangePhase(TOwner owner, UnitPhase prev, UnitPhase next);
}
```

### メソッド説明

| メソッド | 説明 |
|:---------|:-----|
| `Phase` | 現在のフェーズを返す |
| `OnUpdatePhase(owner, phase)` | 毎tick呼ばれる。非同期処理の進行やフェーズ遷移を行う |
| `OnChangePhase(owner, prev, next)` | フェーズ変更時に呼ばれる。Unitが遷移を開始するとき |
| `Dispose()` | リソース解放。`Unloaded`後にUnitから呼ばれる |

### 実装例

```csharp
// 基本的なUnit使用時
public class DataDetail : IUnitDetail<Unit>
{
    public UnitPhase Phase { get; private set; } = UnitPhase.None;
    private AsyncOperation _loadOp;
    private int _tickCount;

    public void OnUpdatePhase(Unit owner, UnitPhase phase)
    {
        switch (Phase)
        {
            case UnitPhase.Loading:
                if (_loadOp.isDone)
                    Phase = UnitPhase.Loaded;
                break;

            case UnitPhase.Creating:
                InitializeData();
                Phase = UnitPhase.Ready;
                break;

            case UnitPhase.Ready:
                // 安定状態、何もしない
                break;

            case UnitPhase.Unloading:
                if (++_tickCount >= 1)
                {
                    ReleaseData();
                    Phase = UnitPhase.Unloaded;
                }
                break;
        }
    }

    public void OnChangePhase(Unit owner, UnitPhase prev, UnitPhase next)
    {
        Phase = next;
        _tickCount = 0;

        if (next == UnitPhase.Loading)
        {
            _loadOp = StartLoadAsync();
        }
    }

    public void Dispose()
    {
        // 最終クリーンアップ
    }
}
```

---

## Unit<TSelf>

詳細レベルのライフサイクルを管理するメインクラス。
CRTPパターンで継承可能。

```csharp
// ジェネリック版（継承用）
public class Unit<TSelf> where TSelf : Unit<TSelf>
{
    // プロパティ
    public int TargetState { get; }
    public bool IsStable { get; }
    protected TSelf Self { get; }  // コールバック呼び出し用

    // 登録
    public void Register<T>(int requiredAt) where T : class, IUnitDetail<TSelf>, new();

    // 目標設定
    public void RequestState(int targetState);

    // 更新
    public void Tick();

    // 取得（Readyのときのみ）
    public T Get<T>() where T : class, IUnitDetail<TSelf>;

    // イベント
    public event UnitPhaseChangedEventHandler UnitPhaseChanged;
}

// 非ジェネリック版（簡易使用）
public class Unit : Unit<Unit>
{
}
```

### プロパティ

| プロパティ | 説明 |
|:-----------|:-----|
| `TargetState` | 現在の目標レベル |
| `IsStable` | 全詳細レベルがReadyかつ処理中でない場合`true` |

### メソッド

| メソッド | 説明 |
|:---------|:-----|
| `Register<T>(requiredAt)` | 詳細レベル型と必要な目標レベルを登録 |
| `RequestState(target)` | 目標レベルを設定 |
| `Tick()` | 毎フレーム呼び出す。詳細レベルの進行を管理 |
| `Get<T>()` | Ready状態の詳細レベルを取得。Readyでなければ`null` |

### Get<T>() の取得可能タイミング

`Get<T>()`は **`Phase == UnitPhase.Ready` のときのみ** インスタンスを返す。
それ以外のフェーズでは常に`null`を返す。

| フェーズ | Get<T>() | 説明 |
|:---------|:--------:|:-----|
| None | `null` | 生成直後、まだロード開始前 |
| Loading | `null` | ロード中 |
| Loaded | `null` | ロード完了、Creating待ち |
| Creating | `null` | 初期化中 |
| **Ready** | **インスタンス** | **安定状態、取得可能** |
| Unloading | `null` | アンロード開始した瞬間から取得不可 |
| Unloaded | `null` | Dispose待ち（この後削除される） |

#### ロード間際の挙動

```
RequestState(1) 呼び出し
    |
    v
Tick: None -> Loading        Get<T>() = null
Tick: Loading                Get<T>() = null
Tick: Loading -> Loaded      Get<T>() = null
Tick: Loaded -> Creating     Get<T>() = null
Tick: Creating -> Ready      Get<T>() = インスタンス  <-- ここから取得可能
Tick: Ready                  Get<T>() = インスタンス
```

#### アンロード間際の挙動

```
RequestState(0) 呼び出し
    |
    v
Tick: Ready -> Unloading     Get<T>() = null  <-- この瞬間から取得不可
Tick: Unloading              Get<T>() = null
Tick: Unloading -> Unloaded  Get<T>() = null
Tick: (Dispose & 削除)       Get<T>() = null
```

**重要**: アンロード開始（`OnChangePhase(Ready, Unloading)`呼び出し）と同時に`Get<T>()`は`null`を返す。
Unloading中にインスタンスにアクセスする必要がある場合は、`UnitPhaseChanged`イベントを使用する。

```csharp
unit.UnitPhaseChanged += (sender, e) =>
{
    if (e.NewPhase == UnitPhase.Unloading)
    {
        // アンロード開始時の処理
        // この時点ではまだインスタンスは存在するが、Get<T>()では取得できない
    }
};
```

---

## フロー詳細

### 前進フロー（目標が上がる）

グループ単位で順番にロード。前のグループが**Loaded**になってから次のグループのロードを開始。
Creating と次グループの Loading は並行可能。

```
目標: 0 → 2
登録: DataA(1), DataB(1), ModelC(2)

── requiredAt=1 のグループ生成とロード ──────────────────
Tick 1: Idle → Instantiating
        DataA, DataB を new()

Tick 2: Instantiating → Loading
        DataA, DataB が Loading 開始

Tick N: DataA, DataB が Loaded になるまで待つ

── requiredAt=1 が Loaded → requiredAt=2 のロード開始 ────
Tick N+1: ModelC を new() → Loading 開始（並行）
          DataA Loaded → Creating → Ready

Tick N+2: DataB Loaded → Creating → Ready
          (ModelC は Loading 中)

── requiredAt=2 の Creating ────────────────────────
Tick M: ModelC が Loaded になるまで待つ

Tick M+1: ModelC Loaded → Creating → Ready
          → IsStable = true
```

**ポイント**: リソースアロケータがグループごとに分かれている場合を想定し、
前のグループの構築（Loaded）が完了してから次のグループのロードを開始する。

### 後退フロー（目標が下がる）

`requiredAt`降順でアンロード・破棄する。

```
目標: 2 → 0
登録: DataA(1), DataB(1), ModelC(2)

── requiredAt = 2 のグループ ──────────────────
Tick 1: ModelC OnChangePhase(Ready, Unloading)

Tick N: ModelC Unloaded → Dispose

── requiredAt = 1 のグループ ──────────────────
Tick N+1: DataA, DataB OnChangePhase(Ready, Unloading)

Tick M: DataA, DataB Unloaded → Dispose
        → IsStable = true
```

---

## UnitPhaseChangedEventArgs

フェーズ変更イベントの引数。

```csharp
public class UnitPhaseChangedEventArgs : EventArgs
{
    public Type DetailType { get; }
    public UnitPhase OldPhase { get; }
    public UnitPhase NewPhase { get; }
}

public delegate void UnitPhaseChangedEventHandler(
    object sender, UnitPhaseChangedEventArgs e);
```

---

## UnitHelper

フェーズ判定ユーティリティ。

```csharp
public static class UnitHelper
{
    public static bool IsLoading(UnitPhase phase);   // Loading
    public static bool IsStable(UnitPhase phase);    // Ready
    public static bool IsUnloading(UnitPhase phase); // Unloading
    public static bool CanDispose(UnitPhase phase);  // Unloaded
}
```

### 判定マトリクス

| フェーズ | IsLoading | IsStable | IsUnloading | CanDispose |
|:---------|:---------:|:--------:|:-----------:|:----------:|
| None | - | - | - | - |
| Loading | ✓ | - | - | - |
| Loaded | - | - | - | - |
| Creating | - | - | - | - |
| Ready | - | ✓ | - | - |
| Unloading | - | - | ✓ | - |
| Unloaded | - | - | - | ✓ |

---

## 使用例

### 基本的な使用

```csharp
// Unit作成
var unit = new Unit();

// 詳細レベル登録
unit.Register<CharacterDataDetail>(1);
unit.Register<CharacterModelDetail>(2);
unit.Register<CharacterAnimDetail>(2);

// 目標設定
unit.RequestState(2);

// 毎フレーム更新
void Update()
{
    unit.Tick();
}
```

### イベント監視

```csharp
unit.UnitPhaseChanged += (sender, e) =>
{
    Console.WriteLine($"{e.DetailType.Name}: {e.OldPhase} → {e.NewPhase}");

    if (e.NewPhase == UnitPhase.Ready)
    {
        Console.WriteLine($"{e.DetailType.Name} is now available");
    }
};
```

### 詳細レベルの取得

```csharp
void Update()
{
    unit.Tick();

    // Readyのときのみ取得可能
    var model = unit.Get<CharacterModelDetail>();
    if (model != null)
    {
        model.PlayAnimation("idle");
    }

    // または IsStable で全体の安定を確認
    if (unit.IsStable)
    {
        // 全詳細レベルがReady
    }
}
```

### 目標の変更

```csharp
// 目標を下げると不要な詳細レベルが自動破棄される
unit.RequestState(1);
// → CharacterModelDetail, CharacterAnimDetail がアンロード・破棄

// 目標を0にすると全詳細レベルが破棄される
unit.RequestState(0);
// → CharacterDataDetail もアンロード・破棄
```

### enumを目標として使用

```csharp
public enum CharacterLOD
{
    None = 0,
    DataLoaded = 1,
    FullyLoaded = 2,
    Active = 3
}

// enumをintにキャストして使用
unit.RequestState((int)CharacterLOD.FullyLoaded);
```

---

## ディレクトリ構造

```
UnitLODSystem/
├── README.md                           # 概要
├── DESIGN.md                           # 本ドキュメント
│
├── UnitLODSystem.Core/
│   ├── UnitLODSystem.Core.csproj
│   ├── UnitPhase.cs                    # ライフサイクルenum
│   ├── IUnitDetail.cs                  # 詳細レベルインターフェース
│   ├── Unit.cs                         # メインクラス
│   ├── UnitPhaseChangedEventArgs.cs    # イベント引数
│   └── UnitHelper.cs                   # フェーズ判定ヘルパー
│
└── UnitLODSystem.Tests/
    ├── Mocks/
    │   └── MockUnitDetail.cs           # テスト用モック
    ├── Unit/
    │   ├── RegistrationTests.cs
    │   ├── ForwardFlowTests.cs
    │   ├── BackwardFlowTests.cs
    │   ├── GetTests.cs
    │   └── EventTests.cs
    ├── UnitHelper/
    │   └── UnitHelperTests.cs
    └── UnitPhaseTests/
        └── UnitPhaseTests.cs
```

# CombatSystem

攻撃とダメージ処理を管理する戦闘システム。

## 概要

「攻撃が当たった」から「ダメージを与える」までの判定・制御を担当する。
CollisionSystemが「物理的に当たった」を検出し、CombatSystemが「ゲームとしてダメージを与えるべきか」を判定する。

```
CollisionSystem          CombatSystem              ゲーム側
    │                        │                        │
    │  衝突検出              │                        │
    ├───────────────────────▶│                        │
    │  (Hitbox vs Hurtbox)   │                        │
    │                        │  ターゲット判定        │
    │                        │  ├─ 自傷防止           │
    │                        │  ├─ 陣営判定           │
    │                        │  └─ 無敵状態チェック   │
    │                        │                        │
    │                        │  多段ヒット判定        │
    │                        │  ├─ HitGroup判定       │
    │                        │  ├─ インターバル判定   │
    │                        │  └─ 回数制限判定       │
    │                        │                        │
    │                        │  ダメージ適用          │
    │                        ├───────────────────────▶│
    │                        │                        │  HP減少
    │                        │                        │  死亡判定
    │                        │                        │  ヒットストップ
```

## 特徴

- **衝突検出とダメージ処理を分離**: CollisionSystemは幾何学的判定、CombatSystemはゲームロジック
- **多段ヒットの統一制御**: 「同じ技で2回当たらない」「0.5秒間隔で再ヒット」等をフレームワークで保証
- **ゲーム固有ロジックの注入**: ターゲット判定・ダメージ計算をアプリ側で実装

---

## 設計哲学

### 原則1: 被攻撃側が履歴を持つ（Receiver-Owned History）

HitHistoryはIDamageReceiver（被攻撃側）が持つ。攻撃側ではない。

```csharp
// ✓ 正しい設計
public class Character : IDamageReceiver
{
    private readonly HitHistory _hitHistory = new();
    public HitHistory GetHitHistory() => _hitHistory;
}

// ✗ 誤った設計（攻撃側が履歴を持つ）
public class Attack
{
    private readonly HitHistory _hitHistory = new();  // ダメ
}
```

**理由:** 攻撃オブジェクトは一時的。プールに返却されたり、次の攻撃で再利用される。攻撃が消えた瞬間に履歴も消えると、同じHitGroupを持つ後続の攻撃が再ヒットしてしまう。

```
時系列:
  t=0  AttackA(HitGroup=100) がターゲットにヒット → 履歴記録
  t=1  AttackA が終了、プールに返却 → 履歴消失（もし攻撃が持っていたら）
  t=2  AttackB(HitGroup=100) が同じターゲットに当たる → 再ヒット！（バグ）
```

被攻撃側が履歴を持つと、攻撃がいつ消えても履歴は残る。キャラクターがシーンから消えたときだけ履歴も消える。

### 原則2: HitGroupによる攻撃の同一視（Attack Grouping）

同じHitGroupを持つ攻撃は、履歴を共有する。

```
剣の斬撃アニメーション:
┌────────────────────────────────────────┐
│ フレーム 1-5:  ヒット判定A (HitGroup=100) │
│ フレーム 3-8:  ヒット判定B (HitGroup=100) │
│ フレーム 6-10: ヒット判定C (HitGroup=100) │
└────────────────────────────────────────┘

物理衝突: A→B→C と3回発生しうる
ゲーム結果: 最初の1回だけダメージ（HitGroupが同じだから）
```

**理由:** 物理エンジンの衝突検出とゲームロジックのギャップを埋める。物理的には複数回衝突しても、プレイヤーの意図は「1回の斬撃」。

```csharp
// 同じ意図の攻撃には同じHitGroup
var slashA = new MyAttackInfo { HitGroup = 100 };
var slashB = new MyAttackInfo { HitGroup = 100 };
var slashC = new MyAttackInfo { HitGroup = 100 };

// slashAでヒット後、slashB/Cは同じターゲットに当たらない
```

### 原則3: ターゲット判定の委譲（Delegated Targeting）

「誰に当たるか」の判定はアプリ側に委譲する。CombatSystemは判定の仕組みだけを提供する。

```csharp
// 格闘ゲーム: 敵のみ
public override bool CanTarget(IDamageReceiver target)
    => target is Enemy;

// アクションRPG: 陣営 + フレンドリーファイア
public override bool CanTarget(IDamageReceiver target)
{
    if (target.Equals(Attacker)) return false;
    if (target.Team == Attacker.Team && !AllowFriendlyFire) return false;
    return true;
}

// バトルロイヤル: 自分以外全員
public override bool CanTarget(IDamageReceiver target)
    => !target.Equals(Attacker);
```

**理由:** ゲームによってターゲット判定は全く異なる。陣営システム、フレンドリーファイア、無敵状態、ガード状態など、ゲーム固有のロジックをフレームワークに押し込むと破綻する。

---

## コンポーネント

### AttackInfo

攻撃パラメータの基底クラス。アプリ側で継承する。

| プロパティ | 型 | 説明 |
|-----------|------|------|
| `Attacker` | `IDamageReceiver?` | 攻撃者。自傷防止に使う |
| `HitGroup` | `int` | 履歴共有グループ。0以下で自動生成 |
| `HittableCount` | `int` | 同一ターゲットへの最大ヒット数。0=無制限 |
| `AttackableCount` | `int` | 全体での最大ヒット数。0=無制限 |
| `Interval` | `int` | 再ヒット間隔（tick）。0以下=チェックしない |

### HitHistory

ヒット履歴を管理する。IDamageReceiverが持つ。

| メソッド | 説明 |
|---------|------|
| `CanHit(hitGroup, target, info)` | ヒット可能か判定 |
| `RecordHit(hitGroup, target)` | ヒットを記録 |
| `Tick(deltaTicks)` | 内部時刻を進める |
| `Clear()` | 全履歴をクリア |
| `ClearHitGroup(hitGroup)` | 特定HitGroupの履歴をクリア |
| `GetHitCount(hitGroup, target)` | ヒット回数を取得 |

### HittableCount と AttackableCount

| パラメータ | 制限対象 | 用途 |
|-----------|---------|------|
| `HittableCount` | 同一ターゲット | 多段技（1体に3回ヒット） |
| `AttackableCount` | 全ターゲット合計 | 貫通制限（最大5体まで貫通） |

```csharp
// 3ヒット技で最大5体まで
var multiHitAttack = new MyAttackInfo
{
    HittableCount = 3,      // 1体に最大3回
    AttackableCount = 15,   // 全体で15回（5体×3ヒット）
    Interval = 6            // 6 tick間隔で多段ヒット
};

// 単発の貫通攻撃
var pierceAttack = new MyAttackInfo
{
    HittableCount = 1,      // 1体に1回
    AttackableCount = 3     // 3体まで貫通
};
```

### DamageBody と IDamageReceiver

1つのキャラクター（IDamageReceiver）が複数の当たり判定（DamageBody）を持てる。

```
         ┌─────────────┐
         │  頭 (部位)  │ ← DamageBody (Priority: 100)
         ├─────────────┤
         │  胴 (部位)  │ ← DamageBody (Priority: 50)
         ├─────────────┤
         │  足 (部位)  │ ← DamageBody (Priority: 10)
         └─────────────┘
               │
               ▼
         IDamageReceiver (キャラクター)
```

Priorityは「同時に複数部位に当たった場合の優先順位」。高い方が先に処理される。
同一Ownerへの重複攻撃は自動排除。頭と胴に同時に当たってもダメージは1回。

### HandleSystem

攻撃は一時的。HandleSystemでプール管理する。

```csharp
var handle = combat.CreateAttack(info);  // プールから取得
// ... 攻撃処理 ...
combat.ReleaseAttack(handle);  // プールに返却
// handleは無効化、IsValid=false
```

---

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│                     CombatSystem                         │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────────┐    ┌─────────────────────────┐    │
│  │  CombatManager  │───▶│   AttackArena           │    │
│  │   (攻撃管理)    │    │ (HandleSystem Pool)     │    │
│  └────────┬────────┘    └─────────────────────────┘    │
│           │                                              │
│           ▼                                              │
│  ┌─────────────────┐    ┌─────────────────────────┐    │
│  │   AttackInfo    │    │      DamageBody         │    │
│  │  (攻撃パラメータ) │───▶│  (衝突形状の紐づけ)    │    │
│  └─────────────────┘    └───────────┬─────────────┘    │
│                                      │                   │
│                                      ▼                   │
│  ┌─────────────────┐    ┌─────────────────────────┐    │
│  │   HitHistory    │◀───│    IDamageReceiver      │    │
│  │  (多段ヒット制御) │    │   (ダメージ受信側)     │    │
│  └─────────────────┘    └─────────────────────────┘    │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

---

## 処理フロー

### 1. 攻撃の作成

```csharp
var info = new MyAttackInfo
{
    Attacker = player,
    HitGroup = 0,           // 0以下で自動生成
    HittableCount = 1,      // 同一ターゲットに1回
    AttackableCount = 0,    // 0=無制限
    Interval = 0            // 再ヒット不可
};

var handle = combat.CreateAttack(info);
```

### 2. 衝突検出（CollisionSystem）

```csharp
var collisions = collisionDetector.DetectCollisions();

foreach (var collision in collisions)
{
    var damageBody = resolver.Resolve(collision.Hurtbox);
    if (damageBody != null)
        targets.Add(damageBody);
}
```

### 3. 攻撃実行

```csharp
// 単体攻撃
var result = combat.AttackTo(handle, target);

// 複数ターゲット（Priority順にソート、同一Owner重複排除）
var results = combat.AttackTo(handle, targets);
```

### 4. 内部処理

```
AttackTo(handle, target)
    │
    ├─ ハンドル有効性チェック → 無効なら InvalidHandle
    ├─ ターゲット有効性チェック → nullなら InvalidTarget
    ├─ AttackInfo.CanTarget() → falseなら TargetFiltered
    ├─ Attack.CanAttack() → falseなら AttackLimitReached
    ├─ HitHistory.CanHit() → falseなら HitLimitReached
    ├─ IDamageReceiver.OnDamage() 呼び出し
    ├─ HitHistory.RecordHit() で履歴記録
    └─ Attack.RecordHit() でヒット数カウント
```

### 5. 攻撃の解放

```csharp
combat.ReleaseAttack(handle);
```

---

## HitHistory詳細

### 構造

HitHistoryは `Dictionary<HitHistoryKey, HitHistoryEntry>` を内部に持つ。

```
HitHistoryKey = (HitGroup, Target)
HitHistoryEntry = { HitCount, LastHitTick }
```

同じHitGroupで同じTargetへのヒットは、1つのエントリで管理される。

### ヒット可否判定フロー

```
CanHit(hitGroup, target, info)
    │
    ├─ エントリが存在しない → ヒット可能
    │
    └─ エントリが存在する
        │
        ├─ HittableCount > 0 かつ HitCount >= HittableCount
        │   → ヒット不可（回数上限）
        │
        ├─ Interval > 0 かつ (CurrentTick - LastHitTick) < Interval
        │   → ヒット不可（インターバル中）
        │
        └─ 上記以外 → ヒット可能
```

### 時間管理

HitHistoryは内部時刻（CurrentTick）を持つ。Intervalを使う場合は毎tick`Tick`を呼ぶ。

```csharp
// 毎tick
foreach (var character in characters)
    character.GetHitHistory().Tick(deltaTicks);
```

**Tick を呼ばないと CurrentTick が進まず、Interval による再ヒット判定が機能しない。**

### 自動クリーンアップ

コンストラクタで `autoCleanupInterval` を指定すると、古いエントリが自動削除される。

```csharp
var history = new HitHistory(autoCleanupInterval: 600);
// 600 tick以上アクセスのないエントリは Tick 時に削除
```

### 履歴操作

```csharp
// 全クリア
history.Clear();

// 特定HitGroupのみクリア（攻撃終了時に呼ぶ）
history.ClearHitGroup(hitGroup);

// ヒット回数取得
int count = history.GetHitCount(hitGroup, target);
```

### HitGroupのライフサイクル

```
攻撃開始
    │
    ├─ HitGroup を設定（0以下なら CombatManager が自動生成）
    │
    ├─ 攻撃処理（複数フレームにわたることも）
    │   ├─ ヒット → HitHistory.RecordHit()
    │   ├─ ヒット → HitHistory.RecordHit()
    │   └─ ...
    │
    └─ 攻撃終了
        │
        └─ HitHistory.ClearHitGroup(hitGroup)  ← 任意
            同じHitGroupで次の攻撃が再ヒット可能になる
```

ClearHitGroupを呼ばなくても動作するが、同じHitGroupの次の攻撃が同じターゲットにヒットしなくなる。
技ごとに異なるHitGroupを使うか、技の終了時にClearHitGroupを呼ぶ。

---

## AttackHandle のメソッド

HandleSystemの自動生成により、AttackHandleから直接呼べる。

| メソッド | 説明 |
|---------|------|
| `TryGetInfo(out AttackInfo?)` | 攻撃情報を取得 |
| `TryCanAttack(out bool)` | まだ攻撃可能か |
| `TryGetHitCount(out int)` | 現在のヒット回数 |
| `TryUpdateTick(int deltaTicks)` | 経過tick数を加算 |
| `TryGetElapsedTicks(out int)` | 経過tick数を取得 |
| `TryGetResolvedHitGroup(out int)` | 解決済みHitGroupを取得 |

```csharp
// 攻撃の経過tick数で有効期限を管理する例
handle.TryUpdateTick(deltaTicks);
if (handle.TryGetElapsedTicks(out var elapsed) && elapsed > attackDuration)
{
    combat.ReleaseAttack(handle);
}
```

---

## AttackResultStatus

| ステータス | 意味 |
|-----------|------|
| `Success` | 攻撃成功 |
| `InvalidHandle` | 無効な攻撃ハンドル |
| `InvalidTarget` | ターゲットがnull |
| `TargetFiltered` | CanTargetで除外 |
| `HitLimitReached` | 同一ターゲットへの上限 |
| `AttackLimitReached` | 全体の攻撃上限 |

---

## 使用例

### AttackInfo実装

```csharp
public class MyAttackInfo : AttackInfo
{
    public int BaseDamage { get; set; }
    public int Team { get; set; }

    public override bool CanTarget(IDamageReceiver target)
    {
        if (target.Equals(Attacker)) return false;
        if (target is MyCharacter c && c.Team == Team) return false;
        return true;
    }
}
```

### IDamageReceiver実装

```csharp
public class MyCharacter : IDamageReceiver
{
    public int Id { get; }
    public float Health { get; private set; }
    private readonly HitHistory _hitHistory = new();

    public DamageResult OnDamage(DamageInfo damageInfo)
    {
        var info = damageInfo.AttackInfo as MyAttackInfo;
        var damage = info?.BaseDamage ?? 10f;

        Health -= damage;

        return new DamageResult
        {
            Applied = true,
            ActualDamage = damage,
            Killed = Health <= 0
        };
    }

    public HitHistory GetHitHistory() => _hitHistory;

    public bool Equals(IDamageReceiver? other)
        => other is MyCharacter c && c.Id == Id;

    public override int GetHashCode() => Id;
}
```

### CollisionSystemとの統合

```csharp
public class CombatIntegration
{
    private readonly CombatManager _combat = new();

    public void Tick(int deltaTicks)
    {
        // HitHistoryのtickを更新
        foreach (var character in _characters)
            character.GetHitHistory().Tick(deltaTicks);
    }

    public void OnCollision(CollisionResult collision, AttackHandle handle)
    {
        var damageBody = _resolver.Resolve(collision.Hurtbox);
        if (damageBody == null) return;

        var result = _combat.AttackTo(handle, damageBody);

        if (result.IsSuccess)
            SpawnHitEffect(collision.ContactPoint);
    }
}
```

---

## トラブルシューティング

### 攻撃が当たらない

**1. AttackResultStatus を確認**

```csharp
var result = combat.AttackTo(handle, target);
Console.WriteLine(result.Status);
```

| Status | 原因 | 対処 |
|--------|------|------|
| `InvalidHandle` | ハンドルが無効 | ReleaseAttack後に使っていないか確認 |
| `InvalidTarget` | ターゲットがnull | DamageBodyのOwnerをBindしているか確認 |
| `TargetFiltered` | CanTargetがfalse | AttackInfo.CanTargetの実装を確認 |
| `HitLimitReached` | HittableCount上限 | HittableCountの設定を確認 |
| `AttackLimitReached` | AttackableCount上限 | AttackableCountの設定を確認 |

**2. HitHistory を確認**

```csharp
var history = target.GetHitHistory();
var count = history.GetHitCount(hitGroup, target);
Console.WriteLine($"HitCount: {count}");
```

### 多段ヒットしない

**1. Interval を設定しているか**

```csharp
var info = new MyAttackInfo
{
    HittableCount = 5,
    Interval = 6  // これがないと1回しか当たらない
};
```

**2. HitHistory.Tick を呼んでいるか**

```csharp
// 毎tick
foreach (var character in characters)
    character.GetHitHistory().Tick(deltaTicks);
```

### 同じ攻撃が何度も当たる

**1. HitGroup が正しく設定されているか**

```csharp
// 同じ攻撃の複数ヒット判定は同じHitGroupにする
var slashA = new MyAttackInfo { HitGroup = 100 };
var slashB = new MyAttackInfo { HitGroup = 100 };
```

**2. HitGroup が 0 以下だと毎回自動生成される**

```csharp
// これは毎回異なるHitGroupになる
var info = new MyAttackInfo { HitGroup = 0 };  // 自動生成
```

### IDamageReceiver.Equals が機能しない

HitHistory は `IDamageReceiver.Equals` で同一ターゲットを判定する。

```csharp
public class Character : IDamageReceiver
{
    public int Id { get; }

    // これがないと同一キャラクターを識別できない
    public bool Equals(IDamageReceiver? other)
        => other is Character c && c.Id == Id;

    public override int GetHashCode() => Id;
}
```

---

## テスト

```bash
dotnet test libs/systems/CombatSystem/CombatSystem.Tests/
```

テスト数: 37

## 依存関係

- HandleSystem.Core

## ディレクトリ構造

```
CombatSystem/
├── README.md
├── CombatSystem.Core/
│   ├── CombatManager.cs
│   ├── Attack/
│   │   ├── Attack.cs
│   │   ├── AttackInfo.cs
│   │   └── AttackResult.cs
│   ├── Damage/
│   │   ├── DamageBody.cs
│   │   ├── DamageInfo.cs
│   │   ├── DamageResult.cs
│   │   ├── IDamageReceiver.cs
│   │   └── IDamageBodyResolver.cs
│   └── History/
│       ├── HitHistory.cs
│       ├── HitHistoryKey.cs
│       └── HitHistoryEntry.cs
└── CombatSystem.Tests/
    ├── CombatManagerTests.cs
    ├── HitHistoryTests.cs
    ├── AttackHandleTests.cs
    └── Mocks/
```

# アクションゲーム設計ドキュメント

## 概要

本ドキュメントは、3次元空間上でEntityが相互作用するアクションゲームのコアループ設計を定義する。

> **実装状況**: このドキュメントで定義されたゲームループは `EntitySystem` として完全に実装済み。
> 詳細は [libs/EntitySystem/README.md](../../libs/EntitySystem/README.md) を参照。

---

## ユビキタス言語

| 用語 | 定義 |
|------|------|
| Entity | ゲーム世界に存在する全てのオブジェクト。位置、状態（HP等）、被衝突半径を持つ |
| メッセージ | Entityに対する操作の要求。状態変更の唯一の手段 |
| 波（Wave） | メッセージ処理の単位。1波で全Entityのキューを処理し、新たに発生したメッセージは次波へ |
| 衝突判定 | Entityが発行する空間的な判定領域。条件に合う他Entityにメッセージを送る |
| 行動（Action） | Entityが実行する振る舞い。ステートマシンで管理される |
| 依存順 | LateUpdateでの処理順序を決定する動的な関係性（騎乗者は馬に依存、等） |

---

## ドメインモデル

### Entity（集約ルート）

Entityはゲーム世界の中心的な概念であり、集約ルートである。

```
Entity
├── Identity: EntityId
├── Transform: Position, Rotation
├── State: HP, その他ゲーム固有の状態
├── CollisionRadius: float
├── MessageQueue: PriorityQueue<Message>
├── ActionStateMachine: StateMachine
└── Dependencies: List<EntityId> (親子関係)
```

**不変条件：**
- 状態（State）の変更はメッセージ処理でのみ発生する
- 消滅はフレーム末尾でのみ発生する

### Message（値オブジェクト）

```
Message
├── Target: EntityId
├── Type: MessageType
├── Priority: int
├── Payload: メッセージ固有データ
└── Source: EntityId (optional)
```

**不変条件：**
- 生成後は不変（Immutable）
- 優先度はMessageTypeで決定される

### Action（値オブジェクト）

```
Action
├── Type: ActionType
├── Parameters: 行動固有パラメータ
└── Source: Controller | AI | Network
```

---

## 境界づけられたコンテキスト

### 1. 衝突コンテキスト（Collision Context）

**責務：** 空間的な衝突判定とメッセージ発行

**主要概念：**
- CollisionShape: 衝突判定の形状
- CollisionFilter: 衝突対象のフィルタリング条件
- CollisionResult: 衝突結果

### 2. メッセージコンテキスト（Message Context）

**責務：** メッセージの配送と処理

**主要概念：**
- MessageDispatcher: メッセージの配送
- MessageQueue: Entity単位の優先度付きキュー
- WaveProcessor: 波単位での処理制御

### 3. 行動コンテキスト（Action Context）

**責務：** 行動の決定と実行

**主要概念：**
- ActionDecider: 入力（Controller/AI/Network）から行動を決定
- ActionExecutor: 決定された行動の実行
- ActionStateMachine: 行動状態の管理

### 4. 調停コンテキスト（Reconciliation Context）

**責務：** LateUpdateでの位置調停

**主要概念：**
- DependencyResolver: 依存順の動的計算
- PositionReconciler: 位置の調停（押し出し等）
- ReconciliationRule: 調停ルール（ゲームデザイン依存）

---

## ゲームループ（ドメインサービス）

### フレーム処理フロー

```
┌─────────────────────────────────────────────────────────┐
│ Update                                                  │
├─────────────────────────────────────────────────────────┤
│ 第一更新: CollisionPhase                                │
│   - 全Entityが衝突判定を発行                            │
│   - 条件に合うEntityへメッセージを送信                   │
│   - 【並列化可能】                                      │
├─────────────────────────────────────────────────────────┤
│ 第二更新: MessagePhase                                  │
│   - 波ごとにメッセージを処理                            │
│   - 優先度順（高→低）で処理                             │
│   - 新メッセージは次波へ                                │
│   - 全波完了まで繰り返し                                │
│   - 【状態変更はここでのみ】                            │
├─────────────────────────────────────────────────────────┤
│ 第三更新: DecisionPhase                                 │
│   - ステートマシンの状態を確認                          │
│   - 行動継続中でなければ、次の行動を決定                 │
│   - 入力ソース: Controller / AI / Network               │
│   - 【並列化可能】【読み取り専用】                      │
├─────────────────────────────────────────────────────────┤
│ 第四更新: ExecutionPhase                                │
│   - 決定された行動を実行                                │
│   - 位置・向きの変更                                    │
│   - アニメーション状態の変更                            │
│   - 【並列化可能】                                      │
├─────────────────────────────────────────────────────────┤
│ [モーション更新]                                        │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ LateUpdate                                              │
├─────────────────────────────────────────────────────────┤
│ ReconciliationPhase                                     │
│   - 依存順を動的計算（循環検出含む）                     │
│   - 依存順に従って位置調停                              │
│   - 押し出しルールはゲームデザイン依存                   │
│   - カメラ位置更新                                      │
│   - IK処理                                             │
├─────────────────────────────────────────────────────────┤
│ CleanupPhase                                            │
│   - HP0等で消滅フラグが立ったEntityを削除               │
│   - 宛先不在のメッセージを破棄                          │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ [描画]                                                  │
└─────────────────────────────────────────────────────────┘
```

---

## 設計上の決定事項

### 1. 状態変更の一元化

**決定：** Entityの論理状態（HP等）はメッセージ処理でのみ変更される

**理由：**
- 決定論性の担保
- デバッグの容易さ
- 並列処理時の競合回避

### 2. メッセージの優先度

**決定：** メッセージはTypeごとに固定の優先度を持つ

**理由：**
- 並列環境での決定論性
- 同じ入力に対して常に同じ結果を保証

**例：**
```
回復メッセージ: 優先度 100（高）
ダメージメッセージ: 優先度 50（中）
状態異常メッセージ: 優先度 30（低）
```

### 3. Entity消滅のタイミング

**決定：** フレーム末尾（LateUpdate後）で消滅

**理由：**
- フレーム内での参照整合性の維持
- 「死亡したが存在する」期間を最小化

### 4. 宛先消滅時のメッセージ処理

**決定：** メッセージは破棄。オプションで送信側に通知イベントを発行

**理由：**
- 存在しないEntityへの処理は無意味
- 送信側が対応したい場合のための通知オプション

### 5. 第三・第四更新の分離

**決定：** 行動の「決定」と「実行」を分離する

**理由：**
- デバッグ・可視化の容易さ
- AI/Controller/Networkの差し替え容易性
- ネットワーク同期（決定のみ送信）
- Entity単位での並列化

### 6. 位置調停の方式

**決定：** 押し出し方式、ルールはゲームデザイン依存

**例：**
- プレイヤー vs 壁: プレイヤーが押し出される
- プレイヤー vs 巨大敵: プレイヤーが押し出される
- 小型敵 vs 小型敵: 相互に押し出し

---

## 並列化戦略

### 並列化可能なフェーズ

| フェーズ | 並列化 | 備考 |
|----------|--------|------|
| 第一更新（衝突） | ○ | Entity単位で並列化可能 |
| 第二更新（メッセージ） | △ | 波内は並列可、波間は逐次 |
| 第三更新（決定） | ○ | 読み取り専用のため安全 |
| 第四更新（実行） | ○ | 位置競合はLateUpdateで調停 |
| LateUpdate | △ | 依存順に逐次処理 |

### 注意点

- 第四更新での位置変更は並列で行い、競合はLateUpdateで解決
- メッセージ処理は波内では並列化可能だが、Entity間の依存がある場合は注意

---

## 親子関係と依存順

### 親子関係

- Entityは位置によらず親子関係を持つことがある
- 例：騎乗者と馬、キャラクターと持っている武器

### 依存順の計算

```
入力: 全Entityの依存関係グラフ
出力: トポロジカルソート順

処理:
1. 依存関係からDAGを構築
2. 循環検出（検出時はエラー）
3. トポロジカルソートで処理順を決定
```

### 例

```
馬B → 騎乗者A → 旗C

処理順: B → A → C
```

---

## エラーハンドリング

### 循環依存の検出

- 依存順計算時にトポロジカルソートで検出
- 検出時は開発ビルドでエラーログ出力

### メッセージ無限ループ

- 設計上は許容
- デバッグビルドで波の深度監視を推奨
- 閾値超過時に警告ログ出力

---

## 実装状況

本設計は `EntitySystem` として実装済み。

| コンポーネント | 実装クラス | 状態 |
|---------------|-----------|------|
| GameLoopOrchestrator | `EntitySystem.GameLoop.GameLoopOrchestrator<T>` | 完了 |
| CollisionPhase | `EntitySystem.Phases.CollisionPhaseProcessor<T>` | 完了 |
| MessagePhase | `EntitySystem.Phases.MessagePhaseProcessor<T>` | 完了 |
| DecisionPhase | `EntitySystem.Phases.DecisionPhaseProcessor<T>` | 完了 |
| ExecutionPhase | `EntitySystem.Phases.ExecutionPhaseProcessor<T>` | 完了 |
| ReconciliationPhase | `EntitySystem.Phases.ReconciliationPhaseProcessor<T>` | 完了 |
| CleanupPhase | `EntitySystem.Phases.CleanupPhaseProcessor<T>` | 完了 |
| EntityContext | `EntitySystem.Context.EntityContext<T>` | 完了 |
| SpawnBridge | `EntitySystem.Spawn.SpawnBridge<T>` | 完了 |

---

## 今後の検討事項

1. **具体的なメッセージ優先度の設計**
   - ゲームデザインに基づく優先度表の策定

2. **押し出しルールの具体化**
   - Entity種別ごとの押し出し優先度マトリクス
   - IDependencyResolver, IPositionReconciler の具体実装

3. **ネットワーク同期の詳細**
   - 決定フェーズの同期プロトコル
   - 遅延補償の方式

4. **パフォーマンス監視**
   - メッセージ波の深度
   - 依存順計算のコスト
   - Entity数のスケーラビリティ

5. **ゲーム固有実装**
   - IInputProvider, ICharacterStateProvider の具体実装
   - IActionFactory の具体実装
   - IEntityInitializer の具体実装

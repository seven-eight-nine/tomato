# UnitLODSystem

ユニットベースのLODライフサイクル管理ライブラリ。

## 概要

「目標レベル」を指定すると、必要な詳細レベル（IUnitDetail）を自動的に生成・ロード・破棄するシステム。
`Unit<TSelf>`クラスが全体を管理し、各詳細レベルは`IUnitDetail<TOwner>`インターフェースを実装する。

```
目標: 2

詳細レベル構成:
  requiredAt=1: DataA, DataB   <- 目標1以上で必要
  requiredAt=2: ModelC         <- 目標2以上で必要

動作:
  1. DataA, DataB を生成 -> 並行ロード -> 順次Ready化
  2. ModelC を生成 -> ロード -> Ready化
  3. 全詳細レベルがReadyになったら IsStable = true
```

## 特徴

- **グループ管理**: 同じ`requiredAt`の詳細レベルをまとめて扱う
- **パイプライン処理**: ロードは並行、Ready化は順次で効率的
- **自動クリーンアップ**: 目標が下がると不要な詳細レベルを自動破棄
- **継承可能**: `Unit<TSelf>`をCRTPパターンで継承してカスタマイズ可能
- **所有者参照**: 詳細レベルのコールバックで所有者のUnitにアクセス可能

---

## 基本的な使い方

### 1. 詳細レベルを実装

```csharp
// 基本的な使い方（Unitクラスを使用）
public class MyDataDetail : IUnitDetail<Unit>
{
    public UnitPhase Phase { get; private set; } = UnitPhase.None;
    private AsyncOperation _loadOp;

    public void OnUpdatePhase(Unit owner, UnitPhase phase)
    {
        switch (Phase)
        {
            case UnitPhase.Loading:
                if (_loadOp.isDone)
                    Phase = UnitPhase.Loaded;
                break;
            case UnitPhase.Creating:
                Initialize();
                Phase = UnitPhase.Ready;
                break;
            case UnitPhase.Unloading:
                if (IsUnloadComplete())
                    Phase = UnitPhase.Unloaded;
                break;
        }
    }

    public void OnChangePhase(Unit owner, UnitPhase prev, UnitPhase next)
    {
        Phase = next;
        if (next == UnitPhase.Loading)
        {
            _loadOp = StartLoadAsync();
        }
        else if (next == UnitPhase.Unloading)
        {
            StartUnloadAsync();
        }
    }

    public void Dispose() { /* リソース解放 */ }
}
```

### 2. Unitを設定

```csharp
var unit = new Unit();

// 詳細レベルを登録（requiredAtで必要な目標レベルを指定）
unit.Register<DataDetailA>(1);
unit.Register<DataDetailB>(1);
unit.Register<ModelDetail>(2);

// イベント購読（オプション）
unit.UnitPhaseChanged += (sender, e) =>
{
    Console.WriteLine($"{e.DetailType.Name}: {e.OldPhase} -> {e.NewPhase}");
};
```

### 3. 目標を設定してTickを呼ぶ

```csharp
// 目標を設定
unit.RequestState(2);

// 毎フレームTickを呼ぶ
void Update()
{
    unit.Tick();

    // Get<T>() は Phase == Ready のときのみインスタンスを返す
    // それ以外（Loading, Creating, Unloading等）は null
    var model = unit.Get<ModelDetail>();
    if (model != null)
    {
        // Ready状態のときのみここに到達
    }

    // IsStable は全詳細レベルがReadyのとき true
    if (unit.IsStable)
    {
        // 全詳細レベルが取得可能
    }
}
```

### Get<T>() の取得タイミング

| フェーズ | Get<T>() | 説明 |
|:---------|:--------:|:-----|
| Loading | `null` | ロード中 |
| Loaded/Creating | `null` | 初期化中 |
| **Ready** | **インスタンス** | **取得可能** |
| Unloading | `null` | アンロード開始と同時に取得不可 |

**重要**: Unloading開始の瞬間から`Get<T>()`は`null`を返す。

---

## カスタムUnit継承（CRTPパターン）

Unitを継承してカスタムプロパティやメソッドを追加できる。
基底クラスは最小限のインターフェースのみ提供し、Idなどのプロパティは派生クラスで定義する。

```csharp
// カスタムUnit
public class CharacterUnit : Unit<CharacterUnit>
{
    public string Id { get; }
    public string CharacterType { get; }
    public int Level { get; set; }

    public CharacterUnit(string id, string characterType)
    {
        Id = id;
        CharacterType = characterType;
    }
}

// カスタムUnitに対応した詳細レベル
public class CharacterModelDetail : IUnitDetail<CharacterUnit>
{
    public UnitPhase Phase { get; private set; } = UnitPhase.None;

    public void OnUpdatePhase(CharacterUnit owner, UnitPhase phase)
    {
        // owner.Id, owner.CharacterType, owner.Level にアクセス可能
        if (Phase == UnitPhase.Loading)
        {
            var modelPath = $"models/{owner.CharacterType}/lv{owner.Level}";
            // ...
            Phase = UnitPhase.Loaded;
        }
    }

    public void OnChangePhase(CharacterUnit owner, UnitPhase prev, UnitPhase next)
    {
        Phase = next;
    }

    public void Dispose() { }
}

// 使用例
var hero = new CharacterUnit("hero_001", "warrior") { Level = 5 };
hero.Register<CharacterModelDetail>(1);
hero.RequestState(1);
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** を参照。

---

## ライセンス

MIT License

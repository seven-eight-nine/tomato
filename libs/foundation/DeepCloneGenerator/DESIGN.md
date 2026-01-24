# DeepCloneGenerator 設計書

オブジェクトの深いクローンを自動生成するC# Source Generatorの詳細設計ドキュメント。

namespace: `Tomato.DeepCloneGenerator`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [セットアップ](#セットアップ)
5. [属性詳細](#属性詳細)
6. [コピー戦略](#コピー戦略)
7. [循環参照対応](#循環参照対応)
8. [生成コードの構造](#生成コードの構造)
9. [診断メッセージ](#診断メッセージ)
10. [パフォーマンス](#パフォーマンス)
11. [トラブルシューティング](#トラブルシューティング)
12. [ディレクトリ構造](#ディレクトリ構造)

---

## クイックスタート

### 1. クラスに属性を付与

```csharp
using Tomato.DeepCloneGenerator;

[DeepClonable]
public partial class GameState
{
    public int Score { get; set; }
    public List<Enemy> Enemies { get; set; }
    public Player Player { get; set; }
}
```

### 2. ビルド時に自動生成

`GameState.DeepClone.g.cs` が生成される：

```csharp
public partial class GameState : IDeepCloneable<GameState>
{
    public GameState DeepClone() => DeepCloneInternal();

    internal GameState DeepCloneInternal()
    {
        var clone = new GameState();
        clone.Score = this.Score;
        // ... コレクションと参照型の深いコピー
        return clone;
    }
}
```

### 3. 使用

```csharp
var cloned = original.DeepClone();
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **深いクローン** | Deep Clone | オブジェクトとその参照先すべてを再帰的に複製する。元と複製は完全に独立。 |
| **浅いクローン** | Shallow Clone | オブジェクトのみ複製し、参照先は共有する。 |
| **循環参照** | Circular Reference | オブジェクト間で相互に参照し合っている状態。A→B→A のような構造。 |

### 属性

| 属性 | 対象 | 説明 |
|------|------|------|
| `[DeepClonable]` | クラス/構造体 | この型に DeepClone() を生成する |
| `[DeepCloneOption.Ignore]` | メンバー | クローンから除外（default値になる） |
| `[DeepCloneOption.Shallow]` | メンバー | 参照のみコピー（深いコピーしない） |
| `[DeepCloneOption.Cyclable]` | メンバー | 循環参照を追跡する |

### 生成される型

| 型/メンバー | 説明 |
|-------------|------|
| `IDeepCloneable<T>` | DeepClone() を持つインターフェース |
| `DeepClone()` | パブリックなクローンメソッド |
| `DeepCloneInternal()` | 内部用クローンメソッド（再帰呼び出し用） |
| `DeepCloneCycleTracker` | 循環参照を追跡する静的クラス |

---

## 設計哲学

### 原則1: ゼロ設定（Zero Configuration）

属性を付けるだけで動作する。ほとんどのケースで追加設定不要。

```csharp
[DeepClonable]
public partial class MyClass { }  // これだけで完了
```

### 原則2: 型安全（Type Safety）

- コンパイル時にコード生成
- 実行時リフレクションなし
- 型の不一致はコンパイルエラー

```csharp
// 生成コードは静的に型付けされる
clone.Name = this.Name;  // string → string
clone.Items = ...;       // List<T> → List<T>
```

### 原則3: 明示的なオプトイン（Explicit Opt-in）

深いコピーが危険な場合は、明示的に指定させる。

```csharp
// 循環参照は明示的に宣言
[DeepCloneOption.Cyclable]
public TreeNode? Parent { get; set; }

// 共有したい参照は明示的に宣言
[DeepCloneOption.Shallow]
public ILogger Logger { get; set; }
```

### 原則4: 診断による早期発見（Early Detection）

問題のある構成はコンパイル時に警告/エラー。

```csharp
// エラー: partial がない
[DeepClonable]
public class NotPartial { }  // DCG001: Type must be partial

// 警告: 深いコピーができない型
public Action Callback { get; set; }  // DCG103: Delegate shallow copy
```

---

## セットアップ

### ステップ1: パッケージ参照を追加

```xml
<ItemGroup>
    <!-- 属性とランタイムヘルパー -->
    <PackageReference Include="DeepCloneGenerator.Attributes" Version="x.x.x" />

    <!-- Source Generator（ビルド時のみ） -->
    <PackageReference Include="DeepCloneGenerator.Generator" Version="x.x.x"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
</ItemGroup>
```

### ステップ2: 型を partial にする

```csharp
// ✓ 正しい
[DeepClonable]
public partial class MyClass { }

// ✗ エラー
[DeepClonable]
public class MyClass { }  // DCG001
```

### ステップ3: パラメータレスコンストラクタを用意

```csharp
// ✓ 暗黙的（プロパティのみ）
[DeepClonable]
public partial class Simple
{
    public int Value { get; set; }
}

// ✓ 明示的
[DeepClonable]
public partial class WithConstructor
{
    public int Value { get; set; }

    public WithConstructor() { }  // パラメータレス
    public WithConstructor(int value) { Value = value; }
}

// ✗ エラー
[DeepClonable]
public partial class NoParameterless
{
    public NoParameterless(int required) { }  // DCG002
}
```

---

## 属性詳細

### [DeepClonable]

型に DeepClone() メソッドを生成する。

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DeepClonableAttribute : Attribute { }
```

**使用例:**

```csharp
// クラス
[DeepClonable]
public partial class MyClass { }

// 構造体
[DeepClonable]
public partial struct MyStruct { }

// ジェネリック型
[DeepClonable]
public partial class Container<T> { }

// ネストした型
public partial class Outer
{
    [DeepClonable]
    public partial class Inner { }
}
```

**制約:**

| 条件 | エラー |
|------|--------|
| `partial` が必要 | DCG001 |
| パラメータレスコンストラクタが必要 | DCG002 |
| public/internal のみ | DCG003 |
| 抽象クラス不可 | DCG004 |
| init-only プロパティ不可 | DCG005 |
| file スコープ不可 | DCG006 |

### [DeepCloneOption.Ignore]

メンバーをクローンから除外する。クローン先は `default` 値。

```csharp
[DeepClonable]
public partial class Document
{
    public string Content { get; set; }

    [DeepCloneOption.Ignore]
    public DateTime LastAccessed { get; set; }  // クローン時は default(DateTime)

    [DeepCloneOption.Ignore]
    public WeakReference<Cache> CacheRef { get; set; }  // クローン時は null
}
```

**用途:**

- キャッシュ、一時データ
- 参照カウンタ、バージョン番号
- クローン不要な計算済みの値
- 深いコピーが危険/不可能な型

### [DeepCloneOption.Shallow]

参照のみコピー（深いコピーしない）。元と複製で同じインスタンスを共有。

```csharp
[DeepClonable]
public partial class ViewModel
{
    public ObservableCollection<Item> Items { get; set; }

    [DeepCloneOption.Shallow]
    public ILogger Logger { get; set; }  // シングルトンを共有

    [DeepCloneOption.Shallow]
    public Texture2D Texture { get; set; }  // 大きなリソースを共有
}
```

**用途:**

- シングルトン、サービスロケータ
- イミュータブルな共有リソース
- クローンすべきでない大きなオブジェクト
- 外部から注入された依存関係

### [DeepCloneOption.Cyclable]

循環参照を含む可能性があるメンバー。追跡システムを使用。

```csharp
[DeepClonable]
public partial class TreeNode
{
    public string Name { get; set; }

    [DeepCloneOption.Cyclable]
    public TreeNode? Parent { get; set; }

    [DeepCloneOption.Cyclable]
    public List<TreeNode> Children { get; set; }
}

[DeepClonable]
public partial class Person
{
    public string Name { get; set; }

    [DeepCloneOption.Cyclable]
    public Person? Spouse { get; set; }  // 相互参照
}
```

**用途:**

- ツリー構造（親子関係）
- グラフ構造（相互参照）
- 双方向リンク

**注意:**

- `[Cyclable]` がなければ、循環参照は無限ループになる
- 追跡のオーバーヘッドがあるため、必要な場合のみ使用

---

## コピー戦略

ジェネレータは型に応じて最適なコピー戦略を自動選択する。

### 値コピー（ValueCopy）

値をそのままコピー。

```csharp
// 対象: プリミティブ型、enum、値型
clone.Score = this.Score;          // int
clone.IsActive = this.IsActive;    // bool
clone.State = this.State;          // enum
clone.Position = this.Position;    // struct (値コピー)
```

### 参照コピー（ReferenceCopy）

参照をそのままコピー（イミュータブルな型）。

```csharp
// 対象: string, DateTime, TimeSpan, Guid, Immutable*
clone.Name = this.Name;            // string
clone.CreatedAt = this.CreatedAt;  // DateTime
clone.Id = this.Id;                // Guid
clone.Items = this.Items;          // ImmutableList<T>
```

### 配列

```csharp
// 1次元配列
if (this.Values != null)
{
    clone.Values = new int[this.Values.Length];
    for (int i = 0; i < this.Values.Length; i++)
    {
        clone.Values[i] = this.Values[i];  // 要素のコピー
    }
}

// 2次元配列
if (this.Grid != null)
{
    int len0 = this.Grid.GetLength(0);
    int len1 = this.Grid.GetLength(1);
    clone.Grid = new int[len0, len1];
    for (int i0 = 0; i0 < len0; i0++)
    for (int i1 = 0; i1 < len1; i1++)
    {
        clone.Grid[i0, i1] = this.Grid[i0, i1];
    }
}

// ジャグ配列
if (this.Jagged != null)
{
    clone.Jagged = new int[this.Jagged.Length][];
    for (int i = 0; i < this.Jagged.Length; i++)
    {
        if (this.Jagged[i] != null)
        {
            clone.Jagged[i] = new int[this.Jagged[i].Length];
            Array.Copy(this.Jagged[i], clone.Jagged[i], this.Jagged[i].Length);
        }
    }
}
```

### List

```csharp
if (this.Items != null)
{
    clone.Items = new List<Item>(this.Items.Count);
    foreach (var item in this.Items)
    {
        if (item != null)
            clone.Items.Add(item.DeepCloneInternal());  // 要素も深いコピー
        else
            clone.Items.Add(item!);
    }
}
```

### Dictionary

```csharp
if (this.Map != null)
{
    clone.Map = new Dictionary<string, Value>(this.Map.Count);
    foreach (var kvp in this.Map)
    {
        var clonedValue = kvp.Value?.DeepCloneInternal();
        clone.Map.Add(kvp.Key, clonedValue!);  // キーは参照コピー、値は深いコピー
    }
}
```

### DeepClonable な型

```csharp
// [DeepClonable] または IDeepCloneable<T> を実装
if (this.Child != null)
{
    clone.Child = this.Child.DeepCloneInternal();
}
```

### ジェネリック型パラメータ

```csharp
// 実行時に型を判定
if (this.Data != null)
{
    if (this.Data is IDeepCloneable<T> cloneable)
        clone.Data = cloneable.DeepClone();
    else
        clone.Data = this.Data;  // 値型またはイミュータブル
}
```

### コピー戦略一覧

| 戦略 | 対象 | 説明 |
|------|------|------|
| `ValueCopy` | プリミティブ、enum、struct | 値をコピー |
| `ReferenceCopy` | string、イミュータブル型 | 参照をコピー |
| `Array` | `T[]` | 新しい配列を作成、要素をコピー |
| `JaggedArray` | `T[][]` | ネストした配列をコピー |
| `MultiDimensionalArray2` | `T[,]` | 2次元配列をコピー |
| `MultiDimensionalArray3` | `T[,,]` | 3次元配列をコピー |
| `List` | `List<T>` | 新しいリストを作成、要素をコピー |
| `Dictionary` | `Dictionary<K,V>` | 新しい辞書を作成、値をコピー |
| `HashSet` | `HashSet<T>` | 新しいセットを作成 |
| `Queue` | `Queue<T>` | 新しいキューを作成 |
| `Stack` | `Stack<T>` | 新しいスタックを作成 |
| `LinkedList` | `LinkedList<T>` | 新しいリストを作成 |
| `SortedList` | `SortedList<K,V>` | 新しいソート済みリストを作成 |
| `SortedDictionary` | `SortedDictionary<K,V>` | 新しいソート済み辞書を作成 |
| `SortedSet` | `SortedSet<T>` | 新しいソート済みセットを作成 |
| `ObservableCollection` | `ObservableCollection<T>` | 新しいコレクションを作成 |
| `ReadOnlyCollection` | `ReadOnlyCollection<T>` | 内部リストをコピーしてラップ |
| `ConcurrentDictionary` | `ConcurrentDictionary<K,V>` | 新しい並行辞書を作成 |
| `ConcurrentQueue` | `ConcurrentQueue<T>` | 新しい並行キューを作成 |
| `ConcurrentStack` | `ConcurrentStack<T>` | 新しい並行スタックを作成 |
| `ConcurrentBag` | `ConcurrentBag<T>` | 新しい並行バッグを作成 |
| `DeepCloneable` | `[DeepClonable]` 型 | DeepCloneInternal() を呼ぶ |
| `TypeParameter` | ジェネリック `T` | 実行時に判定 |
| `Nullable` | `T?` | null チェック後にコピー |
| `ImmutableReference` | Immutable* | 参照をコピー |
| `ShallowWithWarning` | その他 | 警告付きで参照コピー |

---

## 循環参照対応

### 仕組み

`[DeepCloneOption.Cyclable]` が指定されたメンバーは、`DeepCloneCycleTracker` を使用して循環参照を検出・解決する。

```csharp
// Person.Spouse が相互参照
alice.Spouse = bob;
bob.Spouse = alice;

// Cyclable なしだと無限ループ
// alice.DeepClone() → bob.DeepClone() → alice.DeepClone() → ...

// Cyclable ありだと正しく処理
// alice.DeepClone() → bob.DeepClone() → 既にクローン済みの alice を参照
```

### DeepCloneCycleTracker

スレッドごとに独立したキャッシュでクローン済みオブジェクトを追跡。

```csharp
public static class DeepCloneCycleTracker
{
    [ThreadStatic]
    private static Dictionary<object, object>? _cloneMap;

    public static bool TryGetClone<T>(T original, out T? clone);
    public static void Register<T>(T original, T clone);
    public static void Clear();
}
```

### 生成コード（Cyclable あり）

```csharp
public partial class Person : IDeepCloneable<Person>
{
    public Person DeepClone()
    {
        try
        {
            return DeepCloneInternal();
        }
        finally
        {
            DeepCloneCycleTracker.Clear();  // 必ずクリア
        }
    }

    internal Person DeepCloneInternal()
    {
        var clone = new Person();

        // 自身を即座に登録（循環参照対策）
        DeepCloneCycleTracker.Register(this, clone);

        clone.Name = this.Name;

        // Cyclable メンバーの処理
        if (this.Spouse != null)
        {
            if (DeepCloneCycleTracker.TryGetClone(this.Spouse, out Person? cached))
            {
                clone.Spouse = cached;  // 既にクローン済み
            }
            else
            {
                clone.Spouse = this.Spouse.DeepCloneInternal();
            }
        }

        return clone;
    }
}
```

### 使い分け

| シナリオ | Cyclable |
|----------|----------|
| 単方向の親子関係（子→親のみ） | 必要 |
| 双方向リンク（親↔子） | 必要 |
| グラフ構造 | 必要 |
| 独立したオブジェクト | 不要 |
| コレクション内の相互参照 | 必要 |

---

## 生成コードの構造

### 基本構造

```csharp
// 元のコード
[DeepClonable]
public partial class MyClass
{
    public int Value { get; set; }
    public string Name { get; set; }
}

// 生成コード（MyClass.DeepClone.g.cs）
#nullable enable

namespace MyNamespace
{
    partial class MyClass : global::Tomato.DeepCloneGenerator.IDeepCloneable<MyClass>
    {
        /// <summary>このオブジェクトの深いクローンを作成します。</summary>
        public MyClass DeepClone()
        {
            return DeepCloneInternal();
        }

        /// <summary>内部クローン処理（再帰呼び出し用）。</summary>
        internal MyClass DeepCloneInternal()
        {
            var clone = new MyClass();
            clone.Value = this.Value;
            clone.Name = this.Name;
            return clone;
        }
    }
}
```

### 構造体の場合

```csharp
// 構造体は IDeepCloneable<T> を実装しない（where T : class 制約）
partial struct MyStruct
{
    public MyStruct DeepClone()
    {
        return DeepCloneInternal();
    }

    internal MyStruct DeepCloneInternal()
    {
        var clone = new MyStruct();
        // ...
        return clone;
    }
}
```

### ネストした型

```csharp
partial class Outer
{
    partial class Inner : global::Tomato.DeepCloneGenerator.IDeepCloneable<Outer.Inner>
    {
        public Inner DeepClone() => DeepCloneInternal();
        internal Inner DeepCloneInternal() { /* ... */ }
    }
}
```

### ジェネリック型

```csharp
partial class Container<T> : global::Tomato.DeepCloneGenerator.IDeepCloneable<Container<T>>
{
    public Container<T> DeepClone() => DeepCloneInternal();

    internal Container<T> DeepCloneInternal()
    {
        var clone = new Container<T>();
        // T の型に応じた処理
        if (this.Data != null)
        {
            if (this.Data is global::Tomato.DeepCloneGenerator.IDeepCloneable<T> cloneable)
                clone.Data = cloneable.DeepClone();
            else
                clone.Data = this.Data;
        }
        return clone;
    }
}
```

---

## 診断メッセージ

### エラー（Error）

| コード | メッセージ | 原因 | 対処 |
|--------|-----------|------|------|
| DCG001 | Type must be partial | `partial` キーワードがない | 型宣言に `partial` を追加 |
| DCG002 | Parameterless constructor required | パラメータレスコンストラクタがない | パラメータレスコンストラクタを追加 |
| DCG003 | Invalid type accessibility | private/protected のみ | public または internal に変更 |
| DCG004 | Abstract class not supported | 抽象クラス | 抽象でない派生クラスに属性を付ける |
| DCG005 | Init-only property not supported | `init` アクセサ | `set` に変更するか `[Ignore]` を付ける |
| DCG006 | File-scoped type not supported | `file` スコープ | `file` を削除 |

### 警告（Warning）

| コード | メッセージ | 原因 | 対処 |
|--------|-----------|------|------|
| DCG101 | Shallow copy used | 深いコピーができない型 | `[Shallow]` を明示、または設計を見直し |
| DCG102 | Readonly field skipped | readonly フィールド | コンストラクタで設定、またはプロパティに変更 |
| DCG103 | Delegate shallow copy | デリゲート型 | `[Shallow]` または `[Ignore]` を付ける |
| DCG104 | Event shallow copy | イベント | `[Shallow]` または `[Ignore]` を付ける |

---

## パフォーマンス

### 設計目標

| 指標 | 目標 |
|------|------|
| 実行時リフレクション | なし |
| 追加のアロケーション | クローンオブジェクトのみ |
| 循環参照追跡オーバーヘッド | Cyclable メンバーがある場合のみ |

### 最適化

**静的コード生成:**
- すべてのクローンロジックはコンパイル時に生成
- 型情報の実行時ルックアップなし

**最小限のアロケーション:**
- クローン先オブジェクトとコレクションのみ割り当て
- 中間オブジェクトなし

**Cyclable の選択的使用:**
- Cyclable がなければ追跡システムを使わない
- 必要な場合のみオーバーヘッドが発生

### ベンチマーク指針

```csharp
// 単純なオブジェクト（プロパティのみ）
// → 手書きのクローンとほぼ同等

// コレクションを含むオブジェクト
// → foreach によるイテレーション + 要素のクローン

// Cyclable を含むオブジェクト
// → Dictionary によるルックアップが追加
```

---

## トラブルシューティング

### DeepClone() が見つからない

**症状:**
```csharp
var clone = original.DeepClone();  // CS1061: 'MyClass' does not contain a definition for 'DeepClone'
```

**原因と対処:**

| 原因 | 対処 |
|------|------|
| `[DeepClonable]` がない | 属性を追加 |
| `partial` がない | `partial` を追加 |
| Generator が動いていない | パッケージ参照を確認、ビルドをクリーン |
| 診断エラーがある | エラー一覧を確認 |

### 無限ループ / StackOverflow

**症状:**
```
StackOverflowException at MyClass.DeepCloneInternal()
```

**原因と対処:**
- 循環参照があるのに `[Cyclable]` がない
- 該当メンバーに `[DeepCloneOption.Cyclable]` を追加

### 一部のプロパティがクローンされない

**症状:**
クローン後、特定のプロパティが `null` または `default`

**原因と対処:**

| 原因 | 対処 |
|------|------|
| `[Ignore]` が付いている | 属性を削除 |
| `readonly` フィールド | プロパティに変更 |
| `init` アクセサ | `set` に変更 |
| private setter | internal 以上に変更 |

### コレクションの要素がシャローコピー

**症状:**
```csharp
cloned.Items[0].Value = 100;
original.Items[0].Value == 100  // true（共有されている）
```

**原因と対処:**
- 要素の型が `[DeepClonable]` でない
- 要素の型に `[DeepClonable]` を追加、または `IDeepCloneable<T>` を実装

### 警告 DCG101 / DCG103 が出る

**症状:**
```
DCG101: Shallow copy used for member 'Handler' of type 'Action<int>'
```

**原因と対処:**
- 深いコピーができない型（デリゲート、インターフェースなど）
- 意図的なら `[Shallow]` を明示して警告を抑制
- クローン不要なら `[Ignore]` を追加

### ジェネリック型のクローンが正しくない

**症状:**
```csharp
Container<MyClass> cloned = original.DeepClone();
cloned.Data == original.Data  // true（参照が同じ）
```

**原因と対処:**
- `MyClass` が `IDeepCloneable<MyClass>` を実装していない
- `MyClass` に `[DeepClonable]` を追加

---

## ディレクトリ構造

```
DeepCloneGenerator/
├── README.md                          # クイックスタート
├── DESIGN.md                          # 本ドキュメント
│
├── DeepCloneGenerator.Attributes/     # 属性とランタイムヘルパー
│   ├── DeepClonableAttribute.cs       # [DeepClonable] 属性
│   ├── DeepCloneOption.cs             # [Ignore], [Shallow], [Cyclable]
│   ├── IDeepCloneable.cs              # IDeepCloneable<T> インターフェース
│   └── DeepCloneCycleTracker.cs       # 循環参照追跡
│
├── DeepCloneGenerator.Generator/      # Source Generator
│   ├── DeepCloneSourceGenerator.cs    # メインジェネレータ
│   ├── TypeAnalyzer.cs                # 型分析
│   ├── CodeEmitter.cs                 # コード生成
│   ├── Enums.cs                       # CloneOption, CopyStrategy
│   ├── MemberInfo.cs                  # メンバー情報
│   └── DiagnosticDescriptors.cs       # 診断メッセージ定義
│
└── DeepCloneGenerator.Tests/          # テスト
    ├── Attributes/                    # 属性のテスト
    ├── Generator/                     # ジェネレータのテスト
    ├── Integration/                   # 統合テスト
    └── Runtime/                       # ランタイムヘルパーのテスト
```

# DeepCloneGenerator

オブジェクトの深いクローン（Deep Clone）を自動生成するC# Source Generator。

## これは何？

`[DeepClonable]` 属性を付けるだけで、オブジェクトの完全な複製を作る `DeepClone()` メソッドを自動生成する。
ネストしたオブジェクト、コレクション、循環参照まで対応。

```csharp
[DeepClonable]
public partial class GameState
{
    public int Score { get; set; }
    public List<Item> Items { get; set; }
    public Player Player { get; set; }
}

// 使用
var original = new GameState { ... };
var cloned = original.DeepClone();  // 完全に独立した複製

cloned.Items.Add(new Item());       // original.Items には影響しない
```

## なぜ使うのか

- **ゼロボイラープレート**: 属性を付けるだけ。手書きのクローンロジック不要
- **型安全**: コンパイル時にコード生成。実行時リフレクションなし
- **高速**: Source Generatorによる静的コード生成。パフォーマンス劣化なし
- **循環参照対応**: 相互参照するオブジェクトも正しくクローン

---

## クイックスタート

### 1. パッケージを追加

```xml
<ItemGroup>
    <PackageReference Include="DeepCloneGenerator.Attributes" Version="x.x.x" />
    <PackageReference Include="DeepCloneGenerator.Generator" Version="x.x.x" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. クラスに属性を付与

```csharp
using Tomato.DeepCloneGenerator;

[DeepClonable]
public partial class Person  // partial が必須
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Tags { get; set; }
}
```

### 3. DeepClone() を呼ぶ

```csharp
var original = new Person
{
    Name = "Alice",
    Age = 30,
    Tags = new List<string> { "developer", "gamer" }
};

var cloned = original.DeepClone();

// cloned は完全に独立したコピー
cloned.Name = "Bob";
cloned.Tags.Add("musician");

Console.WriteLine(original.Name);        // "Alice"（変わらない）
Console.WriteLine(original.Tags.Count);  // 2（変わらない）
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- 属性の詳細（Ignore, Shallow, Cyclable）
- コピー戦略（値型、参照型、コレクション）
- 循環参照の仕組み
- 診断メッセージ一覧
- トラブルシューティング

---

## 主要な概念

| 概念 | 説明 |
|------|------|
| **DeepClone** | オブジェクトとその参照先すべてを再帰的に複製 |
| **ShallowClone** | オブジェクトのみ複製、参照先は共有 |
| **Cyclable** | 循環参照を含む可能性があるメンバー |

### 生成の流れ

```
[DeepClonable] 属性を検出
       ↓
型を分析（メンバー、コピー戦略を決定）
       ↓
DeepClone() / DeepCloneInternal() を生成
       ↓
*.DeepClone.g.cs として出力
```

---

## よく使うパターン

### 特定メンバーを除外

```csharp
[DeepClonable]
public partial class Document
{
    public string Content { get; set; }

    [DeepCloneOption.Ignore]
    public int CacheVersion { get; set; }  // クローン時は default(int) = 0
}
```

### シャローコピー（参照共有）

```csharp
[DeepClonable]
public partial class ViewModel
{
    public string Title { get; set; }

    [DeepCloneOption.Shallow]
    public ILogger Logger { get; set; }  // 同じインスタンスを共有
}
```

### 循環参照

```csharp
[DeepClonable]
public partial class TreeNode
{
    public string Name { get; set; }

    [DeepCloneOption.Cyclable]
    public TreeNode? Parent { get; set; }  // 親への参照

    [DeepCloneOption.Cyclable]
    public List<TreeNode> Children { get; set; }  // 子への参照
}
```

### ネストしたDeepClonableオブジェクト

```csharp
[DeepClonable]
public partial class Player
{
    public string Name { get; set; }
    public Stats Stats { get; set; }  // Stats も [DeepClonable] なら自動で深いコピー
}

[DeepClonable]
public partial class Stats
{
    public int HP { get; set; }
    public int MP { get; set; }
}
```

### IDeepCloneable<T> を手動実装

カスタムのクローンロジックが必要な場合、`IDeepCloneable<T>` を直接実装できる。
`[DeepClonable]` を使わず手動実装した型は、他の `[DeepClonable]` 型のプロパティとして
自動的に認識され、`DeepClone()` メソッドが呼ばれる。

```csharp
// 手動実装（[DeepClonable]属性なし）
public class CustomQueue : IDeepCloneable<CustomQueue>
{
    private readonly List<int> _items = new();

    public CustomQueue DeepClone()
    {
        var clone = new CustomQueue();
        // カスタムロジック：偶数のみコピー
        foreach (var item in _items.Where(x => x % 2 == 0))
        {
            clone._items.Add(item);
        }
        return clone;
    }
}

// CustomQueue をプロパティとして使用
[DeepClonable]
public partial class GameState
{
    public string Name { get; set; }
    public CustomQueue EventQueue { get; set; }  // 自動で DeepClone() が呼ばれる
}

var clone = gameState.DeepClone();
// clone.EventQueue は CustomQueue.DeepClone() で作成された独立したインスタンス
```

> **注**: `[DeepClonable]` 属性付きの型は生成された `DeepCloneInternal()` が使われ、
> 手動実装の `IDeepCloneable<T>` は `DeepClone()` が使われる。
> コレクション内の要素（`List<CustomItem>` など）も同様に正しく処理される。

---

## サポートする型

### 値コピー（そのままコピー）

- プリミティブ型（`int`, `float`, `bool` など）
- `enum`
- 値型（`struct`）

### 参照コピー（参照を共有）

- `string`（イミュータブル）
- `DateTime`, `TimeSpan`, `Guid`（イミュータブル）
- `System.Collections.Immutable.*`

### 深いコピー

- 配列（`T[]`, `T[,]`, `T[,,]`, ジャグ配列）
- `List<T>`, `Dictionary<K,V>`, `HashSet<T>`
- `Queue<T>`, `Stack<T>`, `LinkedList<T>`
- `SortedList<K,V>`, `SortedDictionary<K,V>`, `SortedSet<T>`
- `ObservableCollection<T>`, `ReadOnlyCollection<T>`
- `ConcurrentDictionary<K,V>`, `ConcurrentQueue<T>`, `ConcurrentStack<T>`, `ConcurrentBag<T>`
- `[DeepClonable]` 属性付きの型
- `IDeepCloneable<T>` 実装型

---

## 要件

- C# 9.0 以上（LangVersion latest 推奨）
- .NET Standard 2.0 対応（.NET Framework 4.6.1+, .NET Core 2.0+, Unity 2018.1+ 等）
- `partial` 型宣言
- パラメータレスコンストラクタ（public/internal/protected）

---

## ライセンス

MIT License

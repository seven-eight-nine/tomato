# DependencySortSystem

依存関係グラフのトポロジカルソートを行う汎用ライブラリ。

## 概要

`DependencySortSystem`は、ノード間の依存関係を管理し、依存先が先に処理される順序（トポロジカル順序）を計算するための汎用ライブラリです。循環検出とそのパス特定も行えます。

## インストール

```xml
<ProjectReference Include="../DependencySortSystem/DependencySortSystem.Core/DependencySortSystem.Core.csproj" />
```

## クイックスタート

```csharp
using Tomato.DependencySortSystem;

// 1. 依存グラフを作成
var graph = new DependencyGraph<string>();

// 2. 依存関係を追加（fromがtoに依存 = toが先に処理される）
graph.AddDependency("app", "database");
graph.AddDependency("app", "cache");
graph.AddDependency("database", "config");
graph.AddDependency("cache", "config");

// 3. トポロジカルソートを実行
var sorter = new TopologicalSorter<string>();
var result = sorter.Sort(graph.GetAllNodes(), graph);

if (result.Success)
{
    // 処理順序: config -> database -> cache -> app
    foreach (var node in result.SortedOrder!)
    {
        Console.WriteLine(node);
    }
}
else
{
    // 循環検出時
    Console.WriteLine($"循環を検出: {string.Join(" -> ", result.CyclePath!)}");
}
```

## API

### DependencyGraph&lt;TNode&gt;

ノード間の依存関係を管理するグラフ。

```csharp
// 依存関係の追加（fromがtoに依存）
void AddDependency(TNode from, TNode to)

// 依存関係の削除
void RemoveDependency(TNode from, TNode to)

// ノードの削除（関連する依存関係も削除）
void RemoveNode(TNode node)

// 依存先を取得（このノードが依存しているノード）
IReadOnlyList<TNode> GetDependencies(TNode node)

// 依存元を取得（このノードに依存しているノード）
IReadOnlyList<TNode> GetDependents(TNode node)

// 依存関係の有無を確認
bool HasDependencies(TNode node)
bool HasDependents(TNode node)

// 全ノードを取得
IEnumerable<TNode> GetAllNodes()

// グラフをクリア
void Clear()
```

### TopologicalSorter&lt;TNode&gt;

依存グラフをトポロジカルソートするソーター。

```csharp
// ソートを実行
SortResult<TNode> Sort(IEnumerable<TNode> nodes, DependencyGraph<TNode> graph)
```

### SortResult&lt;TNode&gt;

ソート結果を表す構造体。

```csharp
// ソートが成功したか
bool Success { get; }

// ソート結果（成功時のみ有効）
IReadOnlyList<TNode>? SortedOrder { get; }

// 循環パス（循環検出時のみ有効）
IReadOnlyList<TNode>? CyclePath { get; }
```

## カスタム等値比較

独自の等値比較器を使用できます。

```csharp
// 大文字小文字を無視
var graph = new DependencyGraph<string>(StringComparer.OrdinalIgnoreCase);
var sorter = new TopologicalSorter<string>(StringComparer.OrdinalIgnoreCase);

graph.AddDependency("App", "Database");
graph.AddDependency("app", "database");  // 重複として扱われる
```

## 循環検出

循環が存在する場合、`SortResult.CyclePath`に循環を構成するノードのパスが返されます。

```csharp
var graph = new DependencyGraph<string>();
graph.AddDependency("a", "b");
graph.AddDependency("b", "c");
graph.AddDependency("c", "a");  // 循環

var result = sorter.Sort(graph.GetAllNodes(), graph);

if (!result.Success)
{
    // CyclePath: ["a", "b", "c", "a"]
    Console.WriteLine($"循環: {string.Join(" -> ", result.CyclePath!)}");
}
```

## 使用例

### タスク実行順序の決定

```csharp
var graph = new DependencyGraph<string>();
graph.AddDependency("deploy", "build");
graph.AddDependency("build", "test");
graph.AddDependency("build", "lint");
graph.AddDependency("test", "compile");
graph.AddDependency("lint", "compile");

var result = new TopologicalSorter<string>().Sort(graph.GetAllNodes(), graph);
// 実行順序: compile -> test -> lint -> build -> deploy
```

### モジュール初期化順序

```csharp
var graph = new DependencyGraph<Type>();
graph.AddDependency(typeof(AppModule), typeof(DatabaseModule));
graph.AddDependency(typeof(AppModule), typeof(CacheModule));
graph.AddDependency(typeof(DatabaseModule), typeof(ConfigModule));

var result = new TopologicalSorter<Type>().Sort(graph.GetAllNodes(), graph);
foreach (var moduleType in result.SortedOrder!)
{
    // ConfigModule -> DatabaseModule -> CacheModule -> AppModule の順で初期化
    InitializeModule(moduleType);
}
```

## 設計

- **汎用性**: `TNode`型パラメータにより任意の型で利用可能
- **再利用性**: `TopologicalSorter`インスタンスは複数回のソートで再利用可能（内部バッファを再利用）
- **循環検出**: 深さ優先探索（DFS）による効率的な循環検出とパス特定
- **不変性**: ソート結果は新しいリストとして返され、内部状態を公開しない

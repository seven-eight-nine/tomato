namespace Tomato.EntityHandleSystem;

/// <summary>
/// 特定のコンポーネント型へのアクセスを提供する Arena インターフェース。
///
/// <para>このインターフェースは Arena が特定のコンポーネント型を管理していることを示し、
/// AnyHandle や TypedHandle から型安全なコンポーネントアクセスを可能にします。</para>
///
/// <para>Arena は [EntityComponent] で指定されたコンポーネント型ごとに
/// このインターフェースを実装します。</para>
/// </summary>
/// <typeparam name="TComponent">コンポーネントの型</typeparam>
public interface IComponentArena<TComponent>
{
    /// <summary>
    /// 指定されたインデックスと世代番号でコンポーネントに対してアクションを実行します。
    /// ハンドルが有効な場合のみアクションが実行されます。
    ///
    /// <para>このメソッドはスレッドセーフです（内部でロックを取得します）。</para>
    /// </summary>
    /// <param name="index">エンティティのインデックス</param>
    /// <param name="generation">期待される世代番号</param>
    /// <param name="action">コンポーネントに対して実行するアクション</param>
    /// <returns>アクションが実行された場合は true、ハンドルが無効な場合は false</returns>
    bool TryExecuteComponent(int index, int generation, Tomato.HandleSystem.RefAction<TComponent> action);

    /// <summary>
    /// 指定されたインデックスのコンポーネントへの参照を検証なしで取得します。
    ///
    /// <para>警告: このメソッドは有効性チェックを行いません。
    /// 呼び出し側でハンドルの有効性を確認してから使用してください。</para>
    /// </summary>
    /// <param name="index">エンティティのインデックス</param>
    /// <returns>コンポーネントへの参照</returns>
    ref TComponent GetComponentRefUnchecked(int index);
}

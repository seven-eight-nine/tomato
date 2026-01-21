namespace Tomato.EntityHandleSystem;

/// <summary>
/// エンティティハンドルの共通インターフェース。
/// EntityContainerで使用するために必要な最小限のインターフェースを定義します。
/// </summary>
public interface IEntityHandle
{
    /// <summary>
    /// ハンドルが有効かどうかを返します。
    /// 参照先のエンティティが存在し、世代番号が一致する場合にtrueを返します。
    /// </summary>
    bool IsValid { get; }
}

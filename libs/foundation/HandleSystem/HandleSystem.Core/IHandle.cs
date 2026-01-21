namespace Tomato.HandleSystem;

/// <summary>
/// 汎用ハンドルの共通インターフェース。
/// [Handleable]属性で生成されるすべてのハンドル型の基底インターフェースです。
/// </summary>
public interface IHandle
{
    /// <summary>
    /// ハンドルが有効かどうかを返します。
    /// 参照先のオブジェクトが存在し、世代番号が一致する場合にtrueを返します。
    /// </summary>
    bool IsValid { get; }
}

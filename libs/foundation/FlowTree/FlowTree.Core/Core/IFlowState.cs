namespace Tomato.FlowTree;

/// <summary>
/// FlowTreeの状態オブジェクトが実装すべきインターフェース。
/// 親状態への参照を持つことで、サブツリー間で状態を共有できる。
/// </summary>
public interface IFlowState
{
    /// <summary>
    /// 親の状態への参照。
    /// サブツリーで新しい状態を注入した場合、呼び出し元の状態がここに設定される。
    /// ルートの場合はnull。
    /// </summary>
    IFlowState? Parent { get; set; }
}

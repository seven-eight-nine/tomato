namespace Tomato.CommandGenerator;

/// <summary>
/// プール可能なコマンドが実装するインターフェース
/// </summary>
/// <typeparam name="TSelf">実装クラス自身の型</typeparam>
public interface ICommandPoolable<TSelf> where TSelf : class
{
    /// <summary>
    /// フィールドを初期値にリセットする
    /// </summary>
    void ResetToDefault();
}

namespace Tomato.CommandGenerator;

/// <summary>
/// メッセージハンドラ実行用のコマンドキュー。
/// CommandGeneratorの優先度機能とプーリングを活用する。
/// ゲーム側で[Command&lt;MessageHandlerQueue&gt;]属性を使って
/// ゲーム固有のコマンドを定義する。
/// </summary>
[CommandQueue]
public partial class MessageHandlerQueue
{
    [CommandMethod]
    public partial void Execute();
}

using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.SystemPipeline;

/// <summary>
/// メッセージキューシステムのインターフェース。
/// StepProcessorを使用してコマンドキューを処理します。
/// </summary>
public interface IMessageQueueSystem : ISystem
{
    /// <summary>
    /// メッセージキューを処理します。
    /// StepProcessorによるStep処理（収束まで）を実行します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    /// <param name="context">実行コンテキスト</param>
    void ProcessMessages(
        IEntityRegistry registry,
        in SystemContext context);
}

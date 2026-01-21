using System.Collections.Immutable;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドクラスの解析情報
/// </summary>
internal sealed class CommandInfo
{
    /// <summary>
    /// 名前空間
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// クラス名
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// 完全修飾名
    /// </summary>
    public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? ClassName : $"{Namespace}.{ClassName}";

    /// <summary>
    /// 登録先キューの情報リスト
    /// </summary>
    public ImmutableArray<CommandQueueRegistration> QueueRegistrations { get; }

    /// <summary>
    /// リセット対象フィールドのリスト
    /// </summary>
    public ImmutableArray<FieldResetInfo> FieldResets { get; }

    public CommandInfo(
        string ns,
        string className,
        ImmutableArray<CommandQueueRegistration> queueRegistrations,
        ImmutableArray<FieldResetInfo> fieldResets)
    {
        Namespace = ns;
        ClassName = className;
        QueueRegistrations = queueRegistrations;
        FieldResets = fieldResets;
    }
}

/// <summary>
/// コマンドのキュー登録情報
/// </summary>
internal sealed class CommandQueueRegistration
{
    /// <summary>
    /// キュークラスの完全修飾名
    /// </summary>
    public string QueueFullyQualifiedName { get; }

    /// <summary>
    /// キュークラス名（名前空間なし）
    /// </summary>
    public string QueueClassName { get; }

    /// <summary>
    /// インターフェース名
    /// </summary>
    public string InterfaceName => $"I{QueueClassName}";

    /// <summary>
    /// 優先度
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// プール初期容量
    /// </summary>
    public int PoolInitialCapacity { get; }

    /// <summary>
    /// シグナルコマンドかどうか（キューに1つしか入らない）
    /// </summary>
    public bool Signal { get; }

    public CommandQueueRegistration(string queueFullyQualifiedName, string queueClassName, int priority, int poolInitialCapacity, bool signal)
    {
        QueueFullyQualifiedName = queueFullyQualifiedName;
        QueueClassName = queueClassName;
        Priority = priority;
        PoolInitialCapacity = poolInitialCapacity;
        Signal = signal;
    }
}

/// <summary>
/// フィールドリセット情報
/// </summary>
internal sealed class FieldResetInfo
{
    /// <summary>
    /// フィールド名
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// リセットコード
    /// </summary>
    public string ResetCode { get; }

    public FieldResetInfo(string fieldName, string resetCode)
    {
        FieldName = fieldName;
        ResetCode = resetCode;
    }
}

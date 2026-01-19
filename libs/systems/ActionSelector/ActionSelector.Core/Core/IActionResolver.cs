using System;

namespace Tomato.ActionSelector;

/// <summary>
/// 解決されたアクション情報。
///
/// ジャッジメント成立時に、実行するアクションとそのパラメータを保持する。
/// </summary>
/// <remarks>
/// パフォーマンス:
/// - readonly struct で値渡し
/// - Parameter は object? なのでボクシングが発生する場合がある
///   （パフォーマンスクリティカルな場合は専用の構造体を使用）
///
/// 該当アクションなし:
/// - ResolvedAction.None を返すと「該当なし」として扱われる
/// - エンジンは次のジャッジメントの評価を継続する
/// </remarks>
public readonly struct ResolvedAction
{
    /// <summary>
    /// 該当アクションなしを表す値。
    /// </summary>
    public static readonly ResolvedAction None = default;

    /// <summary>
    /// 実行するアクションのラベル。
    /// </summary>
    public readonly string Label;

    /// <summary>
    /// アクションパラメータ（任意）。
    /// </summary>
    public readonly object? Parameter;

    /// <summary>
    /// 該当アクションなしかどうか。
    /// </summary>
    public bool IsNone => Label == null;

    /// <summary>
    /// 解決されたアクションを生成する。
    /// </summary>
    public ResolvedAction(string label, object? parameter = null)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Parameter = parameter;
    }

    /// <summary>
    /// ラベルから暗黙変換。
    /// </summary>
    public static implicit operator ResolvedAction(string label) => new(label);

    /// <summary>
    /// パラメータを指定した型で取得する。
    /// </summary>
    public T? GetParameter<T>() => Parameter is T value ? value : default;

    public override string ToString() =>
        IsNone ? "(None)" : (Parameter != null ? $"{Label}({Parameter})" : Label);
}

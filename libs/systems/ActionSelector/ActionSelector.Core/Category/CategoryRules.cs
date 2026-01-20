using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// カテゴリ間の排他性を定義する抽象クラス。
///
/// カテゴリが排他的な場合、一方が選択されると他方は選択されない。
/// ゲームごとに適切なルールを実装する。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// 設計意図:
/// - 仮想関数によるシンプルな実装（アトリビュートは使用しない）
/// - 実行時に柔軟にルールを変更可能
/// - ゲームごとに適切なルールを定義
///
/// 使用例:
/// <code>
/// public class MyCategoryRules : CategoryRules&lt;MyCategory&gt;
/// {
///     public override bool AreExclusive(MyCategory a, MyCategory b)
///     {
///         // FullBody は他のすべてと排他
///         if (a == MyCategory.FullBody || b == MyCategory.FullBody)
///             return true;
///         return a == b;
///     }
/// }
/// </code>
/// </remarks>
public abstract class CategoryRules<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// 2つのカテゴリが排他的かどうかを返す。
    /// </summary>
    /// <param name="a">カテゴリA</param>
    /// <param name="b">カテゴリB</param>
    /// <returns>
    /// true: 一方が選択されると他方は選択されない
    /// false: 両方同時に選択可能
    /// </returns>
    /// <remarks>
    /// 同一カテゴリ（a == b）の場合も通常は true を返す。
    /// ただし、同一カテゴリでも複数選択を許可する場合は false を返す。
    /// </remarks>
    public abstract bool AreExclusive(TCategory a, TCategory b);

    /// <summary>
    /// 指定カテゴリと排他的な全カテゴリを返す。
    /// </summary>
    /// <param name="category">対象カテゴリ</param>
    /// <returns>排他的なカテゴリの列挙（自身を含む場合あり）</returns>
    /// <remarks>
    /// デバッグやUI表示、ルール検証に使用。
    /// デフォルト実装は全カテゴリをスキャンするため、
    /// パフォーマンスが重要な場合はオーバーライドすること。
    /// </remarks>
    public virtual IEnumerable<TCategory> GetExclusiveCategories(TCategory category)
    {
        var allCategories = (TCategory[])Enum.GetValues(typeof(TCategory));
        foreach (var other in allCategories)
        {
            if (AreExclusive(category, other))
            {
                yield return other;
            }
        }
    }

    /// <summary>
    /// 全カテゴリの排他関係をまとめた情報を返す。
    /// </summary>
    /// <returns>カテゴリと排他カテゴリ群のペア</returns>
    /// <remarks>
    /// ルールのデバッグ・可視化に使用。
    /// </remarks>
    public Dictionary<TCategory, List<TCategory>> GetExclusivityMap()
    {
        var map = new Dictionary<TCategory, List<TCategory>>();
        var allCategories = (TCategory[])Enum.GetValues(typeof(TCategory));

        foreach (var cat in allCategories)
        {
            var exclusives = new List<TCategory>();
            foreach (var other in allCategories)
            {
                if (!cat.Equals(other) && AreExclusive(cat, other))
                {
                    exclusives.Add(other);
                }
            }
            if (exclusives.Count > 0)
            {
                map[cat] = exclusives;
            }
        }

        return map;
    }
}

/// <summary>
/// 排他性なし（デフォルト）。同一カテゴリのみ排他。
///
/// 最もシンプルなルール。各カテゴリは独立して動作する。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public class NoExclusivityRules<TCategory> : CategoryRules<TCategory>
    where TCategory : struct, Enum
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly NoExclusivityRules<TCategory> Instance = new();

    private NoExclusivityRules() { }

    /// <summary>
    /// 同一カテゴリの場合のみ排他。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool AreExclusive(TCategory a, TCategory b) => a.Equals(b);
}

/// <summary>
/// 完全排他ルール。すべてのカテゴリが互いに排他。
///
/// カテゴリが1つしか選択されないゲーム向け。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public class FullExclusivityRules<TCategory> : CategoryRules<TCategory>
    where TCategory : struct, Enum
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly FullExclusivityRules<TCategory> Instance = new();

    private FullExclusivityRules() { }

    /// <summary>
    /// 常に排他。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool AreExclusive(TCategory a, TCategory b) => true;
}

// ===========================================
// 標準カテゴリ定義
// ===========================================

/// <summary>
/// アクションゲーム用標準カテゴリ。
/// </summary>
public enum V5ActionCategory
{
    /// <summary>
    /// 全身アクション（他のすべてと排他）。
    /// </summary>
    FullBody = 0,

    /// <summary>
    /// 上半身アクション。
    /// </summary>
    UpperBody = 1,

    /// <summary>
    /// 下半身アクション。
    /// </summary>
    LowerBody = 2,

    /// <summary>
    /// 左手アクション。
    /// </summary>
    LeftHand = 3,

    /// <summary>
    /// 右手アクション。
    /// </summary>
    RightHand = 4,
}

/// <summary>
/// アクションゲーム用カテゴリルール。
///
/// FullBody は他のすべてと排他。
/// UpperBody/LowerBody/LeftHand/RightHand は独立。
/// </summary>
public sealed class V5ActionCategoryRules : CategoryRules<V5ActionCategory>
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly V5ActionCategoryRules Instance = new();

    private V5ActionCategoryRules() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool AreExclusive(V5ActionCategory a, V5ActionCategory b)
    {
        // 同一カテゴリは常に排他
        if (a == b)
            return true;

        // FullBody は他のすべてと排他
        if (a == V5ActionCategory.FullBody || b == V5ActionCategory.FullBody)
            return true;

        // その他は独立
        return false;
    }
}

/// <summary>
/// RTS用カテゴリ。
/// </summary>
public enum RTSCategory
{
    /// <summary>
    /// ユニット制御。
    /// </summary>
    UnitControl = 0,

    /// <summary>
    /// 建物制御。
    /// </summary>
    BuildControl = 1,

    /// <summary>
    /// リソース制御。
    /// </summary>
    ResourceControl = 2,

    /// <summary>
    /// コマンドキュー。
    /// </summary>
    CommandQueue = 3,

    /// <summary>
    /// UI制御。
    /// </summary>
    UIControl = 4,
}

/// <summary>
/// RTS用カテゴリルール。
///
/// 各カテゴリは独立して動作（同一カテゴリのみ排他）。
/// </summary>
public sealed class RTSCategoryRules : CategoryRules<RTSCategory>
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly RTSCategoryRules Instance = new();

    private RTSCategoryRules() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool AreExclusive(RTSCategory a, RTSCategory b) => a == b;
}

/// <summary>
/// VR用カテゴリ。
/// </summary>
public enum VRCategory
{
    /// <summary>
    /// 左手。
    /// </summary>
    LeftHand = 0,

    /// <summary>
    /// 右手。
    /// </summary>
    RightHand = 1,

    /// <summary>
    /// 頭部。
    /// </summary>
    Head = 2,

    /// <summary>
    /// 音声。
    /// </summary>
    Voice = 3,

    /// <summary>
    /// 視線。
    /// </summary>
    Gaze = 4,
}

/// <summary>
/// VR用カテゴリルール。
///
/// 各カテゴリは独立して動作（同一カテゴリのみ排他）。
/// </summary>
public sealed class VRCategoryRules : CategoryRules<VRCategory>
{
    /// <summary>
    /// シングルトンインスタンス。
    /// </summary>
    public static readonly VRCategoryRules Instance = new();

    private VRCategoryRules() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool AreExclusive(VRCategory a, VRCategory b) => a == b;
}

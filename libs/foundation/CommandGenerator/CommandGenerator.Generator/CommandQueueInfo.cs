using System.Collections.Immutable;
using System.Linq;

namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドキューの解析情報
/// </summary>
internal sealed class CommandQueueInfo
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
    /// 生成するインターフェース名（I + クラス名）
    /// </summary>
    public string InterfaceName => $"I{ClassName}";

    /// <summary>
    /// 完全修飾名
    /// </summary>
    public string FullyQualifiedName => string.IsNullOrEmpty(Namespace) ? ClassName : $"{Namespace}.{ClassName}";

    /// <summary>
    /// CommandMethodの一覧
    /// </summary>
    public ImmutableArray<CommandMethodInfo> Methods { get; }

    public CommandQueueInfo(string ns, string className, ImmutableArray<CommandMethodInfo> methods)
    {
        Namespace = ns;
        ClassName = className;
        Methods = methods;
    }
}

/// <summary>
/// CommandMethodの解析情報
/// </summary>
internal sealed class CommandMethodInfo
{
    /// <summary>
    /// メソッド名
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// パラメータリスト（型と名前のペア）
    /// </summary>
    public ImmutableArray<(string Type, string Name)> Parameters { get; }

    /// <summary>
    /// Clear属性（trueの場合、実行後にキュークリア＆プール返却）
    /// </summary>
    public bool Clear { get; }

    public CommandMethodInfo(string methodName, ImmutableArray<(string Type, string Name)> parameters, bool clear)
    {
        MethodName = methodName;
        Parameters = parameters;
        Clear = clear;
    }

    /// <summary>
    /// インターフェースメソッドのシグネチャを生成
    /// </summary>
    public string GetInterfaceMethodSignature()
    {
        var paramList = string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"void {MethodName}({paramList});";
    }

    /// <summary>
    /// パラメータリスト文字列を生成
    /// </summary>
    public string GetParameterList()
    {
        return string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"));
    }

    /// <summary>
    /// 引数リスト文字列を生成
    /// </summary>
    public string GetArgumentList()
    {
        return string.Join(", ", Parameters.Select(p => p.Name));
    }
}

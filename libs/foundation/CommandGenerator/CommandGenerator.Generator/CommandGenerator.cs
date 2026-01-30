using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tomato.CommandGenerator;

/// <summary>
/// CommandGenerator Source Generator本体
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CommandGenerator : IIncrementalGenerator
{
    private const string CommandQueueAttributeName = "Tomato.CommandGenerator.CommandQueueAttribute";
    private const string CommandMethodAttributeName = "Tomato.CommandGenerator.CommandMethodAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // CommandQueue属性を持つクラスを検索
        var commandQueueProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCommandQueueCandidate(node),
                transform: static (ctx, ct) => GetCommandQueueInfo(ctx, ct))
            .Where(static info => info != null)
            .Select(static (info, _) => info!);

        // Command属性を持つクラスを検索
        var commandProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCommandCandidate(node),
                transform: static (ctx, ct) => GetCommandInfo(ctx, ct))
            .Where(static info => info != null)
            .Select(static (info, _) => info!);

        // キューコード生成
        context.RegisterSourceOutput(commandQueueProvider, GenerateCommandQueueCode);

        // コマンドコード生成
        context.RegisterSourceOutput(commandProvider, GenerateCommandCode);
    }

    #region Predicate Methods

    private static bool IsCommandQueueCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl &&
               classDecl.AttributeLists.Count > 0;
    }

    private static bool IsCommandCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl &&
               classDecl.AttributeLists.Count > 0;
    }

    #endregion

    #region Transform Methods

    private static CommandQueueInfo? GetCommandQueueInfo(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol == null)
            return null;

        // partialクラスでなければスキップ
        if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        // CommandQueue属性をチェック
        var hasCommandQueueAttr = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() == CommandQueueAttributeName);

        if (!hasCommandQueueAttr)
            return null;

        // メソッドを解析
        var methods = ImmutableArray.CreateBuilder<CommandMethodInfo>();

        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax methodDecl)
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, ct);
                if (methodSymbol == null)
                    continue;

                var commandMethodAttr = methodSymbol.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == CommandMethodAttributeName);

                if (commandMethodAttr != null)
                {
                    // Clear引数を取得（デフォルトはtrue）
                    bool clear = true;
                    if (commandMethodAttr.ConstructorArguments.Length > 0)
                    {
                        clear = (bool)commandMethodAttr.ConstructorArguments[0].Value!;
                    }

                    // パラメータを解析
                    var parameters = ImmutableArray.CreateBuilder<(string Type, string Name)>();
                    foreach (var param in methodSymbol.Parameters)
                    {
                        parameters.Add((param.Type.ToDisplayString(), param.Name));
                    }

                    methods.Add(new CommandMethodInfo(
                        methodSymbol.Name,
                        parameters.ToImmutable(),
                        clear));
                }
            }
        }

        if (methods.Count == 0)
            return null;

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : classSymbol.ContainingNamespace.ToDisplayString();

        return new CommandQueueInfo(ns, classSymbol.Name, methods.ToImmutable());
    }

    private static CommandInfo? GetCommandInfo(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);

        if (classSymbol == null)
            return null;

        // Command<T>属性をチェック
        var commandAttrs = classSymbol.GetAttributes()
            .Where(attr =>
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null || !attrClass.IsGenericType)
                    return false;

                var originalDef = attrClass.OriginalDefinition;
                // Use fully qualified name check - "Tomato.CommandGenerator.CommandAttribute"
                var containingNamespace = originalDef.ContainingNamespace?.ToDisplayString() ?? "";
                var typeName = originalDef.Name;
                return containingNamespace == "Tomato.CommandGenerator" && typeName == "CommandAttribute";
            })
            .ToList();

        if (commandAttrs.Count == 0)
            return null;

        // キュー登録情報を解析
        var registrations = ImmutableArray.CreateBuilder<CommandQueueRegistration>();

        foreach (var attr in commandAttrs)
        {
            var queueType = attr.AttributeClass!.TypeArguments[0];
            var queueClassName = queueType.Name;
            var queueFullName = queueType.ToDisplayString();

            // Priority, PoolInitialCapacity, Signal を取得
            int priority = 0;
            int poolInitialCapacity = 8;
            bool signal = false;

            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Priority":
                        priority = (int)namedArg.Value.Value!;
                        break;
                    case "PoolInitialCapacity":
                        poolInitialCapacity = (int)namedArg.Value.Value!;
                        break;
                    case "Signal":
                        signal = (bool)namedArg.Value.Value!;
                        break;
                }
            }

            registrations.Add(new CommandQueueRegistration(
                queueFullName,
                queueClassName,
                priority,
                poolInitialCapacity,
                signal));
        }

        // フィールドリセット情報を解析
        var fieldResets = AnalyzeFieldsForReset(classDecl, semanticModel, ct);

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : classSymbol.ContainingNamespace.ToDisplayString();

        return new CommandInfo(ns, classSymbol.Name, registrations.ToImmutable(), fieldResets);
    }

    private static ImmutableArray<FieldResetInfo> AnalyzeFieldsForReset(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var fieldResets = ImmutableArray.CreateBuilder<FieldResetInfo>();

        foreach (var member in classDecl.Members)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                // static, const, readonlyは除外
                if (fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                    fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                    fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                {
                    continue;
                }

                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    // フィールドシンボルを取得して型情報を得る
                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, ct) as IFieldSymbol;
                    if (fieldSymbol == null)
                        continue;

                    var fieldName = fieldSymbol.Name;
                    var typeSymbol = fieldSymbol.Type;

                    var resetCode = GenerateResetCode(fieldName, typeSymbol, variable.Initializer);
                    if (resetCode != null)
                    {
                        fieldResets.Add(new FieldResetInfo(fieldName, resetCode));
                    }
                }
            }
        }

        return fieldResets.ToImmutable();
    }

    private static string? GenerateResetCode(string fieldName, ITypeSymbol typeSymbol, EqualsValueClauseSyntax? initializer)
    {
        var typeName = typeSymbol.ToDisplayString();

        // コレクション型の判定
        if (IsCollectionType(typeSymbol, out var collectionKind))
        {
            switch (collectionKind)
            {
                case CollectionKind.List:
                case CollectionKind.HashSet:
                case CollectionKind.Dictionary:
                case CollectionKind.Queue:
                case CollectionKind.Stack:
                case CollectionKind.Collection:
                    return $"{fieldName}.Clear();";
                case CollectionKind.Array:
                    return $"System.Array.Clear({fieldName}, 0, {fieldName}.Length);";
            }
        }

        // 初期化子がある場合はその値を使用
        if (initializer != null)
        {
            var initValue = initializer.Value.ToString();
            return $"{fieldName} = {initValue};";
        }

        // 値型のデフォルト値
        if (typeSymbol.IsValueType)
        {
            return $"{fieldName} = default;";
        }

        // 参照型はnull
        return $"{fieldName} = default!;";
    }

    private enum CollectionKind
    {
        None,
        List,
        HashSet,
        Dictionary,
        Queue,
        Stack,
        Array,
        Collection
    }

    private static bool IsCollectionType(ITypeSymbol typeSymbol, out CollectionKind kind)
    {
        kind = CollectionKind.None;

        if (typeSymbol is IArrayTypeSymbol)
        {
            kind = CollectionKind.Array;
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var originalDef = namedType.OriginalDefinition.ToDisplayString();

            if (originalDef.StartsWith("System.Collections.Generic.List<"))
            {
                kind = CollectionKind.List;
                return true;
            }
            if (originalDef.StartsWith("System.Collections.Generic.HashSet<"))
            {
                kind = CollectionKind.HashSet;
                return true;
            }
            if (originalDef.StartsWith("System.Collections.Generic.Dictionary<"))
            {
                kind = CollectionKind.Dictionary;
                return true;
            }
            if (originalDef.StartsWith("System.Collections.Generic.Queue<"))
            {
                kind = CollectionKind.Queue;
                return true;
            }
            if (originalDef.StartsWith("System.Collections.Generic.Stack<"))
            {
                kind = CollectionKind.Stack;
                return true;
            }

            // ICollection<T>を実装しているかチェック
            foreach (var iface in namedType.AllInterfaces)
            {
                if (iface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>")
                {
                    kind = CollectionKind.Collection;
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Code Generation

    private static void GenerateCommandQueueCode(SourceProductionContext context, CommandQueueInfo info)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Tomato.CommandGenerator;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace}");
            sb.AppendLine("{");
        }

        // インターフェース生成
        GenerateQueueInterface(sb, info);

        sb.AppendLine();

        // キュー実装生成
        GenerateQueueImplementation(sb, info);

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{info.ClassName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));

        // MessageSystem自動生成
        GenerateMessageSystemCode(context, info);
    }

    private static void GenerateMessageSystemCode(SourceProductionContext context, CommandQueueInfo info)
    {
        var sb = new StringBuilder();
        var systemClassName = $"{info.ClassName}System";
        var indent = string.IsNullOrEmpty(info.Namespace) ? "" : "    ";
        var indent2 = indent + "    ";
        var indent3 = indent2 + "    ";

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Tomato.CommandGenerator;");
        sb.AppendLine("using Tomato.SystemPipeline;");
        sb.AppendLine("using Tomato.SystemPipeline.Query;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// {info.ClassName} を処理するMessageQueueSystem。");
        sb.AppendLine($"{indent}/// StepProcessorを内蔵し、コマンドの収束まで処理を実行します。");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public sealed class {systemClassName} : IMessageQueueSystem");
        sb.AppendLine($"{indent}{{");

        // Fields
        sb.AppendLine($"{indent2}private readonly {info.ClassName} _queue;");
        sb.AppendLine($"{indent2}private readonly StepProcessor _stepProcessor;");
        sb.AppendLine();

        // Properties
        sb.AppendLine($"{indent2}/// <inheritdoc/>");
        sb.AppendLine($"{indent2}public bool IsEnabled {{ get; set; }} = true;");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <inheritdoc/>");
        sb.AppendLine($"{indent2}public IEntityQuery? Query => null;");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>現在のStep深度</summary>");
        sb.AppendLine($"{indent2}public int CurrentStepDepth => _stepProcessor.CurrentStepDepth;");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>キューを取得します</summary>");
        sb.AppendLine($"{indent2}public {info.ClassName} Queue => _queue;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// {systemClassName}を生成します。");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}/// <param name=\"queue\">処理対象のキュー</param>");
        sb.AppendLine($"{indent2}/// <param name=\"maxStepDepth\">最大Step深度（デフォルト100）</param>");
        sb.AppendLine($"{indent2}public {systemClassName}({info.ClassName} queue, int maxStepDepth = 100)");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}_queue = queue ?? throw new ArgumentNullException(nameof(queue));");
        sb.AppendLine($"{indent3}_stepProcessor = new StepProcessor(maxStepDepth);");
        sb.AppendLine($"{indent3}_stepProcessor.Register(_queue);");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();

        // ProcessMessages method
        sb.AppendLine($"{indent2}/// <inheritdoc/>");
        sb.AppendLine($"{indent2}public void ProcessMessages(IEntityRegistry registry, in SystemContext context)");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}_stepProcessor.BeginFrame();");

        // Generate execute call based on methods
        if (info.Methods.Length > 0)
        {
            var method = info.Methods[0]; // 最初のメソッドを使用
            var parameters = method.Parameters;
            if (parameters.Length > 0)
            {
                // 引数付きメソッド（Entity単位の処理）
                sb.AppendLine($"{indent3}var entities = registry.GetAllEntities();");
                sb.AppendLine($"{indent3}foreach (var entity in entities)");
                sb.AppendLine($"{indent3}{{");
                sb.AppendLine($"{indent3}    _queue.{method.MethodName}(entity);");
                sb.AppendLine($"{indent3}}}");
            }
            else
            {
                // 引数なしメソッド（Step収束まで処理）
                sb.AppendLine($"{indent3}_stepProcessor.ProcessAllSteps(_ => _queue.{method.MethodName}());");
            }
        }
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine($"{indent}}}");

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{systemClassName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GenerateQueueInterface(StringBuilder sb, CommandQueueInfo info)
    {
        var indent = string.IsNullOrEmpty(info.Namespace) ? "" : "    ";

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// {info.ClassName} に登録可能なコマンドが実装すべきインターフェース");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public interface {info.InterfaceName}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>このキューでの優先度</summary>");
        sb.AppendLine($"{indent}    int Priority {{ get; }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>シグナルコマンドかどうか（キューに1つしか入らない）</summary>");
        sb.AppendLine($"{indent}    bool IsSignal {{ get; }}");
        sb.AppendLine();

        foreach (var method in info.Methods)
        {
            sb.AppendLine($"{indent}    {method.GetInterfaceMethodSignature()}");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateQueueImplementation(StringBuilder sb, CommandQueueInfo info)
    {
        var indent = string.IsNullOrEmpty(info.Namespace) ? "" : "    ";
        var indent2 = indent + "    ";
        var indent3 = indent2 + "    ";
        var indent4 = indent3 + "    ";

        sb.AppendLine($"{indent}public partial class {info.ClassName} : IStepProcessable");
        sb.AppendLine($"{indent}{{");

        // Queue Storage
        sb.AppendLine($"{indent2}#region Queue Storage");
        sb.AppendLine();

        // QueueEntry構造体
        sb.AppendLine($"{indent2}// 優先度付きエントリ");
        sb.AppendLine($"{indent2}private readonly struct QueueEntry : IComparable<QueueEntry>");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}public readonly {info.InterfaceName} Command;");
        sb.AppendLine($"{indent3}public readonly int Priority;");
        sb.AppendLine($"{indent3}public readonly long Sequence;");
        sb.AppendLine($"{indent3}public readonly Action<{info.InterfaceName}> ReturnAction;");
        sb.AppendLine();
        sb.AppendLine($"{indent3}public QueueEntry(");
        sb.AppendLine($"{indent4}{info.InterfaceName} command,");
        sb.AppendLine($"{indent4}int priority,");
        sb.AppendLine($"{indent4}long sequence,");
        sb.AppendLine($"{indent4}Action<{info.InterfaceName}> returnAction)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}Command = command;");
        sb.AppendLine($"{indent4}Priority = priority;");
        sb.AppendLine($"{indent4}Sequence = sequence;");
        sb.AppendLine($"{indent4}ReturnAction = returnAction;");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// 優先度降順、同優先度はSequence昇順（先着順）");
        sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent3}public int CompareTo(QueueEntry other)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}int cmp = other.Priority.CompareTo(Priority);");
        sb.AppendLine($"{indent4}if (cmp != 0) return cmp;");
        sb.AppendLine($"{indent4}return Sequence.CompareTo(other.Sequence);");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();

        // キュー関連フィールド
        sb.AppendLine($"{indent2}// トリプルバッファリング：現在実行中、次Step用、次フレーム用");
        sb.AppendLine($"{indent2}private List<QueueEntry> _currentQueue = new(64);");
        sb.AppendLine($"{indent2}private List<QueueEntry> _pendingQueue = new(64);");
        sb.AppendLine($"{indent2}private List<QueueEntry> _nextFrameQueue = new(64);");
        sb.AppendLine($"{indent2}private readonly object _pendingLock = new();");
        sb.AppendLine($"{indent2}private long _sequenceCounter = 0;");
        sb.AppendLine($"{indent2}private bool _isExecuting = false;");
        sb.AppendLine();
        sb.AppendLine($"{indent2}// シグナルコマンド追跡（同一タイプは1つしかキューに入らない）");
        sb.AppendLine($"{indent2}private readonly HashSet<Type> _signalTypes = new();");
        sb.AppendLine();
        sb.AppendLine($"{indent2}// IStepProcessable: Enqueue時のコールバック");
        sb.AppendLine($"{indent2}public Action<IStepProcessable>? OnEnqueue {{ get; set; }}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // Enqueue
        sb.AppendLine($"{indent2}#region Enqueue（マルチスレッドセーフ）");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// コマンドをキューに追加する（マルチスレッドセーフ）");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}/// <param name=\"initializer\">コマンドの初期化処理</param>");
        sb.AppendLine($"{indent2}/// <param name=\"timing\">実行タイミング（デフォルト: NextStep）</param>");
        sb.AppendLine($"{indent2}/// <returns>エンキューが成功したかどうか（Signalコマンドで既に同タイプがある場合はfalse）</returns>");
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}public bool Enqueue<TCommand>(Action<TCommand> initializer, EnqueueTiming timing = EnqueueTiming.NextStep)");
        sb.AppendLine($"{indent3}where TCommand : class, {info.InterfaceName}, ICommandPoolable<TCommand>, new()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}// プールから取得");
        sb.AppendLine($"{indent3}var command = CommandPool<TCommand>.Rent();");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// 初期化");
        sb.AppendLine($"{indent3}initializer(command);");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// 優先度取得（インターフェース経由）");
        sb.AppendLine($"{indent3}int priority = command.Priority;");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// キューに追加");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}// シグナルコマンドの場合、既に同タイプがあれば無視");
        sb.AppendLine($"{indent4}if (command.IsSignal)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    if (!_signalTypes.Add(typeof(TCommand)))");
        sb.AppendLine($"{indent4}    {{");
        sb.AppendLine($"{indent4}        // 既に同タイプのシグナルコマンドがある→プールに返却して終了");
        sb.AppendLine($"{indent4}        CommandPool<TCommand>.Return(command);");
        sb.AppendLine($"{indent4}        return false;");
        sb.AppendLine($"{indent4}    }}");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent4}long seq = _sequenceCounter++;");
        sb.AppendLine($"{indent4}var entry = new QueueEntry(");
        sb.AppendLine($"{indent4}    command,");
        sb.AppendLine($"{indent4}    priority,");
        sb.AppendLine($"{indent4}    seq,");
        sb.AppendLine($"{indent4}    static cmd => CommandPool<TCommand>.Return((TCommand)cmd)");
        sb.AppendLine($"{indent4});");
        sb.AppendLine();
        sb.AppendLine($"{indent4}if (timing == EnqueueTiming.NextFrame)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _nextFrameQueue.Add(entry);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent4}else");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _pendingQueue.Add(entry);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// StepProcessorに通知");
        sb.AppendLine($"{indent3}OnEnqueue?.Invoke(this);");
        sb.AppendLine($"{indent3}return true;");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // 各CommandMethodの実装
        foreach (var method in info.Methods)
        {
            GenerateCommandMethodImplementation(sb, info, method, indent2);
        }

        // Internal Methods
        sb.AppendLine($"{indent2}#region Internal Methods");
        sb.AppendLine();
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}private void PrepareExecution()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}// Pendingキューをswap（アロケーション回避）");
        sb.AppendLine($"{indent3}// pendingが空なら何もしない（MergePendingToCurrentStep後の重複呼び出し対策）");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}if (_pendingQueue.Count > 0)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    (_currentQueue, _pendingQueue) = (_pendingQueue, _currentQueue);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// ソート（優先度降順、同優先度は先着順）");
        sb.AppendLine($"{indent3}if (_currentQueue.Count > 1)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_currentQueue.Sort();");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}_isExecuting = true;");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}private void ClearAndReturnToPool()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}for (int i = 0; i < _currentQueue.Count; i++)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_currentQueue[i].ReturnAction(_currentQueue[i].Command);");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent3}_currentQueue.Clear();");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// シグナル追跡をクリア（次のEnqueueでシグナルコマンドを受け付けるため）");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_signalTypes.Clear();");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}_isExecuting = false;");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // Utility
        sb.AppendLine($"{indent2}#region Utility");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// 現在キューにあるコマンド数を取得（全キュー合計）");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}public int Count");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent3}get");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}lock (_pendingLock)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    return _currentQueue.Count + _pendingQueue.Count + _nextFrameQueue.Count;");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// 次フレームキュー内のコマンド数を取得");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}public int NextFrameCount");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent3}get");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}lock (_pendingLock)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    return _nextFrameQueue.Count;");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// Pending/NextFrameキューをクリア（実行中でも安全）");
        sb.AppendLine($"{indent2}/// 次回実行予定のコマンドのみ破棄し、現在実行中のキューには影響しない");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}public void Clear()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}// Pendingキューをクリア");
        sb.AppendLine($"{indent4}for (int i = 0; i < _pendingQueue.Count; i++)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _pendingQueue[i].ReturnAction(_pendingQueue[i].Command);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent4}_pendingQueue.Clear();");
        sb.AppendLine();
        sb.AppendLine($"{indent4}// NextFrameキューをクリア");
        sb.AppendLine($"{indent4}for (int i = 0; i < _nextFrameQueue.Count; i++)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _nextFrameQueue[i].ReturnAction(_nextFrameQueue[i].Command);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent4}_nextFrameQueue.Clear();");
        sb.AppendLine();
        sb.AppendLine($"{indent4}// シグナル追跡をクリア");
        sb.AppendLine($"{indent4}_signalTypes.Clear();");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// 全キューを強制クリア（実行中のキューも含む）");
        sb.AppendLine($"{indent2}/// シーン遷移やエラー発生時など、即座に全コマンドを破棄したい場合に使用");
        sb.AppendLine($"{indent2}/// 注意: 実行中に呼び出すと、以降のコマンドはスキップされる");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}public void ForceClear()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}// Pending/NextFrameキューをクリア");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}for (int i = 0; i < _pendingQueue.Count; i++)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _pendingQueue[i].ReturnAction(_pendingQueue[i].Command);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent4}_pendingQueue.Clear();");
        sb.AppendLine();
        sb.AppendLine($"{indent4}for (int i = 0; i < _nextFrameQueue.Count; i++)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    _nextFrameQueue[i].ReturnAction(_nextFrameQueue[i].Command);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent4}_nextFrameQueue.Clear();");
        sb.AppendLine();
        sb.AppendLine($"{indent4}// シグナル追跡をクリア");
        sb.AppendLine($"{indent4}_signalTypes.Clear();");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// 現在のキューもクリア（実行中でも強制）");
        sb.AppendLine($"{indent3}for (int i = 0; i < _currentQueue.Count; i++)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_currentQueue[i].ReturnAction(_currentQueue[i].Command);");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent3}_currentQueue.Clear();");
        sb.AppendLine($"{indent3}_isExecuting = false;");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // IStepProcessable実装
        sb.AppendLine($"{indent2}#region IStepProcessable Implementation");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// 処理待ちのコマンドがあるかどうか");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}public bool HasPendingCommands");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent3}get");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}lock (_pendingLock)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    return _currentQueue.Count > 0 || _pendingQueue.Count > 0;");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// PendingQueueをCurrentQueueにswapする");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}public void MergePendingToCurrentStep()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}// swap: アロケーション回避（pendingが空なら何もしない）");
        sb.AppendLine($"{indent4}if (_pendingQueue.Count > 0)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    (_currentQueue, _pendingQueue) = (_pendingQueue, _currentQueue);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent3}// ソート（優先度降順、同優先度は先着順）");
        sb.AppendLine($"{indent3}if (_currentQueue.Count > 1)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_currentQueue.Sort();");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// NextFrameQueueをPendingQueueにswapする");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}public void MergeNextFrameToPending()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}lock (_pendingLock)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}// swap: アロケーション回避（nextFrameが空なら何もしない）");
        sb.AppendLine($"{indent4}if (_nextFrameQueue.Count > 0)");
        sb.AppendLine($"{indent4}{{");
        sb.AppendLine($"{indent4}    (_pendingQueue, _nextFrameQueue) = (_nextFrameQueue, _pendingQueue);");
        sb.AppendLine($"{indent4}}}");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateCommandMethodImplementation(
        StringBuilder sb,
        CommandQueueInfo queueInfo,
        CommandMethodInfo methodInfo,
        string indent)
    {
        var indent2 = indent + "    ";
        var indent3 = indent2 + "    ";
        var indent4 = indent3 + "    ";

        sb.AppendLine($"{indent}#region {methodInfo.MethodName}（単一スレッド）");
        sb.AppendLine();
        sb.AppendLine($"{indent}public partial void {methodInfo.MethodName}({methodInfo.GetParameterList()})");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent2}PrepareExecution();");
        sb.AppendLine();
        sb.AppendLine($"{indent2}try");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}// ForceClear対応: ループ中にキューがクリアされる可能性を考慮");
        sb.AppendLine($"{indent3}int count = _currentQueue.Count;");
        sb.AppendLine($"{indent3}for (int i = 0; i < count && i < _currentQueue.Count; i++)");
        sb.AppendLine($"{indent3}{{");
        sb.AppendLine($"{indent4}_currentQueue[i].Command.{methodInfo.MethodName}({methodInfo.GetArgumentList()});");
        sb.AppendLine($"{indent3}}}");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine($"{indent2}finally");
        sb.AppendLine($"{indent2}{{");

        if (methodInfo.Clear)
        {
            sb.AppendLine($"{indent3}// clear: true なのでクリア＆返却");
            sb.AppendLine($"{indent3}ClearAndReturnToPool();");
        }
        else
        {
            sb.AppendLine($"{indent3}// clear: false なので何もしない");
            sb.AppendLine($"{indent3}_isExecuting = false;");
        }

        sb.AppendLine($"{indent2}}}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}#endregion");
        sb.AppendLine();
    }

    private static void GenerateCommandCode(SourceProductionContext context, CommandInfo info)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Tomato.CommandGenerator;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine($"namespace {info.Namespace}");
            sb.AppendLine("{");
        }

        var indent = string.IsNullOrEmpty(info.Namespace) ? "" : "    ";
        var indent2 = indent + "    ";
        var indent3 = indent2 + "    ";

        // プール初期化子クラス（静的コンストラクタで初期化）
        int maxPoolCapacity = info.QueueRegistrations.Max(r => r.PoolInitialCapacity);
        sb.AppendLine($"{indent}// プール初期容量設定（静的コンストラクタで設定）");
        sb.AppendLine($"{indent}internal static class {info.ClassName}PoolInitializer");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent2}static {info.ClassName}PoolInitializer()");
        sb.AppendLine($"{indent2}{{");
        sb.AppendLine($"{indent3}// 複数キューに登録されている場合、最大値を使用");
        sb.AppendLine($"{indent3}CommandPoolConfig<{info.ClassName}>.InitialCapacity = {maxPoolCapacity};");
        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}// 静的コンストラクタを確実に呼び出すためのダミーメソッド");
        sb.AppendLine($"{indent2}[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine($"{indent2}internal static void EnsureInitialized() {{ }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        // インターフェースリストを生成
        var interfaces = new List<string>();
        foreach (var reg in info.QueueRegistrations)
        {
            // 同じ名前空間の場合はインターフェース名のみ、異なる場合は完全修飾名
            var queueNs = GetNamespace(reg.QueueFullyQualifiedName);
            if (queueNs == info.Namespace)
            {
                interfaces.Add(reg.InterfaceName);
            }
            else
            {
                interfaces.Add($"{queueNs}.{reg.InterfaceName}");
            }
        }
        interfaces.Add($"ICommandPoolable<{info.ClassName}>");

        var interfaceList = string.Join(", ", interfaces);

        // partialクラス生成
        sb.AppendLine($"{indent}public partial class {info.ClassName} : {interfaceList}");
        sb.AppendLine($"{indent}{{");

        // 優先度プロパティ（インターフェース明示的実装）
        sb.AppendLine($"{indent2}#region Queue Priority（インターフェース実装）");
        sb.AppendLine();

        // 複数キュー対応：PriorityFor{QueueClassName} プロパティを生成
        foreach (var reg in info.QueueRegistrations)
        {
            sb.AppendLine($"{indent2}/// <summary>{reg.QueueClassName} での優先度</summary>");
            sb.AppendLine($"{indent2}public int PriorityFor{reg.QueueClassName}");
            sb.AppendLine($"{indent2}{{");
            sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent3}get => {reg.Priority};");
            sb.AppendLine($"{indent2}}}");
            sb.AppendLine();
        }

        // インターフェース明示的実装（PriorityFor{QueueClassName}を参照）
        foreach (var reg in info.QueueRegistrations)
        {
            // 同じ名前空間の場合はインターフェース名のみ、異なる場合は完全修飾名
            var queueNs = GetNamespace(reg.QueueFullyQualifiedName);
            var interfaceName = queueNs == info.Namespace
                ? reg.InterfaceName
                : $"{queueNs}.{reg.InterfaceName}";

            sb.AppendLine($"{indent2}int {interfaceName}.Priority");
            sb.AppendLine($"{indent2}{{");
            sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent3}get => PriorityFor{reg.QueueClassName};");
            sb.AppendLine($"{indent2}}}");
            sb.AppendLine();
        }
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // シグナルプロパティ（インターフェース明示的実装）
        sb.AppendLine($"{indent2}#region Queue IsSignal（インターフェース実装）");
        sb.AppendLine();

        // 複数キュー対応：IsSignalFor{QueueClassName} プロパティを生成
        foreach (var reg in info.QueueRegistrations)
        {
            sb.AppendLine($"{indent2}/// <summary>{reg.QueueClassName} でシグナルコマンドかどうか</summary>");
            sb.AppendLine($"{indent2}public bool IsSignalFor{reg.QueueClassName}");
            sb.AppendLine($"{indent2}{{");
            sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent3}get => {(reg.Signal ? "true" : "false")};");
            sb.AppendLine($"{indent2}}}");
            sb.AppendLine();
        }

        // インターフェース明示的実装（IsSignalFor{QueueClassName}を参照）
        foreach (var reg in info.QueueRegistrations)
        {
            // 同じ名前空間の場合はインターフェース名のみ、異なる場合は完全修飾名
            var queueNs = GetNamespace(reg.QueueFullyQualifiedName);
            var interfaceName = queueNs == info.Namespace
                ? reg.InterfaceName
                : $"{queueNs}.{reg.InterfaceName}";

            sb.AppendLine($"{indent2}bool {interfaceName}.IsSignal");
            sb.AppendLine($"{indent2}{{");
            sb.AppendLine($"{indent3}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent3}get => IsSignalFor{reg.QueueClassName};");
            sb.AppendLine($"{indent2}}}");
            sb.AppendLine();
        }
        sb.AppendLine($"{indent2}#endregion");
        sb.AppendLine();

        // ResetToDefaultメソッド
        sb.AppendLine($"{indent2}#region Reset");
        sb.AppendLine();
        sb.AppendLine($"{indent2}/// <summary>");
        sb.AppendLine($"{indent2}/// フィールドを初期値にリセット");
        sb.AppendLine($"{indent2}/// </summary>");
        sb.AppendLine($"{indent2}[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent2}public void ResetToDefault()");
        sb.AppendLine($"{indent2}{{");

        foreach (var field in info.FieldResets)
        {
            sb.AppendLine($"{indent3}{field.ResetCode}");
        }

        sb.AppendLine($"{indent2}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent2}#endregion");

        sb.AppendLine($"{indent}}}");

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{info.ClassName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GetNamespace(string fullyQualifiedName)
    {
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot > 0 ? fullyQualifiedName.Substring(0, lastDot) : "";
    }

    #endregion
}

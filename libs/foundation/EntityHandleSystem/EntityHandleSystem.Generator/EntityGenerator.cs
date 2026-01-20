using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// Source Generator that generates Handle structs and Arena classes
/// for types marked with [Entity] attribute.
/// </summary>
[Generator]
public class EntityGenerator : ISourceGenerator
{
        private const string EntityAttributeFullName = "Tomato.EntityHandleSystem.EntityAttribute";
        private const string EntityMethodAttributeFullName = "Tomato.EntityHandleSystem.EntityMethodAttribute";
        private const string EntityComponentAttributeFullName = "Tomato.EntityHandleSystem.EntityComponentAttribute";
        private const string HasCommandQueueAttributeFullName = "Tomato.EntityHandleSystem.HasCommandQueueAttribute";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new EntitySyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not EntitySyntaxReceiver receiver)
            {
                return;
            }

            Compilation compilation = context.Compilation;

            INamedTypeSymbol entityAttributeSymbol = compilation.GetTypeByMetadataName(EntityAttributeFullName);
            INamedTypeSymbol entityMethodAttributeSymbol = compilation.GetTypeByMetadataName(EntityMethodAttributeFullName);
            INamedTypeSymbol entityComponentAttributeSymbol = compilation.GetTypeByMetadataName(EntityComponentAttributeFullName);
            INamedTypeSymbol hasCommandQueueAttributeSymbol = compilation.GetTypeByMetadataName(HasCommandQueueAttributeFullName);

            if (entityAttributeSymbol == null)
            {
                return;
            }

            foreach (TypeDeclarationSyntax typeDeclaration in receiver.CandidateTypes)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
                INamedTypeSymbol classSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;

                if (classSymbol == null)
                {
                    continue;
                }

                AttributeData entityAttribute = classSymbol.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, entityAttributeSymbol));

                if (entityAttribute == null)
                {
                    continue;
                }

                // Extract attribute parameters
                int initialCapacity = 256;
                string arenaName = null;

                foreach (KeyValuePair<string, TypedConstant> namedArg in entityAttribute.NamedArguments)
                {
                    if (namedArg.Key == "InitialCapacity" && namedArg.Value.Value is int capacity)
                    {
                        initialCapacity = capacity;
                    }
                    else if (namedArg.Key == "ArenaName" && namedArg.Value.Value is string name)
                    {
                        arenaName = name;
                    }
                }

                // Collect EntityMethod methods
                List<EntityMethodInfo> entityMethods = new List<EntityMethodInfo>();

                if (entityMethodAttributeSymbol != null)
                {
                    foreach (ISymbol member in classSymbol.GetMembers())
                    {
                        if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary && !methodSymbol.IsStatic)
                        {
                            AttributeData methodAttribute = methodSymbol.GetAttributes()
                                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, entityMethodAttributeSymbol));

                            if (methodAttribute != null)
                            {
                                bool unsafeFlag = false;
                                foreach (KeyValuePair<string, TypedConstant> namedArg in methodAttribute.NamedArguments)
                                {
                                    if (namedArg.Key == "Unsafe" && namedArg.Value.Value is bool u)
                                    {
                                        unsafeFlag = u;
                                    }
                                }

                                entityMethods.Add(new EntityMethodInfo(methodSymbol, unsafeFlag));
                            }
                        }
                    }
                }

                // Collect EntityComponent attributes
                List<ComponentInfo> components = new List<ComponentInfo>();

                if (entityComponentAttributeSymbol != null)
                {
                    foreach (AttributeData componentAttribute in classSymbol.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(componentAttribute.AttributeClass, entityComponentAttributeSymbol))
                        {
                            // Get the component type from the constructor argument
                            if (componentAttribute.ConstructorArguments.Length > 0 &&
                                componentAttribute.ConstructorArguments[0].Value is INamedTypeSymbol componentType)
                            {
                                // Collect EntityMethod methods from the component type
                                List<EntityMethodInfo> componentMethods = new List<EntityMethodInfo>();

                                if (entityMethodAttributeSymbol != null)
                                {
                                    foreach (ISymbol member in componentType.GetMembers())
                                    {
                                        if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Ordinary && !methodSymbol.IsStatic)
                                        {
                                            AttributeData methodAttribute = methodSymbol.GetAttributes()
                                                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, entityMethodAttributeSymbol));

                                            if (methodAttribute != null)
                                            {
                                                bool unsafeFlag = false;
                                                foreach (KeyValuePair<string, TypedConstant> namedArg in methodAttribute.NamedArguments)
                                                {
                                                    if (namedArg.Key == "Unsafe" && namedArg.Value.Value is bool u)
                                                    {
                                                        unsafeFlag = u;
                                                    }
                                                }

                                                componentMethods.Add(new EntityMethodInfo(methodSymbol, unsafeFlag));
                                            }
                                        }
                                    }
                                }

                                components.Add(new ComponentInfo(componentType, componentMethods));
                            }
                        }
                    }
                }

                // Collect HasCommandQueue attributes
                List<CommandQueueInfo> commandQueues = new List<CommandQueueInfo>();

                if (hasCommandQueueAttributeSymbol != null)
                {
                    foreach (AttributeData attr in classSymbol.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, hasCommandQueueAttributeSymbol))
                        {
                            // Get the queue type from the constructor argument
                            if (attr.ConstructorArguments.Length > 0 &&
                                attr.ConstructorArguments[0].Value is INamedTypeSymbol queueType)
                            {
                                commandQueues.Add(new CommandQueueInfo(queueType));
                            }
                        }
                    }
                }

                // Generate code
                string source = GenerateEntityCode(classSymbol, initialCapacity, arenaName, entityMethods, components, commandQueues);

                string fileName = $"{classSymbol.ContainingNamespace?.ToDisplayString() ?? ""}.{classSymbol.Name}.g.cs";
                fileName = fileName.TrimStart('.');

                context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
            }
        }

        private string GenerateEntityCode(
            INamedTypeSymbol classSymbol,
            int initialCapacity,
            string customArenaName,
            List<EntityMethodInfo> entityMethods,
            List<ComponentInfo> components,
            List<CommandQueueInfo> commandQueues)
        {
            string typeName = classSymbol.Name;
            string fullTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string handleName = typeName + "Handle";
            string arenaName = customArenaName ?? typeName + "Arena";
            string namespaceName = classSymbol.ContainingNamespace?.ToDisplayString();
            bool hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#pragma warning disable CS0618 // Type or member is obsolete");
            sb.AppendLine("#pragma warning disable CS0219 // Variable is assigned but never used");
            sb.AppendLine("#pragma warning disable CS8019 // Unnecessary using directive");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine();

            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            // Generate Handle struct
            GenerateHandleStruct(sb, typeName, handleName, arenaName, entityMethods, components, commandQueues, hasNamespace);

            sb.AppendLine();

            // Generate Arena class
            GenerateArenaClass(sb, typeName, handleName, arenaName, initialCapacity, components, commandQueues, hasNamespace);

            // Generate Entity partial class/struct with _selfHandle (if Entity has components)
            if (components.Count > 0)
            {
                sb.AppendLine();
                bool isStruct = classSymbol.TypeKind == TypeKind.Struct;
                GenerateEntityPartial(sb, typeName, handleName, arenaName, components, hasNamespace, isStruct);
            }

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private void GenerateHandleStruct(
            StringBuilder sb,
            string typeName,
            string handleName,
            string arenaName,
            List<EntityMethodInfo> entityMethods,
            List<ComponentInfo> components,
            List<CommandQueueInfo> commandQueues,
            bool hasNamespace)
        {
            string indent = hasNamespace ? "    " : "";

            sb.AppendLine($"{indent}public struct {handleName} : IEquatable<{handleName}>, global::Tomato.EntityHandleSystem.IEntityHandle");
            sb.AppendLine($"{indent}{{");

            // Fields
            sb.AppendLine($"{indent}    internal readonly {arenaName} _arena;");
            sb.AppendLine($"{indent}    private readonly int _generation;");
            sb.AppendLine($"{indent}    private readonly int _index;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"{indent}    internal {handleName}({arenaName} arena, int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _arena = arena;");
            sb.AppendLine($"{indent}        _index = index;");
            sb.AppendLine($"{indent}        _generation = generation;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // IsValid property
            sb.AppendLine($"{indent}    public bool IsValid");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get {{ return _arena != null && _arena.IsValid(this); }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Invalid static property
            sb.AppendLine($"{indent}    public static {handleName} Invalid");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get {{ return default({handleName}); }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Dispose method (not IDisposable to avoid boxing)
            sb.AppendLine($"{indent}    public void Dispose()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (_arena != null)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _arena.DestroyInternal(_index, _generation);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // ToVoidHandle method
            sb.AppendLine($"{indent}    public Tomato.EntityHandleSystem.VoidHandle ToVoidHandle()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new Tomato.EntityHandleSystem.VoidHandle(_arena, _index, _generation);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // IEquatable<T>.Equals
            sb.AppendLine($"{indent}    public bool Equals({handleName} other)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return _arena == other._arena");
            sb.AppendLine($"{indent}            && _generation == other._generation");
            sb.AppendLine($"{indent}            && _index == other._index;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Object.Equals
            sb.AppendLine($"{indent}    public override bool Equals(object obj)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return obj is {handleName} && Equals(({handleName})obj);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // GetHashCode
            sb.AppendLine($"{indent}    public override int GetHashCode()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        unchecked");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int hash = 17;");
            sb.AppendLine($"{indent}            hash = hash * 31 + (_arena != null ? _arena.GetHashCode() : 0);");
            sb.AppendLine($"{indent}            hash = hash * 31 + _generation;");
            sb.AppendLine($"{indent}            hash = hash * 31 + _index;");
            sb.AppendLine($"{indent}            return hash;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Equality operators
            sb.AppendLine($"{indent}    public static bool operator ==({handleName} left, {handleName} right)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return left.Equals(right);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            sb.AppendLine($"{indent}    public static bool operator !=({handleName} left, {handleName} right)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return !left.Equals(right);");
            sb.AppendLine($"{indent}    }}");

            // Internal accessors for Arena
            sb.AppendLine();
            sb.AppendLine($"{indent}    internal int Index {{ get {{ return _index; }} }}");
            sb.AppendLine($"{indent}    internal int Generation {{ get {{ return _generation; }} }}");

            // Generate EntityMethod wrappers
            foreach (EntityMethodInfo methodInfo in entityMethods)
            {
                sb.AppendLine();
                GenerateTryMethod(sb, typeName, arenaName, methodInfo, indent);

                if (methodInfo.Unsafe)
                {
                    sb.AppendLine();
                    GenerateUnsafeMethod(sb, typeName, arenaName, methodInfo, indent);
                }
            }

            // Generate component method wrappers (ComponentName_TryMethodName)
            foreach (ComponentInfo component in components)
            {
                foreach (EntityMethodInfo methodInfo in component.Methods)
                {
                    sb.AppendLine();
                    GenerateComponentTryMethod(sb, arenaName, component, methodInfo, components, indent);

                    if (methodInfo.Unsafe)
                    {
                        sb.AppendLine();
                        GenerateComponentUnsafeMethod(sb, arenaName, component, methodInfo, indent);
                    }
                }
            }

            // Generate TryExecute<T> method if there are components
            if (components.Count > 0)
            {
                sb.AppendLine();
                GenerateHandleTryExecuteMethod(sb, arenaName, indent);
            }

            // Generate CommandQueue property for each command queue
            foreach (CommandQueueInfo queue in commandQueues)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// {queue.QueueName} を取得します。");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public {queue.QueueFullName} {queue.QueueName} => _arena.GetCommandQueue_{queue.QueueName}(_index);");
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateTryMethod(
            StringBuilder sb,
            string typeName,
            string arenaName,
            EntityMethodInfo methodInfo,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : null;

            // Build parameter list for the Try method
            StringBuilder paramListBuilder = new StringBuilder();
            StringBuilder callArgsBuilder = new StringBuilder();
            StringBuilder defaultAssignmentsBuilder = new StringBuilder();

            foreach (IParameterSymbol param in method.Parameters)
            {
                if (paramListBuilder.Length > 0)
                {
                    paramListBuilder.Append(", ");
                }
                if (callArgsBuilder.Length > 0)
                {
                    callArgsBuilder.Append(", ");
                }

                string paramTypeName = GetTypeFullName(param.Type);
                string paramName = param.Name;

                if (param.RefKind == RefKind.Ref)
                {
                    paramListBuilder.Append($"ref {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"ref {paramName}");
                }
                else if (param.RefKind == RefKind.Out)
                {
                    paramListBuilder.Append($"out {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"out {paramName}");
                    defaultAssignmentsBuilder.AppendLine($"{indent}            {paramName} = default({paramTypeName});");
                }
                else
                {
                    paramListBuilder.Append($"{paramTypeName} {paramName}");
                    callArgsBuilder.Append(paramName);
                }
            }

            // Add result out parameter if method has return value
            if (hasReturnValue)
            {
                if (paramListBuilder.Length > 0)
                {
                    paramListBuilder.Append(", ");
                }
                paramListBuilder.Append($"out {returnTypeName} result");
                defaultAssignmentsBuilder.AppendLine($"{indent}            result = default({returnTypeName});");
            }

            string paramList = paramListBuilder.ToString();
            string callArgs = callArgsBuilder.ToString();
            string defaultAssignments = defaultAssignmentsBuilder.ToString();

            // Generate the Try method with ref-based access
            sb.AppendLine($"{indent}    public bool Try{methodName}({paramList})");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_arena.LockObject)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            ref var entity = ref _arena.TryGetRefInternal(_index, _generation, out var valid);");
            sb.AppendLine($"{indent}            if (!valid)");
            sb.AppendLine($"{indent}            {{");
            if (!string.IsNullOrEmpty(defaultAssignments))
            {
                sb.Append(defaultAssignments);
            }
            sb.AppendLine($"{indent}                return false;");
            sb.AppendLine($"{indent}            }}");

            if (hasReturnValue)
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}            result = entity.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}            result = entity.{methodName}({callArgs});");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}            entity.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}            entity.{methodName}({callArgs});");
                }
            }

            sb.AppendLine($"{indent}            return true;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateUnsafeMethod(
            StringBuilder sb,
            string typeName,
            string arenaName,
            EntityMethodInfo methodInfo,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : "void";

            // Build parameter list (same as original method)
            StringBuilder paramListBuilder = new StringBuilder();
            StringBuilder callArgsBuilder = new StringBuilder();

            foreach (IParameterSymbol param in method.Parameters)
            {
                if (paramListBuilder.Length > 0)
                {
                    paramListBuilder.Append(", ");
                }
                if (callArgsBuilder.Length > 0)
                {
                    callArgsBuilder.Append(", ");
                }

                string paramTypeName = GetTypeFullName(param.Type);
                string paramName = param.Name;

                if (param.RefKind == RefKind.Ref)
                {
                    paramListBuilder.Append($"ref {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"ref {paramName}");
                }
                else if (param.RefKind == RefKind.Out)
                {
                    paramListBuilder.Append($"out {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"out {paramName}");
                }
                else
                {
                    paramListBuilder.Append($"{paramTypeName} {paramName}");
                    callArgsBuilder.Append(paramName);
                }
            }

            string paramList = paramListBuilder.ToString();
            string callArgs = callArgsBuilder.ToString();

            // Generate the Unsafe method with ref-based access
            sb.AppendLine($"{indent}    public {returnTypeName} {methodName}_Unsafe({paramList})");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        ref var entity = ref _arena.GetEntityRefUnchecked(_index);");

            if (hasReturnValue)
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}        return entity.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}        return entity.{methodName}({callArgs});");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}        entity.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}        entity.{methodName}({callArgs});");
                }
            }

            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateComponentTryMethod(
            StringBuilder sb,
            string arenaName,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            List<ComponentInfo> allComponents,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            string componentName = component.ComponentName;
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : null;

            // Build parameter list for the Try method (user-facing)
            StringBuilder handleParamListBuilder = new StringBuilder();
            StringBuilder arenaCallArgsBuilder = new StringBuilder();
            List<ComponentInfo> autoFetchComponents = new List<ComponentInfo>();

            foreach (IParameterSymbol param in method.Parameters)
            {
                string paramTypeName = GetTypeFullName(param.Type);
                string paramTypeFullName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string paramName = param.Name;

                // Check if this is a ref parameter to another component on the same entity
                ComponentInfo refComponent = null;
                if (param.RefKind == RefKind.Ref)
                {
                    refComponent = allComponents.FirstOrDefault(c => c.ComponentFullName == paramTypeFullName);
                }

                if (refComponent != null)
                {
                    // This is a ref to another component - will be auto-fetched by Arena
                    autoFetchComponents.Add(refComponent);
                    // Don't add to handleParamListBuilder (user doesn't pass this)
                }
                else
                {
                    if (handleParamListBuilder.Length > 0)
                    {
                        handleParamListBuilder.Append(", ");
                    }
                    if (arenaCallArgsBuilder.Length > 0)
                    {
                        arenaCallArgsBuilder.Append(", ");
                    }

                    if (param.RefKind == RefKind.Ref)
                    {
                        handleParamListBuilder.Append($"ref {paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append($"ref {paramName}");
                    }
                    else if (param.RefKind == RefKind.Out)
                    {
                        handleParamListBuilder.Append($"out {paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append($"out {paramName}");
                    }
                    else
                    {
                        handleParamListBuilder.Append($"{paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append(paramName);
                    }
                }
            }

            // Add result out parameter if method has return value
            if (hasReturnValue)
            {
                if (handleParamListBuilder.Length > 0)
                {
                    handleParamListBuilder.Append(", ");
                }
                handleParamListBuilder.Append($"out {returnTypeName} result");
            }

            string handleParamList = handleParamListBuilder.ToString();
            string arenaCallArgs = arenaCallArgsBuilder.ToString();

            // Generate the ComponentName_TryMethodName method on Handle
            // This delegates to Arena's internal method
            sb.AppendLine($"{indent}    public bool {componentName}_Try{methodName}({handleParamList})");
            sb.AppendLine($"{indent}    {{");

            StringBuilder delegateCall = new StringBuilder();
            delegateCall.Append($"return _arena.{componentName}_Try{methodName}Internal(_index, _generation");
            if (!string.IsNullOrEmpty(arenaCallArgs))
            {
                delegateCall.Append(", ");
                delegateCall.Append(arenaCallArgs);
            }
            if (hasReturnValue)
            {
                delegateCall.Append(", out result");
            }
            delegateCall.Append(");");

            sb.AppendLine($"{indent}        {delegateCall}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateComponentUnsafeMethod(
            StringBuilder sb,
            string arenaName,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            string componentName = component.ComponentName;
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : "void";

            // Build parameter list
            StringBuilder paramListBuilder = new StringBuilder();
            StringBuilder callArgsBuilder = new StringBuilder();

            foreach (IParameterSymbol param in method.Parameters)
            {
                if (paramListBuilder.Length > 0)
                {
                    paramListBuilder.Append(", ");
                }
                if (callArgsBuilder.Length > 0)
                {
                    callArgsBuilder.Append(", ");
                }

                string paramTypeName = GetTypeFullName(param.Type);
                string paramName = param.Name;

                if (param.RefKind == RefKind.Ref)
                {
                    paramListBuilder.Append($"ref {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"ref {paramName}");
                }
                else if (param.RefKind == RefKind.Out)
                {
                    paramListBuilder.Append($"out {paramTypeName} {paramName}");
                    callArgsBuilder.Append($"out {paramName}");
                }
                else
                {
                    paramListBuilder.Append($"{paramTypeName} {paramName}");
                    callArgsBuilder.Append(paramName);
                }
            }

            string paramList = paramListBuilder.ToString();
            string callArgs = callArgsBuilder.ToString();

            // Generate the ComponentName_MethodName_Unsafe method
            // This delegates to Arena's internal Unsafe method
            sb.AppendLine($"{indent}    public {returnTypeName} {componentName}_{methodName}_Unsafe({paramList})");
            sb.AppendLine($"{indent}    {{");

            StringBuilder delegateCall = new StringBuilder();
            if (hasReturnValue)
            {
                delegateCall.Append("return ");
            }
            delegateCall.Append($"_arena.{componentName}_{methodName}_UnsafeInternal(_index");
            if (!string.IsNullOrEmpty(callArgs))
            {
                delegateCall.Append(", ");
                delegateCall.Append(callArgs);
            }
            delegateCall.Append(");");

            sb.AppendLine($"{indent}        {delegateCall}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateHandleTryExecuteMethod(
            StringBuilder sb,
            string arenaName,
            string indent)
        {
            sb.AppendLine($"{indent}    public bool TryExecute<TComponent>(Tomato.EntityHandleSystem.RefAction<TComponent> action)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (_arena is Tomato.EntityHandleSystem.IComponentArena<TComponent> componentArena)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return componentArena.TryExecuteComponent(_index, _generation, action);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        return false;");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateArenaClass(
            StringBuilder sb,
            string typeName,
            string handleName,
            string arenaName,
            int initialCapacity,
            List<ComponentInfo> components,
            List<CommandQueueInfo> commandQueues,
            bool hasNamespace)
        {
            string indent = hasNamespace ? "    " : "";

            // Build interface list
            StringBuilder interfaceList = new StringBuilder();
            interfaceList.Append("Tomato.EntityHandleSystem.EntityArenaBase<");
            interfaceList.Append(typeName);
            interfaceList.Append(", ");
            interfaceList.Append(handleName);
            interfaceList.Append(">, Tomato.EntityHandleSystem.IEntityArena");

            foreach (ComponentInfo component in components)
            {
                interfaceList.Append(", Tomato.EntityHandleSystem.IComponentArena<");
                interfaceList.Append(component.ComponentFullName);
                interfaceList.Append(">");
            }

            // Note: IHasCommandQueue is not implemented at Arena level since queues are per-entity

            sb.AppendLine($"{indent}public class {arenaName} : {interfaceList}");
            sb.AppendLine($"{indent}{{");

            // Generate component array fields
            foreach (ComponentInfo component in components)
            {
                sb.AppendLine($"{indent}    private {component.ComponentFullName}[] _{component.ComponentName.ToLower()}Components;");
            }

            // Generate command queue array fields (Entity単位で持つ)
            foreach (CommandQueueInfo queue in commandQueues)
            {
                sb.AppendLine($"{indent}    private {queue.QueueFullName}[] _{queue.QueueName.ToLower()}Queues;");
            }

            if (components.Count > 0 || commandQueues.Count > 0)
            {
                sb.AppendLine();
            }

            // Default constructor
            sb.AppendLine($"{indent}    public {arenaName}()");
            sb.AppendLine($"{indent}        : this({initialCapacity}, null, null) {{ }}");
            sb.AppendLine();

            // Constructor with capacity
            sb.AppendLine($"{indent}    public {arenaName}(int initialCapacity)");
            sb.AppendLine($"{indent}        : this(initialCapacity, null, null) {{ }}");
            sb.AppendLine();

            // Full constructor with RefAction
            sb.AppendLine($"{indent}    public {arenaName}(");
            sb.AppendLine($"{indent}        int initialCapacity,");
            sb.AppendLine($"{indent}        Tomato.EntityHandleSystem.RefAction<{typeName}> onSpawn,");
            sb.AppendLine($"{indent}        Tomato.EntityHandleSystem.RefAction<{typeName}> onDespawn)");
            sb.AppendLine($"{indent}        : base(initialCapacity, onSpawn, onDespawn)");
            sb.AppendLine($"{indent}    {{");

            // Initialize component arrays in constructor
            foreach (ComponentInfo component in components)
            {
                string fieldName = $"_{component.ComponentName.ToLower()}Components";
                sb.AppendLine($"{indent}        {fieldName} = new {component.ComponentFullName}[initialCapacity <= 0 ? 1 : initialCapacity];");
            }

            // Initialize command queue arrays in constructor
            foreach (CommandQueueInfo queue in commandQueues)
            {
                string fieldName = $"_{queue.QueueName.ToLower()}Queues";
                sb.AppendLine($"{indent}        {fieldName} = new {queue.QueueFullName}[initialCapacity <= 0 ? 1 : initialCapacity];");
            }

            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Create method
            sb.AppendLine($"{indent}    public {handleName} Create()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int generation;");
            sb.AppendLine($"{indent}            int index = AllocateInternal(out generation);");
            sb.AppendLine($"{indent}            var handle = new {handleName}(this, index, generation);");

            // Set _selfHandle if components exist
            if (components.Count > 0)
            {
                sb.AppendLine($"{indent}            _entities[index]._selfHandle = handle;");
            }

            // Initialize command queues for this entity
            foreach (CommandQueueInfo queue in commandQueues)
            {
                string fieldName = $"_{queue.QueueName.ToLower()}Queues";
                sb.AppendLine($"{indent}            {fieldName}[index] = new {queue.QueueFullName}();");
            }

            sb.AppendLine($"{indent}            return handle;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // IsValid method (for typed handle)
            sb.AppendLine($"{indent}    public bool IsValid({handleName} handle)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return IsValidInternal(handle.Index, handle.Generation);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // IEntityArena.IsValid method (for VoidHandle)
            sb.AppendLine($"{indent}    bool Tomato.EntityHandleSystem.IEntityArena.IsValid(int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return IsValidInternal(index, generation);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // AsHandle method (convert VoidHandle back to typed handle)
            sb.AppendLine($"{indent}    public {handleName} AsHandle(Tomato.EntityHandleSystem.VoidHandle voidHandle)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new {handleName}(this, voidHandle.Index, voidHandle.Generation);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // DestroyInternal method
            sb.AppendLine($"{indent}    internal bool DestroyInternal(int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return DeallocateInternal(index, generation);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // TryGetRefInternal method (internal, for Handle methods with ref access)
            sb.AppendLine($"{indent}    internal new ref {typeName} TryGetRefInternal(int index, int generation, out bool valid)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return ref base.TryGetRefInternal(index, generation, out valid);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // GetEntityRefUnchecked method (for Unsafe methods with ref access)
            sb.AppendLine($"{indent}    internal new ref {typeName} GetEntityRefUnchecked(int index)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return ref base.GetEntityRefUnchecked(index);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // LockObject property (for Handle methods that need lock access)
            sb.AppendLine($"{indent}    internal object LockObject");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get {{ return _lock; }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Count property
            sb.AppendLine($"{indent}    public int Count");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get {{ lock (_lock) {{ return _count; }} }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Capacity property
            sb.AppendLine($"{indent}    public int Capacity");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        get {{ lock (_lock) {{ return _entities.Length; }} }}");
            sb.AppendLine($"{indent}    }}");

            // Generate OnArrayExpanded override if there are components
            if (components.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}    protected override void OnArrayExpanded(int oldCapacity, int newCapacity)");
                sb.AppendLine($"{indent}    {{");

                foreach (ComponentInfo component in components)
                {
                    string fieldName = $"_{component.ComponentName.ToLower()}Components";
                    sb.AppendLine($"{indent}        var new{component.ComponentName}Components = new {component.ComponentFullName}[newCapacity];");
                    sb.AppendLine($"{indent}        Array.Copy({fieldName}, new{component.ComponentName}Components, {fieldName}.Length);");
                    sb.AppendLine($"{indent}        {fieldName} = new{component.ComponentName}Components;");
                }

                sb.AppendLine($"{indent}    }}");
            }

            // Generate component accessor methods and IComponentArena implementations
            foreach (ComponentInfo component in components)
            {
                string fieldName = $"_{component.ComponentName.ToLower()}Components";

                sb.AppendLine();
                // Internal GetRefUnchecked for component
                sb.AppendLine($"{indent}    internal ref {component.ComponentFullName} {component.ComponentName}_GetRefUnchecked(int index)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return ref {fieldName}[index];");
                sb.AppendLine($"{indent}    }}");

                sb.AppendLine();
                // IComponentArena<T>.GetComponentRefUnchecked explicit implementation
                sb.AppendLine($"{indent}    ref {component.ComponentFullName} Tomato.EntityHandleSystem.IComponentArena<{component.ComponentFullName}>.GetComponentRefUnchecked(int index)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return ref {fieldName}[index];");
                sb.AppendLine($"{indent}    }}");

                sb.AppendLine();
                // IComponentArena<T>.TryExecuteComponent explicit implementation
                sb.AppendLine($"{indent}    bool Tomato.EntityHandleSystem.IComponentArena<{component.ComponentFullName}>.TryExecuteComponent(int index, int generation, Tomato.EntityHandleSystem.RefAction<{component.ComponentFullName}> action)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        lock (_lock)");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            if (!IsValidInternal(index, generation))");
                sb.AppendLine($"{indent}            {{");
                sb.AppendLine($"{indent}                return false;");
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}            action?.Invoke(ref {fieldName}[index]);");
                sb.AppendLine($"{indent}            return true;");
                sb.AppendLine($"{indent}        }}");
                sb.AppendLine($"{indent}    }}");

                // Generate internal Try methods for each component method
                foreach (EntityMethodInfo methodInfo in component.Methods)
                {
                    sb.AppendLine();
                    GenerateArenaComponentTryMethod(sb, component, methodInfo, components, indent);

                    // Always generate UnsafeInternal (needed for Entity partial class methods)
                    sb.AppendLine();
                    GenerateArenaComponentUnsafeMethodWithComponents(sb, component, methodInfo, components, indent);
                }
            }

            // Generate command queue accessor methods (Entity単位)
            foreach (CommandQueueInfo queue in commandQueues)
            {
                sb.AppendLine();
                // Internal accessor for Handle (index指定で配列アクセス)
                sb.AppendLine($"{indent}    internal {queue.QueueFullName} GetCommandQueue_{queue.QueueName}(int index)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return _{queue.QueueName.ToLower()}Queues[index];");
                sb.AppendLine($"{indent}    }}");
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateArenaComponentUnsafeMethod(
            StringBuilder sb,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            string indent)
        {
            GenerateArenaComponentUnsafeMethodWithComponents(sb, component, methodInfo, new List<ComponentInfo>(), indent);
        }

        private void GenerateArenaComponentUnsafeMethodWithComponents(
            StringBuilder sb,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            List<ComponentInfo> allComponents,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            string componentName = component.ComponentName;
            string fieldName = $"_{component.ComponentName.ToLower()}Components";
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : "void";

            // Build parameter list (excluding auto-fetched components)
            StringBuilder arenaParamListBuilder = new StringBuilder();
            arenaParamListBuilder.Append("int index");

            StringBuilder callArgsBuilder = new StringBuilder();
            List<string> autoFetchStatements = new List<string>();

            foreach (IParameterSymbol param in method.Parameters)
            {
                string paramTypeName = GetTypeFullName(param.Type);
                string paramTypeFullName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string paramName = param.Name;

                // Check if this is a ref parameter to another component on the same entity
                ComponentInfo refComponent = null;
                if (param.RefKind == RefKind.Ref && allComponents.Count > 0)
                {
                    refComponent = allComponents.FirstOrDefault(c => c.ComponentFullName == paramTypeFullName);
                }

                if (refComponent != null)
                {
                    // This is a ref to another component - auto-fetch it
                    string refFieldName = $"_{refComponent.ComponentName.ToLower()}Components";
                    autoFetchStatements.Add($"ref var {paramName} = ref {refFieldName}[index];");
                    if (callArgsBuilder.Length > 0)
                    {
                        callArgsBuilder.Append(", ");
                    }
                    callArgsBuilder.Append($"ref {paramName}");
                }
                else
                {
                    arenaParamListBuilder.Append(", ");
                    if (callArgsBuilder.Length > 0)
                    {
                        callArgsBuilder.Append(", ");
                    }

                    if (param.RefKind == RefKind.Ref)
                    {
                        arenaParamListBuilder.Append($"ref {paramTypeName} {paramName}");
                        callArgsBuilder.Append($"ref {paramName}");
                    }
                    else if (param.RefKind == RefKind.Out)
                    {
                        arenaParamListBuilder.Append($"out {paramTypeName} {paramName}");
                        callArgsBuilder.Append($"out {paramName}");
                    }
                    else
                    {
                        arenaParamListBuilder.Append($"{paramTypeName} {paramName}");
                        callArgsBuilder.Append(paramName);
                    }
                }
            }

            string arenaParamList = arenaParamListBuilder.ToString();
            string callArgs = callArgsBuilder.ToString();

            // Generate the internal Arena Unsafe method
            sb.AppendLine($"{indent}    internal {returnTypeName} {componentName}_{methodName}_UnsafeInternal({arenaParamList})");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        ref var component = ref {fieldName}[index];");

            // Auto-fetch other components
            foreach (string statement in autoFetchStatements)
            {
                sb.AppendLine($"{indent}        {statement}");
            }

            if (hasReturnValue)
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}        return component.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}        return component.{methodName}({callArgs});");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}        component.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}        component.{methodName}({callArgs});");
                }
            }

            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateArenaComponentTryMethod(
            StringBuilder sb,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            List<ComponentInfo> allComponents,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            string componentName = component.ComponentName;
            string fieldName = $"_{component.ComponentName.ToLower()}Components";
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : null;

            // Build parameter list for the internal Arena method
            StringBuilder arenaParamListBuilder = new StringBuilder();
            arenaParamListBuilder.Append("int index, int generation");

            StringBuilder callArgsBuilder = new StringBuilder();
            StringBuilder defaultAssignmentsBuilder = new StringBuilder();
            List<string> autoFetchStatements = new List<string>();

            foreach (IParameterSymbol param in method.Parameters)
            {
                string paramTypeName = GetTypeFullName(param.Type);
                string paramTypeFullName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string paramName = param.Name;

                // Check if this is a ref parameter to another component on the same entity
                ComponentInfo refComponent = null;
                if (param.RefKind == RefKind.Ref)
                {
                    refComponent = allComponents.FirstOrDefault(c => c.ComponentFullName == paramTypeFullName);
                }

                if (refComponent != null)
                {
                    // This is a ref to another component - auto-fetch it
                    string refFieldName = $"_{refComponent.ComponentName.ToLower()}Components";
                    autoFetchStatements.Add($"ref var {paramName} = ref {refFieldName}[index];");
                    if (callArgsBuilder.Length > 0)
                    {
                        callArgsBuilder.Append(", ");
                    }
                    callArgsBuilder.Append($"ref {paramName}");
                }
                else
                {
                    arenaParamListBuilder.Append(", ");
                    if (callArgsBuilder.Length > 0)
                    {
                        callArgsBuilder.Append(", ");
                    }

                    if (param.RefKind == RefKind.Ref)
                    {
                        arenaParamListBuilder.Append($"ref {paramTypeName} {paramName}");
                        callArgsBuilder.Append($"ref {paramName}");
                    }
                    else if (param.RefKind == RefKind.Out)
                    {
                        arenaParamListBuilder.Append($"out {paramTypeName} {paramName}");
                        callArgsBuilder.Append($"out {paramName}");
                        defaultAssignmentsBuilder.AppendLine($"{indent}            {paramName} = default({paramTypeName});");
                    }
                    else
                    {
                        arenaParamListBuilder.Append($"{paramTypeName} {paramName}");
                        callArgsBuilder.Append(paramName);
                    }
                }
            }

            // Add result out parameter if method has return value
            if (hasReturnValue)
            {
                arenaParamListBuilder.Append($", out {returnTypeName} result");
                defaultAssignmentsBuilder.AppendLine($"{indent}            result = default({returnTypeName});");
            }

            string arenaParamList = arenaParamListBuilder.ToString();
            string callArgs = callArgsBuilder.ToString();
            string defaultAssignments = defaultAssignmentsBuilder.ToString();

            // Generate the internal Arena method
            sb.AppendLine($"{indent}    internal bool {componentName}_Try{methodName}Internal({arenaParamList})");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            if (!IsValidInternal(index, generation))");
            sb.AppendLine($"{indent}            {{");
            if (!string.IsNullOrEmpty(defaultAssignments))
            {
                sb.Append(defaultAssignments);
            }
            sb.AppendLine($"{indent}                return false;");
            sb.AppendLine($"{indent}            }}");

            // Get component ref
            sb.AppendLine($"{indent}            ref var component = ref {fieldName}[index];");

            // Auto-fetch other components
            foreach (string statement in autoFetchStatements)
            {
                sb.AppendLine($"{indent}            {statement}");
            }

            if (hasReturnValue)
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}            result = component.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}            result = component.{methodName}({callArgs});");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(callArgs))
                {
                    sb.AppendLine($"{indent}            component.{methodName}();");
                }
                else
                {
                    sb.AppendLine($"{indent}            component.{methodName}({callArgs});");
                }
            }

            sb.AppendLine($"{indent}            return true;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        private string GetTypeFullName(ITypeSymbol typeSymbol)
        {
            // Use minimal qualified format for cleaner output
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        private void GenerateEntityPartial(
            StringBuilder sb,
            string typeName,
            string handleName,
            string arenaName,
            List<ComponentInfo> components,
            bool hasNamespace,
            bool isStruct)
        {
            string indent = hasNamespace ? "    " : "";
            string typeKeyword = isStruct ? "struct" : "class";

            sb.AppendLine($"{indent}partial {typeKeyword} {typeName}");
            sb.AppendLine($"{indent}{{");

            // Generate _selfHandle field
            sb.AppendLine($"{indent}    internal {handleName} _selfHandle;");
            sb.AppendLine();

            // Generate component accessor methods (no validity check since entity knows it's alive)
            foreach (ComponentInfo component in components)
            {
                foreach (EntityMethodInfo methodInfo in component.Methods)
                {
                    sb.AppendLine();
                    GenerateEntityComponentMethod(sb, arenaName, component, methodInfo, components, indent);

                    if (methodInfo.Unsafe)
                    {
                        sb.AppendLine();
                        GenerateEntityComponentUnsafeMethod(sb, arenaName, component, methodInfo, indent);
                    }
                }
            }

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateEntityComponentMethod(
            StringBuilder sb,
            string arenaName,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            List<ComponentInfo> allComponents,
            string indent)
        {
            IMethodSymbol method = methodInfo.Method;
            string methodName = method.Name;
            string componentName = component.ComponentName;
            bool hasReturnValue = !method.ReturnsVoid;
            string returnTypeName = hasReturnValue ? GetTypeFullName(method.ReturnType) : "void";

            // Build parameter list (excluding auto-fetched components)
            StringBuilder paramListBuilder = new StringBuilder();
            StringBuilder arenaCallArgsBuilder = new StringBuilder();

            foreach (IParameterSymbol param in method.Parameters)
            {
                string paramTypeName = GetTypeFullName(param.Type);
                string paramTypeFullName = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string paramName = param.Name;

                // Check if this is a ref parameter to another component on the same entity
                ComponentInfo refComponent = null;
                if (param.RefKind == RefKind.Ref)
                {
                    refComponent = allComponents.FirstOrDefault(c => c.ComponentFullName == paramTypeFullName);
                }

                if (refComponent != null)
                {
                    // Auto-fetched by Arena - skip in parameter list
                }
                else
                {
                    if (paramListBuilder.Length > 0)
                    {
                        paramListBuilder.Append(", ");
                    }
                    if (arenaCallArgsBuilder.Length > 0)
                    {
                        arenaCallArgsBuilder.Append(", ");
                    }

                    if (param.RefKind == RefKind.Ref)
                    {
                        paramListBuilder.Append($"ref {paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append($"ref {paramName}");
                    }
                    else if (param.RefKind == RefKind.Out)
                    {
                        paramListBuilder.Append($"out {paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append($"out {paramName}");
                    }
                    else
                    {
                        paramListBuilder.Append($"{paramTypeName} {paramName}");
                        arenaCallArgsBuilder.Append(paramName);
                    }
                }
            }

            string paramList = paramListBuilder.ToString();
            string arenaCallArgs = arenaCallArgsBuilder.ToString();

            // Generate the ComponentName_MethodName method (calls Arena's Unsafe method since entity knows it's alive)
            sb.AppendLine($"{indent}    public {returnTypeName} {componentName}_{methodName}({paramList})");
            sb.AppendLine($"{indent}    {{");

            StringBuilder delegateCall = new StringBuilder();
            if (hasReturnValue)
            {
                delegateCall.Append("return ");
            }
            delegateCall.Append($"_selfHandle._arena.{componentName}_{methodName}_UnsafeInternal(_selfHandle.Index");
            if (!string.IsNullOrEmpty(arenaCallArgs))
            {
                delegateCall.Append(", ");
                delegateCall.Append(arenaCallArgs);
            }
            delegateCall.Append(");");

            sb.AppendLine($"{indent}        {delegateCall}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateEntityComponentUnsafeMethod(
            StringBuilder sb,
            string arenaName,
            ComponentInfo component,
            EntityMethodInfo methodInfo,
            string indent)
        {
            // For entity internal access, Unsafe and normal are the same (no validity check)
            // This is a placeholder for symmetry with Handle methods
        }

        /// <summary>
        /// Information about an EntityMethod to be generated.
        /// </summary>
        private class EntityMethodInfo
        {
            public IMethodSymbol Method { get; }
            public bool Unsafe { get; }

            public EntityMethodInfo(IMethodSymbol method, bool unsafeFlag)
            {
                Method = method;
                Unsafe = unsafeFlag;
            }
        }

        /// <summary>
        /// Information about an EntityComponent to be generated.
        /// </summary>
        private class ComponentInfo
        {
            public INamedTypeSymbol ComponentType { get; }
            public string ComponentName { get; }
            public string ComponentFullName { get; }
            public List<EntityMethodInfo> Methods { get; }

            public ComponentInfo(INamedTypeSymbol componentType, List<EntityMethodInfo> methods)
            {
                ComponentType = componentType;
                ComponentName = componentType.Name;
                ComponentFullName = componentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                Methods = methods;
            }
        }

        /// <summary>
        /// Syntax receiver that collects candidate classes and structs with [Entity] attribute.
        /// </summary>
        private class EntitySyntaxReceiver : ISyntaxReceiver
        {
            public List<TypeDeclarationSyntax> CandidateTypes { get; } = new List<TypeDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Handle both class and struct declarations
                if (syntaxNode is TypeDeclarationSyntax typeDeclaration &&
                    (syntaxNode is ClassDeclarationSyntax || syntaxNode is StructDeclarationSyntax))
                {
                    // Check if the type has any attributes
                    if (typeDeclaration.AttributeLists.Count > 0)
                    {
                        // Check if any attribute might be [Entity]
                        foreach (AttributeListSyntax attributeList in typeDeclaration.AttributeLists)
                        {
                            foreach (AttributeSyntax attribute in attributeList.Attributes)
                            {
                                string name = attribute.Name.ToString();
                                if (name == "Entity" || name == "EntityAttribute" ||
                                    name.EndsWith(".Entity") || name.EndsWith(".EntityAttribute"))
                                {
                                    CandidateTypes.Add(typeDeclaration);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Information about a CommandQueue associated with an Entity.
        /// </summary>
        private class CommandQueueInfo
        {
            public INamedTypeSymbol QueueType { get; }
            public string QueueName { get; }
            public string QueueFullName { get; }

            public CommandQueueInfo(INamedTypeSymbol queueType)
            {
                QueueType = queueType;
                QueueName = queueType.Name;
                QueueFullName = queueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
    }

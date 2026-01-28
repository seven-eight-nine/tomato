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

            // Track groups for derived attributes
            Dictionary<string, GroupInfo> groups = new Dictionary<string, GroupInfo>();

            foreach (TypeDeclarationSyntax typeDeclaration in receiver.CandidateTypes)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
                INamedTypeSymbol classSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;

                if (classSymbol == null)
                {
                    continue;
                }

                // Find an attribute that is EntityAttribute or inherits from it
                AttributeData entityAttribute = null;
                INamedTypeSymbol derivedAttributeType = null;
                bool isDerivedAttribute = false;

                foreach (AttributeData attr in classSymbol.GetAttributes())
                {
                    if (attr.AttributeClass == null)
                    {
                        continue;
                    }

                    // Check for exact match with EntityAttribute
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, entityAttributeSymbol))
                    {
                        entityAttribute = attr;
                        isDerivedAttribute = false;
                        break;
                    }

                    // Check if the attribute inherits from EntityAttribute
                    if (InheritsFrom(attr.AttributeClass, entityAttributeSymbol))
                    {
                        entityAttribute = attr;
                        derivedAttributeType = attr.AttributeClass;
                        isDerivedAttribute = true;
                        break;
                    }
                }

                if (entityAttribute == null)
                {
                    continue;
                }

                // Extract group info for derived attributes
                string groupName = null;
                if (isDerivedAttribute && derivedAttributeType != null)
                {
                    groupName = ExtractGroupName(derivedAttributeType);
                    string groupNamespace = derivedAttributeType.ContainingNamespace?.ToDisplayString();
                    string attributeFullName = derivedAttributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    if (!groups.ContainsKey(groupName))
                    {
                        groups[groupName] = new GroupInfo(groupName, groupNamespace, attributeFullName);
                    }
                    groups[groupName].Entities.Add(classSymbol);
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

                // Collect components and command queues
                List<ComponentInfo> components = new List<ComponentInfo>();
                List<CommandQueueInfo> commandQueues = new List<CommandQueueInfo>();

                // Collect EntityComponent attributes
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
                string source = GenerateEntityCode(classSymbol, initialCapacity, arenaName, entityMethods, components, commandQueues, groupName);

                string fileName = $"{classSymbol.ContainingNamespace?.ToDisplayString() ?? ""}.{classSymbol.Name}.g.cs";
                fileName = fileName.TrimStart('.');

                context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
            }

            // Generate group types (I{GroupName}Arena interface and {GroupName}AnyHandle struct)
            foreach (KeyValuePair<string, GroupInfo> kvp in groups)
            {
                GroupInfo group = kvp.Value;
                string groupSource = GenerateGroupCode(group);

                string groupFileName = $"{group.Namespace ?? ""}.{group.GroupName}Group.g.cs";
                groupFileName = groupFileName.TrimStart('.');

                context.AddSource(groupFileName, SourceText.From(groupSource, Encoding.UTF8));
            }
        }

        private string GenerateEntityCode(
            INamedTypeSymbol classSymbol,
            int initialCapacity,
            string customArenaName,
            List<EntityMethodInfo> entityMethods,
            List<ComponentInfo> components,
            List<CommandQueueInfo> commandQueues,
            string groupName)
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
            GenerateHandleStruct(sb, typeName, handleName, arenaName, entityMethods, components, commandQueues, hasNamespace, groupName);

            sb.AppendLine();

            // Generate Arena class
            GenerateArenaClass(sb, typeName, handleName, arenaName, initialCapacity, components, commandQueues, hasNamespace, groupName);

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
            bool hasNamespace,
            string groupName)
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

            // ToAnyHandle method
            sb.AppendLine($"{indent}    public Tomato.EntityHandleSystem.AnyHandle ToAnyHandle()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new Tomato.EntityHandleSystem.AnyHandle(_arena, _index, _generation);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // ToGroupAnyHandle method (if entity belongs to a group)
            if (!string.IsNullOrEmpty(groupName))
            {
                sb.AppendLine($"{indent}    public {groupName}AnyHandle To{groupName}AnyHandle()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return new {groupName}AnyHandle(_arena, _index, _generation);");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
            }

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
            sb.AppendLine($"{indent}    public bool TryExecute<TComponent>(Tomato.HandleSystem.RefAction<TComponent> action)");
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
            bool hasNamespace,
            string groupName)
        {
            string indent = hasNamespace ? "    " : "";

            // Build interface list
            StringBuilder interfaceList = new StringBuilder();
            interfaceList.Append("Tomato.EntityHandleSystem.EntityArenaBase<");
            interfaceList.Append(typeName);
            interfaceList.Append(", ");
            interfaceList.Append(handleName);
            interfaceList.Append(">, Tomato.EntityHandleSystem.IEntityArena");

            // Add group interface if entity belongs to a group
            if (!string.IsNullOrEmpty(groupName))
            {
                interfaceList.Append(", I");
                interfaceList.Append(groupName);
                interfaceList.Append("Arena");
            }

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
            sb.AppendLine($"{indent}        Tomato.HandleSystem.RefAction<{typeName}> onSpawn,");
            sb.AppendLine($"{indent}        Tomato.HandleSystem.RefAction<{typeName}> onDespawn)");
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

            // IArena.IsValid method (for AnyHandle - IEntityArena inherits from IArena)
            sb.AppendLine($"{indent}    bool Tomato.HandleSystem.IArena.IsValid(int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        lock (_lock)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return IsValidInternal(index, generation);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // AsHandle method (convert AnyHandle back to typed handle)
            sb.AppendLine($"{indent}    public {handleName} AsHandle(Tomato.EntityHandleSystem.AnyHandle voidHandle)");
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
                sb.AppendLine($"{indent}    bool Tomato.EntityHandleSystem.IComponentArena<{component.ComponentFullName}>.TryExecuteComponent(int index, int generation, Tomato.HandleSystem.RefAction<{component.ComponentFullName}> action)");
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

        /// <summary>
        /// Generates the group-specific types: I{GroupName}Arena interface and {GroupName}AnyHandle struct.
        /// </summary>
        private string GenerateGroupCode(GroupInfo group)
        {
            string groupName = group.GroupName;
            string namespaceName = group.Namespace;
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

            string indent = hasNamespace ? "    " : "";

            // Generate I{GroupName}Arena interface
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {groupName} グループ Arena のマーカーインターフェース。");
            sb.AppendLine($"{indent}/// このグループに属する全ての Arena が実装します。");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public interface I{groupName}Arena : Tomato.EntityHandleSystem.IEntityArena {{ }}");
            sb.AppendLine();

            // Generate {GroupName}AnyHandle struct
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {groupName} グループ固有の型消去ハンドル。");
            sb.AppendLine($"{indent}/// 同グループ内の異なるエンティティ型を一つのコンテナに格納し、横串操作ができます。");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public readonly struct {groupName}AnyHandle");
            sb.AppendLine($"{indent}    : Tomato.EntityHandleSystem.IEntityHandle, System.IEquatable<{groupName}AnyHandle>");
            sb.AppendLine($"{indent}{{");

            // Fields
            sb.AppendLine($"{indent}    private readonly I{groupName}Arena _arena;");
            sb.AppendLine($"{indent}    private readonly int _index;");
            sb.AppendLine($"{indent}    private readonly int _generation;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"{indent}    public {groupName}AnyHandle(I{groupName}Arena arena, int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _arena = arena;");
            sb.AppendLine($"{indent}        _index = index;");
            sb.AppendLine($"{indent}        _generation = generation;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // IsValid property
            sb.AppendLine($"{indent}    public bool IsValid => _arena != null && ((Tomato.HandleSystem.IArena)_arena).IsValid(_index, _generation);");
            sb.AppendLine();

            // Invalid static property
            sb.AppendLine($"{indent}    public static {groupName}AnyHandle Invalid => default;");
            sb.AppendLine();

            // Internal Arena property (for Container access)
            sb.AppendLine($"{indent}    internal I{groupName}Arena Arena => _arena;");
            sb.AppendLine();

            // Index property
            sb.AppendLine($"{indent}    public int Index => _index;");
            sb.AppendLine();

            // Generation property
            sb.AppendLine($"{indent}    public int Generation => _generation;");
            sb.AppendLine();

            // ToAnyHandle method (convert to global AnyHandle)
            sb.AppendLine($"{indent}    /// <summary>グローバル AnyHandle への変換</summary>");
            sb.AppendLine($"{indent}    public Tomato.EntityHandleSystem.AnyHandle ToAnyHandle()");
            sb.AppendLine($"{indent}        => new Tomato.EntityHandleSystem.AnyHandle(");
            sb.AppendLine($"{indent}            (Tomato.EntityHandleSystem.IEntityArena)_arena, _index, _generation);");
            sb.AppendLine();

            // TryAs method
            sb.AppendLine($"{indent}    public bool TryAs<TArena>(out TArena arena) where TArena : class, Tomato.EntityHandleSystem.IEntityArena");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        arena = _arena as TArena;");
            sb.AppendLine($"{indent}        return arena != null;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // TryExecute method
            sb.AppendLine($"{indent}    public bool TryExecute<TComponent>(Tomato.HandleSystem.RefAction<TComponent> action)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (_arena is Tomato.EntityHandleSystem.IComponentArena<TComponent> componentArena)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return componentArena.TryExecuteComponent(_index, _generation, action);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        return false;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Equals method
            sb.AppendLine($"{indent}    public bool Equals({groupName}AnyHandle other)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return ReferenceEquals(_arena, other._arena)");
            sb.AppendLine($"{indent}            && _index == other._index");
            sb.AppendLine($"{indent}            && _generation == other._generation;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Object.Equals override
            sb.AppendLine($"{indent}    public override bool Equals(object obj)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return obj is {groupName}AnyHandle other && Equals(other);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // GetHashCode override
            sb.AppendLine($"{indent}    public override int GetHashCode()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        unchecked");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int hash = 17;");
            sb.AppendLine($"{indent}            hash = hash * 31 + (_arena?.GetHashCode() ?? 0);");
            sb.AppendLine($"{indent}            hash = hash * 31 + _index;");
            sb.AppendLine($"{indent}            hash = hash * 31 + _generation;");
            sb.AppendLine($"{indent}            return hash;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Equality operators
            sb.AppendLine($"{indent}    public static bool operator ==({groupName}AnyHandle left, {groupName}AnyHandle right) => left.Equals(right);");
            sb.AppendLine($"{indent}    public static bool operator !=({groupName}AnyHandle left, {groupName}AnyHandle right) => !left.Equals(right);");

            sb.AppendLine($"{indent}}}");

            sb.AppendLine();

            // Generate {GroupName}Container class
            GenerateGroupContainer(sb, group, indent);

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates the {GroupName}Container class for sorted storage of group handles.
        /// </summary>
        private void GenerateGroupContainer(StringBuilder sb, GroupInfo group, string indent)
        {
            string groupName = group.GroupName;

            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {groupName} グループのハンドルを Arena 順・Index 順でソートして格納するコンテナ。");
            sb.AppendLine($"{indent}/// キャッシュ効率の最大化とコンポーネントベースのクエリをサポートします。");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public sealed class {groupName}Container");
            sb.AppendLine($"{indent}{{");

            // ArenaSegment struct
            sb.AppendLine($"{indent}    private struct ArenaSegment");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        public I{groupName}Arena Arena;");
            sb.AppendLine($"{indent}        public {groupName}AnyHandle[] Handles;");
            sb.AppendLine($"{indent}        public int Count;");
            sb.AppendLine($"{indent}        public int FreeCount;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public int Capacity => Handles?.Length ?? 0;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Fields
            sb.AppendLine($"{indent}    private ArenaSegment[] _segments;");
            sb.AppendLine($"{indent}    private int _segmentCount;");
            sb.AppendLine($"{indent}    private readonly System.Collections.Generic.Dictionary<I{groupName}Arena, int> _arenaToSegmentIndex;");
            sb.AppendLine($"{indent}    private int _totalCount;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 新しい {groupName}Container を作成します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public {groupName}Container()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _segments = new ArenaSegment[4];");
            sb.AppendLine($"{indent}        _segmentCount = 0;");
            sb.AppendLine($"{indent}        _arenaToSegmentIndex = new System.Collections.Generic.Dictionary<I{groupName}Arena, int>();");
            sb.AppendLine($"{indent}        _totalCount = 0;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Count property
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// コンテナ内の有効なハンドル数を取得します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public int Count => _totalCount;");
            sb.AppendLine();

            // Add method
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// ハンドルをコンテナに追加します。Arena 順・Index 順でソートされた位置に挿入されます。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <param name=\"handle\">追加するハンドル</param>");
            sb.AppendLine($"{indent}    public void Add({groupName}AnyHandle handle)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (!handle.IsValid) return;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        var arena = handle.Arena;");
            sb.AppendLine($"{indent}        if (arena == null) return;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Get or create segment for this arena");
            sb.AppendLine($"{indent}        if (!_arenaToSegmentIndex.TryGetValue(arena, out int segmentIndex))");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            segmentIndex = CreateSegment(arena);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        ref var segment = ref _segments[segmentIndex];");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Binary search for insertion position (sorted by Index)");
            sb.AppendLine($"{indent}        int insertPos = BinarySearchInsertPosition(ref segment, handle.Index);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Ensure capacity");
            sb.AppendLine($"{indent}        if (segment.Count >= segment.Capacity)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int newCapacity = segment.Capacity == 0 ? 4 : segment.Capacity * 2;");
            sb.AppendLine($"{indent}            var newHandles = new {groupName}AnyHandle[newCapacity];");
            sb.AppendLine($"{indent}            if (segment.Handles != null)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                System.Array.Copy(segment.Handles, newHandles, segment.Count);");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            segment.Handles = newHandles;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Shift elements and insert");
            sb.AppendLine($"{indent}        if (insertPos < segment.Count)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            System.Array.Copy(segment.Handles, insertPos, segment.Handles, insertPos + 1, segment.Count - insertPos);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        segment.Handles[insertPos] = handle;");
            sb.AppendLine($"{indent}        segment.Count++;");
            sb.AppendLine($"{indent}        _totalCount++;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Remove method
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// ハンドルをコンテナから削除します（遅延削除：スロットを無効としてマーク）。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <param name=\"handle\">削除するハンドル</param>");
            sb.AppendLine($"{indent}    /// <returns>削除された場合は true</returns>");
            sb.AppendLine($"{indent}    public bool Remove({groupName}AnyHandle handle)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        var arena = handle.Arena;");
            sb.AppendLine($"{indent}        if (arena == null) return false;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        if (!_arenaToSegmentIndex.TryGetValue(arena, out int segmentIndex))");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            return false;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        ref var segment = ref _segments[segmentIndex];");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Binary search for the handle");
            sb.AppendLine($"{indent}        int pos = BinarySearchExact(ref segment, handle.Index, handle.Generation);");
            sb.AppendLine($"{indent}        if (pos < 0) return false;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        // Mark as removed (set to default/invalid)");
            sb.AppendLine($"{indent}        segment.Handles[pos] = default;");
            sb.AppendLine($"{indent}        segment.FreeCount++;");
            sb.AppendLine($"{indent}        _totalCount--;");
            sb.AppendLine($"{indent}        return true;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Clear method
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// コンテナをクリアします。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public void Clear()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        for (int i = 0; i < _segmentCount; i++)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _segments[i].Count = 0;");
            sb.AppendLine($"{indent}            _segments[i].FreeCount = 0;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        _segmentCount = 0;");
            sb.AppendLine($"{indent}        _arenaToSegmentIndex.Clear();");
            sb.AppendLine($"{indent}        _totalCount = 0;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Compact method
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 無効なスロットを除去してコンパクト化します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public void Compact()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        for (int s = 0; s < _segmentCount; s++)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            ref var segment = ref _segments[s];");
            sb.AppendLine($"{indent}            if (segment.FreeCount == 0) continue;");
            sb.AppendLine();
            sb.AppendLine($"{indent}            int writePos = 0;");
            sb.AppendLine($"{indent}            for (int readPos = 0; readPos < segment.Count; readPos++)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                if (segment.Handles[readPos].IsValid)");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    if (writePos != readPos)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        segment.Handles[writePos] = segment.Handles[readPos];");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    writePos++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            segment.Count = writePos;");
            sb.AppendLine($"{indent}            segment.FreeCount = 0;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // GetEnumerator
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 全ての有効なハンドルをイテレートする列挙子を取得します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public Enumerator GetEnumerator() => new Enumerator(this);");
            sb.AppendLine();

            // GetIterator
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// フレーム分散更新用のイテレータを取得します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    /// <param name=\"skip\">スキップするスロット数（0で全て、1で1つおき）</param>");
            sb.AppendLine($"{indent}    /// <param name=\"offset\">開始オフセット</param>");
            sb.AppendLine($"{indent}    public Iterator GetIterator(int skip = 0, int offset = 0) => new Iterator(this, skip, offset);");
            sb.AppendLine();

            // Query<T>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定したコンポーネントを持つエンティティのみをフィルタリングして返します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public QueryView<TComponent> Query<TComponent>()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new QueryView<TComponent>(this);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Query<T1, T2>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定した2つのコンポーネントを両方持つエンティティのみをフィルタリングして返します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public QueryView<TComponent1, TComponent2> Query<TComponent1, TComponent2>()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new QueryView<TComponent1, TComponent2>(this);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Query<T1, T2, T3>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定した3つのコンポーネントを全て持つエンティティのみをフィルタリングして返します。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public QueryView<TComponent1, TComponent2, TComponent3> Query<TComponent1, TComponent2, TComponent3>()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new QueryView<TComponent1, TComponent2, TComponent3>(this);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Private helper methods
            sb.AppendLine($"{indent}    private int CreateSegment(I{groupName}Arena arena)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (_segmentCount >= _segments.Length)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            var newSegments = new ArenaSegment[_segments.Length * 2];");
            sb.AppendLine($"{indent}            System.Array.Copy(_segments, newSegments, _segments.Length);");
            sb.AppendLine($"{indent}            _segments = newSegments;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        int index = _segmentCount++;");
            sb.AppendLine($"{indent}        _segments[index] = new ArenaSegment");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            Arena = arena,");
            sb.AppendLine($"{indent}            Handles = new {groupName}AnyHandle[4],");
            sb.AppendLine($"{indent}            Count = 0,");
            sb.AppendLine($"{indent}            FreeCount = 0");
            sb.AppendLine($"{indent}        }};");
            sb.AppendLine($"{indent}        _arenaToSegmentIndex[arena] = index;");
            sb.AppendLine($"{indent}        return index;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            sb.AppendLine($"{indent}    private static int BinarySearchInsertPosition(ref ArenaSegment segment, int index)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        int lo = 0;");
            sb.AppendLine($"{indent}        int hi = segment.Count - 1;");
            sb.AppendLine($"{indent}        while (lo <= hi)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int mid = lo + (hi - lo) / 2;");
            sb.AppendLine($"{indent}            int midIndex = segment.Handles[mid].Index;");
            sb.AppendLine($"{indent}            if (midIndex < index)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                lo = mid + 1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            else");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                hi = mid - 1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        return lo;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            sb.AppendLine($"{indent}    private static int BinarySearchExact(ref ArenaSegment segment, int index, int generation)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        int lo = 0;");
            sb.AppendLine($"{indent}        int hi = segment.Count - 1;");
            sb.AppendLine($"{indent}        while (lo <= hi)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int mid = lo + (hi - lo) / 2;");
            sb.AppendLine($"{indent}            var h = segment.Handles[mid];");
            sb.AppendLine($"{indent}            if (h.Index == index && h.Generation == generation)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                return mid;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            if (h.Index < index)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                lo = mid + 1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            else");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                hi = mid - 1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        return -1;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Internal accessor for segments (used by QueryView)
            sb.AppendLine($"{indent}    internal int SegmentCount => _segmentCount;");
            sb.AppendLine($"{indent}    internal I{groupName}Arena GetSegmentArena(int segmentIndex) => _segments[segmentIndex].Arena;");
            sb.AppendLine($"{indent}    internal int GetSegmentHandleCount(int segmentIndex) => _segments[segmentIndex].Count;");
            sb.AppendLine($"{indent}    internal {groupName}AnyHandle GetSegmentHandle(int segmentIndex, int handleIndex) => _segments[segmentIndex].Handles[handleIndex];");
            sb.AppendLine();

            // Enumerator struct
            GenerateContainerEnumerator(sb, groupName, indent);
            sb.AppendLine();

            // Iterator struct
            GenerateContainerIterator(sb, groupName, indent);
            sb.AppendLine();

            // QueryView structs
            GenerateQueryViews(sb, groupName, indent);

            sb.AppendLine($"{indent}}}");
        }

        private void GenerateContainerEnumerator(StringBuilder sb, string groupName, string indent)
        {
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// {groupName}Container の列挙子。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public struct Enumerator");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private int _segmentIndex;");
            sb.AppendLine($"{indent}        private int _handleIndex;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal Enumerator({groupName}Container container)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentIndex = 0;");
            sb.AppendLine($"{indent}            _handleIndex = -1;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public {groupName}AnyHandle Current => _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public bool MoveNext()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _handleIndex++;");
            sb.AppendLine($"{indent}                while (_handleIndex < _container.GetSegmentHandleCount(_segmentIndex))");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    if (_container.GetSegmentHandle(_segmentIndex, _handleIndex).IsValid)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        return true;");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    _handleIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}                _handleIndex = -1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            return false;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateContainerIterator(StringBuilder sb, string groupName, string indent)
        {
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// フレーム分散更新用のイテレータ。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public struct Iterator");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly int _step;");
            sb.AppendLine($"{indent}        private int _globalIndex;");
            sb.AppendLine($"{indent}        private int _segmentIndex;");
            sb.AppendLine($"{indent}        private int _handleIndex;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal Iterator({groupName}Container container, int skip, int offset)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _step = skip + 1;");
            sb.AppendLine($"{indent}            _globalIndex = offset - _step;");
            sb.AppendLine($"{indent}            _segmentIndex = 0;");
            sb.AppendLine($"{indent}            _handleIndex = -1;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public {groupName}AnyHandle Current => _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public bool MoveNext()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            while (true)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _globalIndex += _step;");
            sb.AppendLine();
            sb.AppendLine($"{indent}                // Find the segment and handle at globalIndex");
            sb.AppendLine($"{indent}                int remaining = _globalIndex;");
            sb.AppendLine($"{indent}                _segmentIndex = 0;");
            sb.AppendLine($"{indent}                while (_segmentIndex < _container.SegmentCount)");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    int segCount = _container.GetSegmentHandleCount(_segmentIndex);");
            sb.AppendLine($"{indent}                    if (remaining < segCount)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        _handleIndex = remaining;");
            sb.AppendLine($"{indent}                        var handle = _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine($"{indent}                        if (handle.IsValid)");
            sb.AppendLine($"{indent}                        {{");
            sb.AppendLine($"{indent}                            return true;");
            sb.AppendLine($"{indent}                        }}");
            sb.AppendLine($"{indent}                        // Skip invalid, continue to next step");
            sb.AppendLine($"{indent}                        break;");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    remaining -= segCount;");
            sb.AppendLine($"{indent}                    _segmentIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}                if (_segmentIndex >= _container.SegmentCount)");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    return false;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        private void GenerateQueryViews(StringBuilder sb, string groupName, string indent)
        {
            // QueryView<T>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定したコンポーネントを持つエンティティのみをフィルタリングするビュー。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public readonly ref struct QueryView<TComponent>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryView({groupName}Container container)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = new bool[container.SegmentCount];");
            sb.AppendLine($"{indent}            for (int i = 0; i < container.SegmentCount; i++)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _segmentMatches[i] = container.GetSegmentArena(i) is Tomato.EntityHandleSystem.IComponentArena<TComponent>;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public QueryEnumerator<TComponent> GetEnumerator() => new QueryEnumerator<TComponent>(_container, _segmentMatches);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // QueryView<T1, T2>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定した2つのコンポーネントを持つエンティティのみをフィルタリングするビュー。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public readonly ref struct QueryView<TComponent1, TComponent2>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryView({groupName}Container container)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = new bool[container.SegmentCount];");
            sb.AppendLine($"{indent}            for (int i = 0; i < container.SegmentCount; i++)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                var arena = container.GetSegmentArena(i);");
            sb.AppendLine($"{indent}                _segmentMatches[i] = arena is Tomato.EntityHandleSystem.IComponentArena<TComponent1>");
            sb.AppendLine($"{indent}                    && arena is Tomato.EntityHandleSystem.IComponentArena<TComponent2>;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public QueryEnumerator<TComponent1, TComponent2> GetEnumerator() => new QueryEnumerator<TComponent1, TComponent2>(_container, _segmentMatches);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // QueryView<T1, T2, T3>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 指定した3つのコンポーネントを持つエンティティのみをフィルタリングするビュー。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public readonly ref struct QueryView<TComponent1, TComponent2, TComponent3>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryView({groupName}Container container)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = new bool[container.SegmentCount];");
            sb.AppendLine($"{indent}            for (int i = 0; i < container.SegmentCount; i++)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                var arena = container.GetSegmentArena(i);");
            sb.AppendLine($"{indent}                _segmentMatches[i] = arena is Tomato.EntityHandleSystem.IComponentArena<TComponent1>");
            sb.AppendLine($"{indent}                    && arena is Tomato.EntityHandleSystem.IComponentArena<TComponent2>");
            sb.AppendLine($"{indent}                    && arena is Tomato.EntityHandleSystem.IComponentArena<TComponent3>;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public QueryEnumerator<TComponent1, TComponent2, TComponent3> GetEnumerator() => new QueryEnumerator<TComponent1, TComponent2, TComponent3>(_container, _segmentMatches);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // QueryEnumerator<T>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// クエリ結果の列挙子。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public ref struct QueryEnumerator<TComponent>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine($"{indent}        private int _segmentIndex;");
            sb.AppendLine($"{indent}        private int _handleIndex;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryEnumerator({groupName}Container container, bool[] segmentMatches)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = segmentMatches;");
            sb.AppendLine($"{indent}            _segmentIndex = 0;");
            sb.AppendLine($"{indent}            _handleIndex = -1;");
            sb.AppendLine($"{indent}            // Skip to first matching segment");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public {groupName}AnyHandle Current => _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public bool MoveNext()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _handleIndex++;");
            sb.AppendLine($"{indent}                while (_handleIndex < _container.GetSegmentHandleCount(_segmentIndex))");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    if (_container.GetSegmentHandle(_segmentIndex, _handleIndex).IsValid)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        return true;");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    _handleIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                // Move to next matching segment");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}                while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    _segmentIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _handleIndex = -1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            return false;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // QueryEnumerator<T1, T2>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 2コンポーネントクエリ結果の列挙子。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public ref struct QueryEnumerator<TComponent1, TComponent2>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine($"{indent}        private int _segmentIndex;");
            sb.AppendLine($"{indent}        private int _handleIndex;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryEnumerator({groupName}Container container, bool[] segmentMatches)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = segmentMatches;");
            sb.AppendLine($"{indent}            _segmentIndex = 0;");
            sb.AppendLine($"{indent}            _handleIndex = -1;");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public {groupName}AnyHandle Current => _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public bool MoveNext()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _handleIndex++;");
            sb.AppendLine($"{indent}                while (_handleIndex < _container.GetSegmentHandleCount(_segmentIndex))");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    if (_container.GetSegmentHandle(_segmentIndex, _handleIndex).IsValid)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        return true;");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    _handleIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}                while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    _segmentIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _handleIndex = -1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            return false;");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // QueryEnumerator<T1, T2, T3>
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 3コンポーネントクエリ結果の列挙子。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public ref struct QueryEnumerator<TComponent1, TComponent2, TComponent3>");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {groupName}Container _container;");
            sb.AppendLine($"{indent}        private readonly bool[] _segmentMatches;");
            sb.AppendLine($"{indent}        private int _segmentIndex;");
            sb.AppendLine($"{indent}        private int _handleIndex;");
            sb.AppendLine();
            sb.AppendLine($"{indent}        internal QueryEnumerator({groupName}Container container, bool[] segmentMatches)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _container = container;");
            sb.AppendLine($"{indent}            _segmentMatches = segmentMatches;");
            sb.AppendLine($"{indent}            _segmentIndex = 0;");
            sb.AppendLine($"{indent}            _handleIndex = -1;");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public {groupName}AnyHandle Current => _container.GetSegmentHandle(_segmentIndex, _handleIndex);");
            sb.AppendLine();
            sb.AppendLine($"{indent}        public bool MoveNext()");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            while (_segmentIndex < _container.SegmentCount)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                _handleIndex++;");
            sb.AppendLine($"{indent}                while (_handleIndex < _container.GetSegmentHandleCount(_segmentIndex))");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    if (_container.GetSegmentHandle(_segmentIndex, _handleIndex).IsValid)");
            sb.AppendLine($"{indent}                    {{");
            sb.AppendLine($"{indent}                        return true;");
            sb.AppendLine($"{indent}                    }}");
            sb.AppendLine($"{indent}                    _handleIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _segmentIndex++;");
            sb.AppendLine($"{indent}                while (_segmentIndex < _container.SegmentCount && !_segmentMatches[_segmentIndex])");
            sb.AppendLine($"{indent}                {{");
            sb.AppendLine($"{indent}                    _segmentIndex++;");
            sb.AppendLine($"{indent}                }}");
            sb.AppendLine($"{indent}                _handleIndex = -1;");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            return false;");
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
        /// Syntax receiver that collects candidate classes and structs with [Entity] attribute
        /// or any attribute that might inherit from EntityAttribute.
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
                    // We collect all types with attributes and filter in semantic analysis phase
                    // to support derived attributes like [PlayerEntity] that inherit from [Entity]
                    if (typeDeclaration.AttributeLists.Count > 0)
                    {
                        CandidateTypes.Add(typeDeclaration);
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

        /// <summary>
        /// Information about an Entity Group (derived from EntityAttribute).
        /// </summary>
        private class GroupInfo
        {
            public string GroupName { get; }
            public string Namespace { get; }
            public string AttributeFullName { get; }
            public List<INamedTypeSymbol> Entities { get; } = new List<INamedTypeSymbol>();

            public GroupInfo(string groupName, string namespaceName, string attributeFullName)
            {
                GroupName = groupName;
                Namespace = namespaceName;
                AttributeFullName = attributeFullName;
            }
        }

        /// <summary>
        /// Checks if a type inherits from a specified base type.
        /// </summary>
        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            if (type == null || baseType == null)
            {
                return false;
            }

            INamedTypeSymbol current = type.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }

        /// <summary>
        /// Extracts the group name from a derived attribute type.
        /// e.g., "PlayerEntityAttribute" -> "PlayerEntity"
        /// </summary>
        private static string ExtractGroupName(INamedTypeSymbol attributeType)
        {
            string name = attributeType.Name;
            if (name.EndsWith("Attribute"))
            {
                return name.Substring(0, name.Length - "Attribute".Length);
            }
            return name;
        }
    }

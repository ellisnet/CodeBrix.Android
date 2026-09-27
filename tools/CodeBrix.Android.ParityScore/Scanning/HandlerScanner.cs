using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.AssemblyTools;
using CodeBrix.AssemblyTools.Cil;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>
/// Reads the Android assemblies' IL (never runs it) and finds (1) every property mapper and the DependencyProperty keys
/// it maps - collection initialisers, <c>Add</c>/indexer calls, the Append/Prepend/Modify/ReplaceMapping customisations
/// from anywhere (the policy layer included), mapper helper methods, and the chain passed to the mapper's constructor;
/// (2) the mapper each handler type passes to its base constructor; and (3) every element-handler registration and the
/// handler types its factory creates.
/// </summary>
/// <remarks>
/// The scan follows the shapes the C# compiler emits for this code base; it is a linear walk of each method body, not a
/// data-flow analysis. The last DependencyProperty loaded before a key-adding call is that call's key; the mapper a key
/// goes to is the one under construction (between a mapper <c>newobj</c> and the field store) or the last mapper loaded.
/// </remarks>
internal sealed class HandlerScanner
{
    private const string ArgumentMapper = "<argument>";

    private readonly ParityConventions _conventions;
    private readonly AssemblySet _set;

    /// <summary>Creates the scanner.</summary>
    /// <param name="conventions">The names to recognise.</param>
    /// <param name="set">The assembly set (for resolution).</param>
    internal HandlerScanner(ParityConventions conventions, AssemblySet set)
    {
        _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
        _set = set ?? throw new ArgumentNullException(nameof(set));
    }

    /// <summary>Scans the handler assemblies.</summary>
    /// <param name="assemblies">The Android assemblies.</param>
    /// <returns>The model.</returns>
    internal HandlerModel Scan(IEnumerable<AssemblyDefinition> assemblies)
    {
        var model = new HandlerModel();
        var list = assemblies.ToList();
        var types = list.SelectMany(a => a.Modules).SelectMany(m => m.GetTypes()).ToList();

        foreach (var type in types)
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                ScanMappers(method, model);
            }
        }

        foreach (var type in types)
        {
            if (type.IsClass && !type.IsAbstract && _set.Implements(type, _conventions.HandlerInterface))
            {
                var mapper = FindHandlerMapper(type, 0);
                if (mapper != null)
                {
                    model.HandlerMappers[type.FullName] = mapper;
                }
            }
        }

        foreach (var assembly in list)
        {
            foreach (var type in assembly.Modules.SelectMany(m => m.GetTypes()))
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    ScanRegistrations(assembly.Name.Name, method, model);
                }
            }
        }

        return model;
    }

    /// <summary>The id of a DependencyProperty member (<c>Namespace.Type.NameProperty</c>).</summary>
    /// <param name="declaringType">The declaring type.</param>
    /// <param name="memberName">The field or property name.</param>
    /// <returns>The id.</returns>
    internal static string PropertyId(TypeReference declaringType, string memberName) => declaringType.GetElementType().FullName.Replace('/', '+') + "." + memberName;

    private static string MemberId(TypeReference declaringType, string memberName) => declaringType.GetElementType().FullName.Replace('/', '+') + "::" + memberName;

    private static string MethodId(MethodReference method) => MemberId(method.DeclaringType, method.Name);

    private void ScanMappers(MethodDefinition method, HandlerModel model)
    {
        string lastProperty = null;
        string lastMapper = null;
        var loadedMappers = new List<string>();
        var constructing = false;
        GenericInstanceType constructingType = null;
        var pendingKeys = new List<string>();
        var pendingChain = new List<string>();
        var pendingHelpers = new List<string>();

        void AddKey(string target, string key)
        {
            if (key == null || target == null)
            {
                return;
            }

            if (target == ArgumentMapper)
            {
                var id = MethodId(method);
                if (!model.HelperKeys.TryGetValue(id, out var keys))
                {
                    model.HelperKeys[id] = keys = new HashSet<string>();
                }

                keys.Add(key);
            }
            else
            {
                GetMapper(model, target, null).Keys.Add(key);
            }
        }

        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldsfld:
                case Code.Ldfld:
                {
                    var field = (FieldReference)instruction.Operand;
                    if (IsDependencyProperty(field.FieldType))
                    {
                        lastProperty = PropertyId(field.DeclaringType, field.Name);
                    }
                    else if (IsMapperType(field.FieldType))
                    {
                        lastMapper = MemberId(field.DeclaringType, field.Name);
                        loadedMappers.Add(lastMapper);
                    }

                    break;
                }

                case Code.Ldarg:
                case Code.Ldarg_S:
                case Code.Ldarg_0:
                case Code.Ldarg_1:
                case Code.Ldarg_2:
                case Code.Ldarg_3:
                {
                    var parameter = ArgumentOf(method, instruction);
                    if (parameter != null && IsMapperType(parameter.ParameterType))
                    {
                        lastMapper = ArgumentMapper;
                    }

                    break;
                }

                case Code.Newobj:
                {
                    var ctor = (MethodReference)instruction.Operand;
                    if (IsMapperType(ctor.DeclaringType))
                    {
                        constructing = true;
                        constructingType = ctor.DeclaringType as GenericInstanceType;
                        pendingChain.Clear();
                        pendingChain.AddRange(loadedMappers.Where(m => m != ArgumentMapper));
                        pendingKeys.Clear();
                        pendingHelpers.Clear();
                        loadedMappers.Clear();
                    }

                    break;
                }

                case Code.Call:
                case Code.Callvirt:
                {
                    var called = (MethodReference)instruction.Operand;
                    if (IsPropertyGetter(called, out var dependencyProperty))
                    {
                        lastProperty = dependencyProperty;
                    }
                    else if (called.HasThis == false && called.Parameters.Count == 0 && called.Name.StartsWith("get_", StringComparison.Ordinal) && IsMapperType(called.ReturnType))
                    {
                        lastMapper = MemberId(called.DeclaringType, called.Name.Substring(4));
                        loadedMappers.Add(lastMapper);
                    }
                    else if (IsKeyMethod(called))
                    {
                        if (constructing)
                        {
                            pendingKeys.Add(lastProperty);
                        }
                        else
                        {
                            AddKey(lastMapper, lastProperty);
                        }

                        lastProperty = null;
                    }
                    else if (IsMapperHelper(called))
                    {
                        var helper = MethodId(called);
                        if (constructing)
                        {
                            pendingHelpers.Add(helper);
                        }
                        else if (lastMapper != null && lastMapper != ArgumentMapper)
                        {
                            GetMapper(model, lastMapper, null).Helpers.Add(helper);
                        }
                    }

                    break;
                }

                case Code.Stsfld:
                case Code.Stfld:
                {
                    var field = (FieldReference)instruction.Operand;
                    if (IsMapperType(field.FieldType))
                    {
                        var id = MemberId(field.DeclaringType, field.Name);
                        var typed = constructingType ?? field.FieldType as GenericInstanceType;
                        var mapper = GetMapper(model, id, typed);
                        if (constructing)
                        {
                            foreach (var key in pendingKeys.Where(k => k != null))
                            {
                                mapper.Keys.Add(key);
                            }

                            mapper.Chained.AddRange(pendingChain.Where(c => c != id && !mapper.Chained.Contains(c)));
                            mapper.Helpers.AddRange(pendingHelpers.Where(h => !mapper.Helpers.Contains(h)));
                        }

                        constructing = false;
                        constructingType = null;
                        pendingKeys.Clear();
                        pendingChain.Clear();
                        pendingHelpers.Clear();
                        loadedMappers.Clear();
                    }

                    break;
                }
            }
        }
    }

    private MapperDefinition GetMapper(HandlerModel model, string id, GenericInstanceType typed)
    {
        if (!model.Mappers.TryGetValue(id, out var mapper))
        {
            string element = null;
            string handler = null;
            if (typed != null && typed.GenericArguments.Count == 2)
            {
                element = typed.GenericArguments[0].FullName.Replace('/', '+');
                handler = typed.GenericArguments[1].FullName.Replace('/', '+');
            }

            mapper = new MapperDefinition(id, element, handler);
            model.Mappers[id] = mapper;
        }
        else if (mapper.ElementType == null && typed != null && typed.GenericArguments.Count == 2)
        {
            var replacement = new MapperDefinition(id, typed.GenericArguments[0].FullName.Replace('/', '+'), typed.GenericArguments[1].FullName.Replace('/', '+'));
            replacement.Keys.UnionWith(mapper.Keys);
            replacement.Chained.AddRange(mapper.Chained);
            replacement.Helpers.AddRange(mapper.Helpers);
            model.Mappers[id] = replacement;
            mapper = replacement;
        }

        return mapper;
    }

    /// <summary>The mapper a handler's constructor passes to its base constructor (following base types).</summary>
    private string FindHandlerMapper(TypeDefinition type, int depth)
    {
        if (type == null || depth > 8)
        {
            return null;
        }

        foreach (var ctor in type.Methods.Where(m => m.IsConstructor && !m.IsStatic && m.HasBody))
        {
            string lastMapper = null;
            foreach (var instruction in ctor.Body.Instructions)
            {
                if ((instruction.OpCode.Code == Code.Ldsfld) && instruction.Operand is FieldReference field && IsMapperType(field.FieldType))
                {
                    lastMapper = MemberId(field.DeclaringType, field.Name);
                }
                else if (instruction.OpCode.Code == Code.Call && instruction.Operand is MethodReference called)
                {
                    if (!called.HasThis && called.Parameters.Count == 0 && called.Name.StartsWith("get_", StringComparison.Ordinal) && IsMapperType(called.ReturnType))
                    {
                        lastMapper = MemberId(called.DeclaringType, called.Name.Substring(4));
                    }
                    else if (called.Name == ".ctor" && lastMapper != null)
                    {
                        return lastMapper;
                    }
                }
            }
        }

        return FindHandlerMapper(_set.Resolve(type.BaseType), depth + 1);
    }

    private void ScanRegistrations(string assemblyName, MethodDefinition method, HandlerModel model)
    {
        MethodReference lastFunction = null;
        foreach (var instruction in method.Body.Instructions)
        {
            if ((instruction.OpCode.Code == Code.Ldftn || instruction.OpCode.Code == Code.Ldvirtftn) && instruction.Operand is MethodReference function)
            {
                lastFunction = function;
                continue;
            }

            if ((instruction.OpCode.Code != Code.Call && instruction.OpCode.Code != Code.Callvirt)
                || instruction.Operand is not GenericInstanceMethod register
                || register.Name != _conventions.RegistrationMethod
                || !_conventions.RegistrationTypes.Contains(register.DeclaringType.GetElementType().FullName))
            {
                continue;
            }

            var elementArgument = register.GenericArguments[0];
            if (elementArgument is GenericParameter)
            {
                continue;
            }

            var element = elementArgument.FullName.Replace('/', '+');
            HandlerRegistration registration;
            if (register.GenericArguments.Count == 2)
            {
                var handler = register.GenericArguments[1].FullName.Replace('/', '+');
                registration = handler == _conventions.FallbackHandler
                    ? new HandlerRegistration(element, RegistrationKind.Fallback, Array.Empty<string>(), assemblyName)
                    : new HandlerRegistration(element, RegistrationKind.Native, new[] { handler }, assemblyName);
            }
            else
            {
                var created = new List<string>();
                CollectCreatedHandlers(_set.Resolve(lastFunction), created, new HashSet<string>(), 0);
                var natives = created.Where(h => h != _conventions.FallbackHandler).Distinct().ToList();
                var kind = natives.Count > 0 ? RegistrationKind.Native : created.Count > 0 ? RegistrationKind.Fallback : RegistrationKind.CorePath;
                registration = new HandlerRegistration(element, kind, natives, assemblyName);
            }

            model.Registrations[element] = registration;
            lastFunction = null;
        }
    }

    private void CollectCreatedHandlers(MethodDefinition factory, List<string> created, HashSet<string> seen, int depth)
    {
        if (factory == null || !factory.HasBody || depth > 3 || !seen.Add(MethodId(factory)))
        {
            return;
        }

        foreach (var instruction in factory.Body.Instructions)
        {
            if (instruction.OpCode.Code == Code.Newobj && instruction.Operand is MethodReference ctor)
            {
                var type = _set.Resolve(ctor.DeclaringType);
                if (type != null && _set.Implements(type, _conventions.HandlerInterface))
                {
                    created.Add(type.FullName.Replace('/', '+'));
                }
            }
            else if ((instruction.OpCode.Code == Code.Call || instruction.OpCode.Code == Code.Callvirt) && instruction.Operand is MethodReference called && !called.HasThis)
            {
                var returned = _set.Resolve(called.ReturnType);
                if (returned != null && (returned.FullName == _conventions.HandlerInterface || _set.Implements(returned, _conventions.HandlerInterface)))
                {
                    CollectCreatedHandlers(_set.Resolve(called), created, seen, depth + 1);
                }
            }
        }
    }

    private static ParameterDefinition ArgumentOf(MethodDefinition method, Instruction instruction)
    {
        int index;
        switch (instruction.OpCode.Code)
        {
            case Code.Ldarg_0: index = 0; break;
            case Code.Ldarg_1: index = 1; break;
            case Code.Ldarg_2: index = 2; break;
            case Code.Ldarg_3: index = 3; break;
            default: return instruction.Operand as ParameterDefinition;
        }

        if (method.HasThis)
        {
            index--;
        }

        return index >= 0 && index < method.Parameters.Count ? method.Parameters[index] : null;
    }

    private bool IsDependencyProperty(TypeReference type) => type != null && type.FullName == _conventions.DependencyPropertyType;

    private bool IsPropertyGetter(MethodReference method, out string id)
    {
        id = null;
        if (method.HasThis || method.Parameters.Count != 0 || !method.Name.StartsWith("get_", StringComparison.Ordinal) || !IsDependencyProperty(method.ReturnType))
        {
            return false;
        }

        id = PropertyId(method.DeclaringType, method.Name.Substring(4));
        return true;
    }

    private bool IsMapperType(TypeReference type)
    {
        if (type == null)
        {
            return false;
        }

        var element = type.GetElementType();
        return _conventions.IsMapperTypeName(element.Namespace, element.Name);
    }

    private bool IsKeyMethod(MethodReference method)
    {
        var declaring = method.DeclaringType.GetElementType();
        if (_conventions.IsMapperTypeName(declaring.Namespace, declaring.Name) && _conventions.MapperKeyMethods.Contains(method.Name))
        {
            return true;
        }

        return declaring.Namespace == _conventions.MapperNamespace && declaring.Name == _conventions.MapperExtensionsType
            && _conventions.MapperExtensionMethods.Contains(method.Name);
    }

    private bool IsMapperHelper(MethodReference method) =>
        !method.HasThis && method.Parameters.Count >= 1 && IsMapperType(method.Parameters[0].ParameterType) && IsMapperType(method.ReturnType)
        && !IsKeyMethod(method);
}

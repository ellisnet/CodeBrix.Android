using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Reqnroll;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>One step definition: a method with a Given/When/Then cucumber expression.</summary>
internal sealed record StepDefinition(StepKeyword Keyword, CucumberExpression Expression, MethodInfo Method);

/// <summary>
/// Every [Binding] class of the scenario code, indexed: step definitions per keyword,
/// argument transformations per target type, and the hooks. Built once by reflection over
/// the device assembly (the same discovery Reqnroll does on the host for its bindings).
/// </summary>
internal sealed class StepCatalog
{
    private readonly Dictionary<StepKeyword, List<StepDefinition>> _steps = new();
    private readonly Dictionary<Type, MethodInfo> _transformations = new();

    private StepCatalog()
    {
    }

    /// <summary>The binding classes.</summary>
    internal IReadOnlyList<Type> BindingTypes { get; private set; } = Array.Empty<Type>();

    /// <summary>The hooks of a kind, in Order.</summary>
    internal IReadOnlyList<MethodInfo> Hooks<TAttribute>()
        where TAttribute : HookAttribute =>
        BindingTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<TAttribute>()))
            .Where(p => p.Attribute != null)
            .OrderBy(p => p.Attribute!.Order)
            .ThenBy(p => p.Method.DeclaringType!.FullName, StringComparer.Ordinal)
            .ThenBy(p => p.Method.Name, StringComparer.Ordinal)
            .Select(p => p.Method)
            .ToList();

    /// <summary>The number of step definitions.</summary>
    internal int StepCount => _steps.Values.Sum(l => l.Count);

    /// <summary>Builds the catalog from an assembly.</summary>
    internal static StepCatalog Build(Assembly assembly)
    {
        var catalog = new StepCatalog();
        var bindings = assembly.GetTypes().Where(t => t.GetCustomAttribute<BindingAttribute>() != null).ToList();
        catalog.BindingTypes = bindings;
        foreach (var type in bindings)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (var attribute in method.GetCustomAttributes<StepDefinitionBaseAttribute>())
                {
                    if (!catalog._steps.TryGetValue(attribute.Keyword, out var list))
                    {
                        catalog._steps[attribute.Keyword] = list = new List<StepDefinition>();
                    }

                    list.Add(new StepDefinition(attribute.Keyword, CucumberExpression.Compile(attribute.Expression), method));
                }

                if (method.GetCustomAttribute<StepArgumentTransformationAttribute>() != null)
                {
                    catalog._transformations[method.ReturnType] = method;
                }
            }
        }

        return catalog;
    }

    /// <summary>The definitions of a keyword whose expression matches the text, with their raw arguments.</summary>
    internal List<(StepDefinition Definition, string[] Arguments)> Match(StepKeyword keyword, string text)
    {
        var matches = new List<(StepDefinition, string[])>();
        if (_steps.TryGetValue(keyword, out var list))
        {
            foreach (var definition in list)
            {
                var arguments = definition.Expression.Match(text);
                if (arguments != null)
                {
                    matches.Add((definition, arguments));
                }
            }
        }

        return matches;
    }

    /// <summary>Converts a raw argument to a parameter type (built-in conversions, enums, then transformations).</summary>
    internal object? Convert(string raw, Type target, Func<Type, object> instanceOf)
    {
        if (target == typeof(string))
        {
            return raw;
        }

        if (target == typeof(int))
        {
            return int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        if (target == typeof(long))
        {
            return long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        if (target == typeof(double))
        {
            return CucumberExpression.ParseFloat(raw);
        }

        if (target == typeof(float))
        {
            return (float)CucumberExpression.ParseFloat(raw);
        }

        if (target == typeof(decimal))
        {
            return decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (target == typeof(bool))
        {
            return bool.Parse(raw);
        }

        if (_transformations.TryGetValue(target, out var transformation))
        {
            var owner = transformation.IsStatic ? null : instanceOf(transformation.DeclaringType!);
            return Invoke(transformation, owner, new object[] { raw });
        }

        if (target.IsEnum)
        {
            return Enum.Parse(target, raw, ignoreCase: true);
        }

        return System.Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
    }

    /// <summary>Invokes a method, unwrapping TargetInvocationException.</summary>
    internal static object? Invoke(MethodInfo method, object? target, object?[] arguments)
    {
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
            throw;
        }
    }
}

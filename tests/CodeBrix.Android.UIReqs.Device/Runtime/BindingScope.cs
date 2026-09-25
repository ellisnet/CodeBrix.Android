using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Reqnroll;
using Reqnroll.UnitTestProvider;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>Thrown when a hook or step skips the scenario (IUnitTestRuntimeProvider.TestIgnore).</summary>
internal sealed class ScenarioSkippedException : Exception
{
    internal ScenarioSkippedException(string reason) : base(reason)
    {
    }
}

/// <summary>Thrown for a pending step.</summary>
internal sealed class StepPendingException : Exception
{
    internal StepPendingException(string reason) : base(reason)
    {
    }
}

/// <summary>
/// The per-scenario object container (Reqnroll's context injection): one instance per binding
/// class per scenario, constructed with the scenario's contexts, output helper, unit-test
/// runtime and any other binding/context class it asks for.
/// </summary>
internal sealed class BindingScope : IReqnrollOutputHelper, IUnitTestRuntimeProvider
{
    private readonly Dictionary<Type, object> _instances = new();
    private readonly List<string> _output = new();

    internal BindingScope(FeatureContext featureContext, ScenarioContext scenarioContext)
    {
        FeatureContext = featureContext;
        ScenarioContext = scenarioContext;
        _instances[typeof(FeatureContext)] = featureContext;
        _instances[typeof(ScenarioContext)] = scenarioContext;
        _instances[typeof(IReqnrollOutputHelper)] = this;
        _instances[typeof(IUnitTestRuntimeProvider)] = this;
    }

    internal FeatureContext FeatureContext { get; }

    internal ScenarioContext ScenarioContext { get; }

    /// <summary>Takes (and clears) the output lines written so far.</summary>
    internal List<string> TakeOutput()
    {
        lock (_output)
        {
            var taken = _output.ToList();
            _output.Clear();
            return taken;
        }
    }

    /// <summary>The scenario's instance of a type (created on first use).</summary>
    internal object Get(Type type)
    {
        if (_instances.TryGetValue(type, out var existing))
        {
            return existing;
        }

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"{type.FullName} has no public constructor for context injection.");
        var arguments = constructor.GetParameters().Select(p => Get(p.ParameterType)).ToArray();
        var instance = constructor.Invoke(arguments);
        _instances[type] = instance;
        return instance;
    }

    /// <summary>Runs hooks (static or instance), awaiting async ones.</summary>
    internal async Task RunHooksAsync(IEnumerable<MethodInfo> hooks)
    {
        foreach (var hook in hooks)
        {
            var target = hook.IsStatic ? null : Get(hook.DeclaringType!);
            var arguments = hook.GetParameters().Select(p => Get(p.ParameterType)).ToArray();
            if (StepCatalog.Invoke(hook, target, arguments) is Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void WriteLine(string message)
    {
        lock (_output)
        {
            _output.Add(message);
        }
    }

    /// <inheritdoc />
    public void AddAttachment(string filePath) => WriteLine("attachment: " + filePath);

    /// <inheritdoc />
    public void TestIgnore(string message) => throw new ScenarioSkippedException(message);

    /// <inheritdoc />
    public void TestPending(string message) => throw new StepPendingException(message);

    /// <inheritdoc />
    public void TestInconclusive(string message) => throw new ScenarioSkippedException(message);
}

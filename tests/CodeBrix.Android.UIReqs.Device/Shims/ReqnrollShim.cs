using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Reqnroll;

// The Reqnroll surface the copied scenario code (Scenarios/) is written against, re-implemented for
// the device: the scenarios run on the device, driven step by step by the host-side Reqnroll runner
// (tests/CodeBrix.Android.UIReqs), so the device needs the attributes and contexts, not Reqnroll.

/// <summary>Marks a class that holds step definitions, transformations or hooks.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BindingAttribute : Attribute
{
}

/// <summary>The keyword of a step definition.</summary>
public enum StepKeyword
{
    /// <summary>Given.</summary>
    Given,

    /// <summary>When.</summary>
    When,

    /// <summary>Then.</summary>
    Then,
}

/// <summary>The base of the step definition attributes (a cucumber expression).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public abstract class StepDefinitionBaseAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    protected StepDefinitionBaseAttribute(string expression) => Expression = expression;

    /// <summary>The cucumber expression.</summary>
    public string Expression { get; }

    /// <summary>The keyword.</summary>
    public abstract StepKeyword Keyword { get; }
}

/// <summary>A Given step definition.</summary>
public sealed class GivenAttribute : StepDefinitionBaseAttribute
{
    /// <summary>Creates the attribute.</summary>
    public GivenAttribute(string expression) : base(expression)
    {
    }

    /// <inheritdoc />
    public override StepKeyword Keyword => StepKeyword.Given;
}

/// <summary>A When step definition.</summary>
public sealed class WhenAttribute : StepDefinitionBaseAttribute
{
    /// <summary>Creates the attribute.</summary>
    public WhenAttribute(string expression) : base(expression)
    {
    }

    /// <inheritdoc />
    public override StepKeyword Keyword => StepKeyword.When;
}

/// <summary>A Then step definition.</summary>
public sealed class ThenAttribute : StepDefinitionBaseAttribute
{
    /// <summary>Creates the attribute.</summary>
    public ThenAttribute(string expression) : base(expression)
    {
    }

    /// <inheritdoc />
    public override StepKeyword Keyword => StepKeyword.Then;
}

/// <summary>Marks a method that converts step text to a parameter type.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StepArgumentTransformationAttribute : Attribute
{
}

/// <summary>The base of the hook attributes.</summary>
[AttributeUsage(AttributeTargets.Method)]
public abstract class HookAttribute : Attribute
{
    /// <summary>The order among hooks of the same kind (lower first).</summary>
    public int Order { get; set; } = 10000;
}

/// <summary>Runs before each scenario.</summary>
public sealed class BeforeScenarioAttribute : HookAttribute
{
}

/// <summary>Runs after each scenario.</summary>
public sealed class AfterScenarioAttribute : HookAttribute
{
}

/// <summary>Runs once before the first scenario.</summary>
public sealed class BeforeTestRunAttribute : HookAttribute
{
}

/// <summary>Runs once after the last scenario.</summary>
public sealed class AfterTestRunAttribute : HookAttribute
{
}

/// <summary>How a scenario ended so far.</summary>
public enum ScenarioExecutionStatus
{
    /// <summary>Every step passed.</summary>
    OK,

    /// <summary>A step was pending.</summary>
    StepDefinitionPending,

    /// <summary>A step had no definition.</summary>
    UndefinedStep,

    /// <summary>A binding could not be invoked.</summary>
    BindingError,

    /// <summary>A step failed.</summary>
    TestError,

    /// <summary>The scenario was skipped.</summary>
    Skipped,
}

/// <summary>What the scenario is.</summary>
public sealed class ScenarioInfo
{
    /// <summary>Creates the info.</summary>
    public ScenarioInfo(string title, string[] tags, string[] combinedTags)
    {
        Title = title;
        Tags = tags;
        CombinedTags = combinedTags;
    }

    /// <summary>The scenario title.</summary>
    public string Title { get; }

    /// <summary>The scenario's own tags.</summary>
    public string[] Tags { get; }

    /// <summary>The scenario's tags plus its feature's.</summary>
    public string[] CombinedTags { get; }
}

/// <summary>What the feature is.</summary>
public sealed class FeatureInfo
{
    /// <summary>Creates the info.</summary>
    public FeatureInfo(string title, string folderPath, string[] tags)
    {
        Title = title;
        FolderPath = folderPath;
        Tags = tags;
    }

    /// <summary>The feature title.</summary>
    public string Title { get; }

    /// <summary>The feature file's folder, relative to the project (e.g. "Features/Harness").</summary>
    public string FolderPath { get; }

    /// <summary>The feature's tags.</summary>
    public string[] Tags { get; }
}

/// <summary>The per-scenario state bag (a dictionary, like Reqnroll's).</summary>
public sealed class ScenarioContext : Dictionary<string, object>
{
    /// <summary>Creates the context.</summary>
    public ScenarioContext(ScenarioInfo scenarioInfo) : base(StringComparer.Ordinal) => ScenarioInfo = scenarioInfo;

    /// <summary>The scenario.</summary>
    public ScenarioInfo ScenarioInfo { get; }

    /// <summary>How the scenario is going.</summary>
    public ScenarioExecutionStatus ScenarioExecutionStatus { get; set; }
}

/// <summary>The per-feature state bag.</summary>
public sealed class FeatureContext : Dictionary<string, object>
{
    /// <summary>Creates the context.</summary>
    public FeatureContext(FeatureInfo featureInfo) : base(StringComparer.Ordinal) => FeatureInfo = featureInfo;

    /// <summary>The feature.</summary>
    public FeatureInfo FeatureInfo { get; }
}

/// <summary>A step's table argument.</summary>
public sealed class DataTable
{
    /// <summary>Creates a table.</summary>
    public DataTable(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        Header = header.ToList();
        Rows = rows.Select(r => new DataTableRow(this, r)).ToList();
    }

    /// <summary>The column names.</summary>
    public ICollection<string> Header { get; }

    /// <summary>The rows.</summary>
    public IReadOnlyList<DataTableRow> Rows { get; }
}

/// <summary>A row of a <see cref="DataTable"/>.</summary>
public sealed class DataTableRow : IEnumerable<KeyValuePair<string, string>>
{
    private readonly List<string> _header;
    private readonly IReadOnlyList<string> _values;

    internal DataTableRow(DataTable table, IReadOnlyList<string> values)
    {
        _header = table.Header.ToList();
        _values = values;
    }

    /// <summary>The cell of a column.</summary>
    public string this[string column] => _values[_header.IndexOf(column)];

    /// <summary>The cell at an index.</summary>
    public string this[int index] => _values[index];

    /// <summary>The cells.</summary>
    public IReadOnlyList<string> Values => _values;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
        _header.Select((h, i) => new KeyValuePair<string, string>(h, _values[i])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Scenario output that reaches the test report (on the device: returned to the host).</summary>
public interface IReqnrollOutputHelper
{
    /// <summary>Writes a line.</summary>
    void WriteLine(string message);

    /// <summary>Names an attachment file.</summary>
    void AddAttachment(string filePath);
}

using PicoScope.Brix.ScopeData.Model;

namespace PicoScope.Brix.ViewModels;

/// <summary>
/// A voltage range as a combo-box item: the range, plus the label the box shows.
/// </summary>
/// <remarks>
/// A ComboBox with no item template displays each item's <see cref="ToString"/>,
/// so binding the enum directly would show its member name ("Range5V"). This
/// wrapper shows the range the way a scope labels it ("+/-5 V"), with no
/// template or converter needed on any head.
/// </remarks>
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class VoltageRangeOption
{
    /// <summary>
    /// Creates the item for one range.
    /// </summary>
    /// <param name="range">The range this item selects.</param>
    public VoltageRangeOption(VoltageRange range)
    {
        Range = range;
        Label = range.ToDisplayString();
    }

    /// <summary>The range this item selects.</summary>
    public VoltageRange Range { get; }

    /// <summary>The text the combo box shows for this range.</summary>
    public string Label { get; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

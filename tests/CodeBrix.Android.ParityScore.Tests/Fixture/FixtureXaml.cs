using System;

// A miniature object model with the shapes of the Core assemblies (read by the scanners through this test assembly's IL).
namespace ParityFixture.Xaml;

public sealed class DependencyProperty
{
}

[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class NotImplementedAttribute : Attribute
{
    public NotImplementedAttribute()
    {
    }

    public NotImplementedAttribute(params string[] platforms)
    {
        Platforms = platforms;
    }

    public string[] Platforms { get; }
}

public static class ApiInformation
{
    public static void TryRaiseNotImplemented(string type, string member)
    {
    }
}

public class UIElement
{
    public static DependencyProperty OpacityProperty { get; } = new();

    public static DependencyProperty IsHitTestVisibleProperty { get; } = new();
}

public class FrameworkElement : UIElement
{
    public static DependencyProperty WidthProperty { get; } = new();

    public static DependencyProperty TagProperty { get; } = new();
}

public class Control : FrameworkElement
{
    public static DependencyProperty ForegroundProperty { get; } = new();

    public static DependencyProperty TemplateProperty { get; } = new();
}

public class Button : Control
{
    public static DependencyProperty ContentProperty { get; } = new();

    public static readonly DependencyProperty FlyoutProperty = new();
}

public class Slider : Control
{
    public static DependencyProperty ValueProperty { get; } = new();

    public static DependencyProperty StepFrequencyProperty { get; } = new();
}

public class Partly
{
    [NotImplemented]
    public void Marked()
    {
    }

    [NotImplemented("IS_UNIT_TESTS")]
    public void MarkedForUnitTestsOnly()
    {
    }

    public void Throws() => throw new NotImplementedException();

    public int Implemented { get; set; }

    [NotImplemented("__SKIA__", "__CODEBRIX_CORE__")]
    public event EventHandler MarkedEvent
    {
        add { }
        remove { }
    }
}

[NotImplemented]
public class Missing
{
    public void First()
    {
    }

    public int Second { get; set; }
}

public class Raising
{
    public void Reports() => ApiInformation.TryRaiseNotImplemented("Raising", "Reports");
}

public class Complete
{
    public void Works()
    {
    }
}

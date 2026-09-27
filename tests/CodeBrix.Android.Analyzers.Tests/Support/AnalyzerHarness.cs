using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using CodeBrix.Android.Analyzers.CSharp;
using CodeBrix.Android.Analyzers.Xaml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CodeBrix.Android.Analyzers.Tests.Support;

/// <summary>Compiles test code against a miniature CodeBrix.Platform object model and runs the CBAND analyzers.</summary>
internal static class AnalyzerHarness
{
    /// <summary>The miniature object model (the names and shapes the rules look for).</summary>
    internal const string PlatformSource = """
        namespace Microsoft.UI.Xaml
        {
            public sealed class DependencyProperty { }
            public class DependencyObject { public void SetValue(DependencyProperty property, object value) { } }
            public class UIElement : DependencyObject
            {
                public object Projection { get; set; }
                public object Transform3D { get; set; }
                public object Shadow { get; set; }
            }
            public class FrameworkElement : UIElement { }
            public class ControlTemplate { }
            public static class VisualStateManager { public static bool GoToState(Microsoft.UI.Xaml.Controls.Control control, string state, bool useTransitions) => false; }
            public class Window { public Microsoft.UI.Xaml.Media.SystemBackdrop SystemBackdrop { get; set; } }
            public class Style { }
            public class Setter { }
        }
        namespace Microsoft.UI.Xaml.Controls
        {
            public class Control : FrameworkElement
            {
                public static DependencyProperty TemplateProperty { get; } = new DependencyProperty();
                public ControlTemplate Template { get; set; }
                protected virtual void OnApplyTemplate() { }
                protected DependencyObject GetTemplateChild(string name) => null;
            }
            public class ContentControl : Control { }
            public class UserControl : Control { }
            public class Button : ContentControl { }
            public enum ZoomMode { Disabled, Enabled }
            public class ScrollViewer : ContentControl
            {
                public ZoomMode ZoomMode { get; set; }
                public static void SetZoomMode(DependencyObject element, ZoomMode mode) { }
            }
            public class PasswordBox : Control { public string PasswordChar { get; set; } }
            public class Grid : FrameworkElement { }
        }
        namespace Microsoft.UI.Xaml.Controls.Primitives { public class MonochromaticOverlayPresenter : FrameworkElement { } }
        namespace Microsoft.UI.Xaml.Media
        {
            public class SystemBackdrop { }
            public class MicaBackdrop : SystemBackdrop { }
            public class CustomBackdrop : SystemBackdrop { }
            public class AcrylicBrush { }
            public class ThemeShadow { }
            public class PlaneProjection { }
            public class SolidColorBrush { }
        }
        namespace Microsoft.UI.Xaml.Hosting { public static class ElementCompositionPreview { public static object GetElementVisual(Microsoft.UI.Xaml.UIElement element) => null; } }
        namespace Microsoft.UI.Composition { public class Compositor { public object CreateSpriteVisual() => null; } }
        namespace CodeBrix.Platform.UI.Runtime.Skia { public class SoftwareKeyboardOptions { } }
        """;

    /// <summary>Compiles <paramref name="sources"/> (with the object model unless <paramref name="withPlatform"/> is false).</summary>
    internal static CSharpCompilation Compile(bool withPlatform, params string[] sources)
    {
        var trees = sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Test{i}.cs")).ToList();
        if (withPlatform)
        {
            trees.Add(CSharpSyntaxTree.ParseText(PlatformSource, path: "Platform.cs"));
        }

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) is "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("CbandTest", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("test source does not compile: " + string.Join("; ", errors));
        }

        return compilation;
    }

    /// <summary>Runs both CBAND analyzers and returns their diagnostics, sorted by position.</summary>
    internal static IReadOnlyList<Diagnostic> Run(CSharpCompilation compilation, IEnumerable<(string Path, string Text)> xaml = null, IDictionary<string, string> globalOptions = null)
    {
        var additional = (xaml ?? Enumerable.Empty<(string, string)>()).Select(x => (AdditionalText)new TestAdditionalText(x.Item1, x.Item2)).ToImmutableArray();
        var options = new AnalyzerOptions(additional, new TestOptionsProvider(globalOptions ?? new Dictionary<string, string>()));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CbandCSharpAnalyzer(), new CbandXamlAnalyzer());
        var diagnostics = compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return diagnostics.OrderBy(d => d.Location.GetLineSpan().Path, StringComparer.Ordinal).ThenBy(d => d.Location.SourceSpan.Start).ToList();
    }

    /// <summary>Runs the C# analyzer over one source with the object model.</summary>
    internal static IReadOnlyList<Diagnostic> RunCSharp(string source) => Run(Compile(true, source));

    /// <summary>Scans XAML with an oracle over the object model (plus <paramref name="extraSource"/>).</summary>
    internal static IReadOnlyList<XamlFinding> ScanXaml(string xaml, string extraSource = "")
    {
        var oracle = new CompilationTypeOracle(Compile(true, extraSource));
        return XamlScanner.Scan(xaml, oracle);
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly string _text;

        internal TestAdditionalText(string path, string text)
        {
            Path = path;
            _text = text;
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(_text);
    }

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestOptions _global;

        internal TestOptionsProvider(IDictionary<string, string> global)
        {
            _global = new TestOptions(global);
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestOptions.Empty;
    }

    private sealed class TestOptions : AnalyzerConfigOptions
    {
        internal static readonly TestOptions Empty = new(new Dictionary<string, string>());

        private readonly IDictionary<string, string> _values;

        internal TestOptions(IDictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
    }
}

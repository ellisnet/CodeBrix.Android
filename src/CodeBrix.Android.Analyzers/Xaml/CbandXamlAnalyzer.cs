using System;
using System.Collections.Immutable;
using CodeBrix.Android.Analyzers.Diagnostics;
using CodeBrix.Android.Analyzers.Surface;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CodeBrix.Android.Analyzers.Xaml;

/// <summary>
/// The XAML scan: runs <see cref="XamlScanner"/> over every <c>.xaml</c> additional file of the compilation (the XAML
/// generator's build logic passes every Page and ApplicationDefinition item as an additional file) and reports each
/// finding at its XAML file and line. Turned off with the MSBuild property <c>CodeBrixAndroidXamlScan=false</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CbandXamlAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The MSBuild property (compiler-visible) that turns the scan off when it is <c>false</c>.</summary>
    public const string ScanProperty = "build_property.CodeBrixAndroidXamlScan";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        CbandDescriptors.ControlTemplateOnNativeControl,
        CbandDescriptors.CompositionIgnored,
        CbandDescriptors.ProjectionIgnored,
        CbandDescriptors.ZoomIgnored,
        CbandDescriptors.PasswordCharIgnored,
        CbandDescriptors.FrameBufferOptionIgnored,
        CbandDescriptors.MaterialFallback);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(AndroidSurface.Control) == null)
            {
                return;
            }

            if (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(ScanProperty, out var enabled)
                && string.Equals(enabled.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oracle = new CompilationTypeOracle(start.Compilation);
            start.RegisterAdditionalFileAction(file => AnalyzeFile(file, oracle));
        });
    }

    private static void AnalyzeFile(AdditionalFileAnalysisContext context, IXamlTypeOracle oracle)
    {
        var path = context.AdditionalFile.Path;
        if (!path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var text = context.AdditionalFile.GetText(context.CancellationToken);
        if (text == null)
        {
            return;
        }

        foreach (var finding in XamlScanner.Scan(text.ToString(), oracle))
        {
            var lineIndex = Math.Max(0, Math.Min(finding.Line - 1, text.Lines.Count - 1));
            var line = text.Lines[lineIndex];
            var start = Math.Min(line.Start + Math.Max(0, finding.Column - 1), line.End);
            var end = Math.Min(start + finding.Length, line.End);
            var span = TextSpan.FromBounds(start, end);
            var location = Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
            context.ReportDiagnostic(Diagnostic.Create(finding.Descriptor, location, ToObjects(finding)));
        }
    }

    private static object[] ToObjects(XamlFinding finding)
    {
        var args = new object[finding.Arguments.Count];
        for (var i = 0; i < args.Length; i++)
        {
            args[i] = finding.Arguments[i];
        }

        return args;
    }
}

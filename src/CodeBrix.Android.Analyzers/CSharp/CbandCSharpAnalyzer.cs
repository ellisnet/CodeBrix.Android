using System.Collections.Immutable;
using System.Linq;
using CodeBrix.Android.Analyzers.Diagnostics;
using CodeBrix.Android.Analyzers.Surface;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeBrix.Android.Analyzers.CSharp;

/// <summary>
/// The C# half of the CBAND diagnostics (the XAML half is <see cref="Xaml.CbandXamlAnalyzer"/>): reports, in hand-written
/// code, the constructs of the plan's "unmappable constructs" table that CodeBrix.Android accepts and ignores. Generated
/// code (the XAML generator's output) is not analysed: the XAML scan reports the XAML itself, at its own file and line.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CbandCSharpAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        CbandDescriptors.ControlTemplateOnNativeControl,
        CbandDescriptors.TemplateMemberOnNativeControl,
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
            // Only a compilation that sees the CodeBrix.Platform object model can contain these constructs.
            if (start.Compilation.GetTypeByMetadataName(AndroidSurface.Control) == null)
            {
                return;
            }

            start.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            start.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
            start.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        });
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation target)
        {
            return;
        }

        var property = target.Property;
        var receiver = ReceiverType(target.Instance, context.ContainingSymbol);
        var location = assignment.Syntax.GetLocation();
        switch (property.Name)
        {
            case "Template" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.Control) && !IsNull(assignment.Value):
            {
                var native = SymbolFacts.NativeControl(receiver);
                if (native != null)
                {
                    Report(context, CbandDescriptors.ControlTemplateOnNativeControl, location, "Control.Template", AndroidSurface.SimpleName(native));
                }

                break;
            }

            case "ZoomMode" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.ScrollViewer):
                if (IsEnumMember(assignment.Value, "Enabled"))
                {
                    Report(context, CbandDescriptors.ZoomIgnored, location, "ScrollViewer.ZoomMode = Enabled");
                }

                break;

            case "PasswordChar" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.PasswordBox):
                if (assignment.Value.ConstantValue.HasValue && assignment.Value.ConstantValue.Value is string text && text.Length != 1)
                {
                    Report(context, CbandDescriptors.PasswordCharIgnored, location, text);
                }

                break;

            case "Projection" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.UIElement):
            case "Transform3D" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.UIElement):
                if (!IsNull(assignment.Value) && !IsCreation(assignment.Value))
                {
                    Report(context, CbandDescriptors.ProjectionIgnored, location, "UIElement." + property.Name);
                }

                break;

            case "Shadow" when SymbolFacts.DerivesFrom(property.ContainingType, AndroidSurface.UIElement):
                if (!IsNull(assignment.Value) && !IsCreation(assignment.Value))
                {
                    Report(context, CbandDescriptors.CompositionIgnored, location, "UIElement.Shadow");
                }

                break;
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var containing = SymbolFacts.FullName(method.ContainingType);
        var location = invocation.Syntax.GetLocation();

        if (containing == AndroidSurface.ElementCompositionPreview)
        {
            Report(context, CbandDescriptors.CompositionIgnored, location, "ElementCompositionPreview." + method.Name);
            return;
        }

        if (containing != null && containing.StartsWith(AndroidSurface.CompositionNamespace + ".", System.StringComparison.Ordinal))
        {
            Report(context, CbandDescriptors.CompositionIgnored, location, AndroidSurface.SimpleName(containing) + "." + method.Name);
            return;
        }

        if (method.ContainingType != null && AndroidSurface.FrameBufferOptionNames.Contains(method.ContainingType.Name))
        {
            Report(context, CbandDescriptors.FrameBufferOptionIgnored, location, method.ContainingType.Name);
            return;
        }

        switch (method.Name)
        {
            case "SetValue" when invocation.Arguments.Length == 2 && IsTemplateProperty(invocation.Arguments[0].Value):
            {
                var native = SymbolFacts.NativeControl(ReceiverType(invocation.Instance, context.ContainingSymbol));
                if (native != null)
                {
                    Report(context, CbandDescriptors.ControlTemplateOnNativeControl, location, "SetValue(Control.TemplateProperty)", AndroidSurface.SimpleName(native));
                }

                break;
            }

            case "GetTemplateChild" when SymbolFacts.DerivesFrom(method.ContainingType, AndroidSurface.Control):
            {
                var native = SymbolFacts.NativeControl(ReceiverType(invocation.Instance, context.ContainingSymbol));
                if (native != null)
                {
                    Report(context, CbandDescriptors.TemplateMemberOnNativeControl, location, "GetTemplateChild", AndroidSurface.SimpleName(native));
                }

                break;
            }

            case "GoToState" when containing == AndroidSurface.VisualStateManager && invocation.Arguments.Length > 0:
            {
                var native = SymbolFacts.NativeControl(Unwrap(invocation.Arguments[0].Value).Type);
                if (native != null)
                {
                    Report(context, CbandDescriptors.TemplateMemberOnNativeControl, location, "VisualStateManager.GoToState", AndroidSurface.SimpleName(native));
                }

                break;
            }

            case "SetZoomMode" when containing == AndroidSurface.ScrollViewer && invocation.Arguments.Length == 2:
                if (IsEnumMember(invocation.Arguments[1].Value, "Enabled"))
                {
                    Report(context, CbandDescriptors.ZoomIgnored, location, "ScrollViewer.SetZoomMode(Enabled)");
                }

                break;
        }
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var type = creation.Type;
        var name = SymbolFacts.FullName(type);
        if (name == null)
        {
            return;
        }

        var location = creation.Syntax.GetLocation();
        if (AndroidSurface.CompositionTypes.Contains(name) || name.StartsWith(AndroidSurface.CompositionNamespace + ".", System.StringComparison.Ordinal))
        {
            Report(context, CbandDescriptors.CompositionIgnored, location, AndroidSurface.SimpleName(name));
        }
        else if (AndroidSurface.ProjectionTypes.Contains(name))
        {
            Report(context, CbandDescriptors.ProjectionIgnored, location, AndroidSurface.SimpleName(name));
        }
        else if (AndroidSurface.MaterialTypes.Contains(name))
        {
            Report(context, CbandDescriptors.MaterialFallback, location, AndroidSurface.SimpleName(name));
        }
        else if (AndroidSurface.FrameBufferOptionNames.Contains(type.Name))
        {
            Report(context, CbandDescriptors.FrameBufferOptionIgnored, location, type.Name);
        }
        else if (SymbolFacts.DerivesFrom(type, AndroidSurface.SystemBackdrop))
        {
            Report(context, CbandDescriptors.CompositionIgnored, location, type.Name);
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!method.IsOverride || method.Name != "OnApplyTemplate" || method.Parameters.Length != 0)
        {
            return;
        }

        var native = SymbolFacts.NativeControl(method.ContainingType);
        if (native != null && method.Locations.FirstOrDefault(l => l.IsInSource) is { } location)
        {
            Report(context, CbandDescriptors.TemplateMemberOnNativeControl, location, "OnApplyTemplate override", AndroidSurface.SimpleName(native));
        }
    }

    private static ITypeSymbol ReceiverType(IOperation instance, ISymbol containingSymbol)
    {
        if (instance == null || instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance })
        {
            return containingSymbol?.ContainingType;
        }

        return Unwrap(instance).Type;
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    // An object creation is reported by the creation rule (one diagnostic per construct).
    private static bool IsCreation(IOperation value) => Unwrap(value) is IObjectCreationOperation;

    private static bool IsNull(IOperation value) => value.ConstantValue.HasValue && value.ConstantValue.Value == null;

    private static bool IsEnumMember(IOperation value, string member) =>
        Unwrap(value) is IFieldReferenceOperation field && field.Field.ContainingType?.TypeKind == TypeKind.Enum && field.Field.Name == member;

    private static bool IsTemplateProperty(IOperation value)
    {
        switch (Unwrap(value))
        {
            case IPropertyReferenceOperation property:
                return property.Property.Name == "TemplateProperty" && SymbolFacts.FullName(property.Property.ContainingType) == AndroidSurface.Control;
            case IFieldReferenceOperation field:
                return field.Field.Name == "TemplateProperty" && SymbolFacts.FullName(field.Field.ContainingType) == AndroidSurface.Control;
            default:
                return false;
        }
    }

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] args) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] args) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
}

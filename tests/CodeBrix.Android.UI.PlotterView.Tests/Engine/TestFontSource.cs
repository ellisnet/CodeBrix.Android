using System;
using System.Reflection;
using System.Threading.Tasks;
using CodeBrix.Platform.Foundation.Extensibility;
using SkiaSharp;

namespace CodeBrix.Android.UI.PlotterView.Tests.Engine;

/// <summary>
/// A stand-in for the platform font source the PlotterView engine asks for its typefaces
/// (IFontSourcePlatform&lt;SKTypeface&gt; in Foundation.Core; on Android the TextLayout add-in registers it). The contract
/// is internal to Foundation.Core and not granted to this test assembly, so the stand-in is a
/// <see cref="DispatchProxy"/> of it that answers every typeface request with Open Sans (embedded from the family's
/// font package). Registered once per process, as a platform bootstrap would.
/// </summary>
public class TestFontSource : DispatchProxy
{
    private static readonly object _gate = new();
    private static bool _registered;
    private static SKTypeface _typeface;

    /// <summary>How many typefaces the engine asked for.</summary>
    internal static int TypefaceRequests { get; private set; }

    /// <summary>Registers the stand-in (once).</summary>
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            using (var font = typeof(TestFontSource).Assembly.GetManifestResourceStream("OpenSans.ttf"))
            {
                _typeface = SKTypeface.FromStream(font);
            }

            var contract = typeof(ApiExtensibility).Assembly
                .GetType("CodeBrix.Platform.Foundation.Contracts.IFontSourcePlatform`1", throwOnError: true)
                .MakeGenericType(typeof(SKTypeface));
            var source = DispatchProxy.Create(contract, typeof(TestFontSource));
            ApiExtensibility.Register(contract, _ => source);
            _registered = true;
        }
    }

    /// <inheritdoc />
    protected override object Invoke(MethodInfo targetMethod, object[] args) => targetMethod?.Name switch
    {
        "get_DefaultTextFontFamily" => "Default",
        "get_SymbolsFont" => "Default",
        "get_RestrictToEmbeddedFonts" => false,
        "get_FallbackFontFamilies" => null,
        "GetTypefaceAsync" => Count(Task.FromResult(_typeface)),
        "GetLoadedEmbeddedDefaultTypeface" => Count(_typeface),
        _ => throw new NotSupportedException(targetMethod?.Name),
    };

    private static T Count<T>(T value)
    {
        TypefaceRequests++;
        return value;
    }
}

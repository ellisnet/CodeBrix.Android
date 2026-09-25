using System;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Text;
using AContext = global::Android.Content.Context;
using ALinearLayoutParams = global::Android.Widget.LinearLayout.LayoutParams;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using ATextView = global::Android.Widget.TextView;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a TextBox / PasswordBox (plan 3 rows TextBox, PasswordBox): the Header text above a
/// Material TextInputLayout (outlined box; its floating label is not used - WinUI's Header sits above the
/// box and PlaceholderText is the editor's hint) holding the <see cref="CodeBrixEditText"/>. The box fills
/// what Core gives the control below the header.
/// </summary>
internal sealed class TextBoxView : AViewGroup
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal TextBoxView(AContext context)
        : base(context)
    {
        Header = new ATextView(context) { Visibility = AViewStates.Gone };
        Header.SetIncludeFontPadding(false);
        Field = new ATextInputLayout(context)
        {
            HintEnabled = false,
            ErrorEnabled = false,
            HelperTextEnabled = false,
            CounterEnabled = false,
            BoxBackgroundMode = ATextInputLayout.BoxBackgroundOutline,
        };
        Editor = new CodeBrixEditText(Field.Context);
        Editor.SetIncludeFontPadding(false);
        Editor.SetMinHeight(0);
        Editor.SetMinimumHeight(0);
        Editor.SetMinWidth(0);
        Editor.SetMinimumWidth(0);
        Field.AddView(Editor, new ALinearLayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        AddView(Header);
        AddView(Field);
    }

    /// <summary>The header text (Gone without a header).</summary>
    internal ATextView Header { get; }

    /// <summary>The Material text field (box, end icon).</summary>
    internal ATextInputLayout Field { get; }

    /// <summary>The native editor.</summary>
    internal CodeBrixEditText Editor { get; }

    /// <summary>The gap below the header, in pixels.</summary>
    internal int HeaderGap { get; set; }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var unspecified = AMeasureSpecMode.Unspecified.MakeMeasureSpec(0);
        var headerHeight = 0;
        var headerWidth = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Measure(unspecified, unspecified);
            headerHeight = Header.MeasuredHeight + HeaderGap;
            headerWidth = Header.MeasuredWidth;
        }

        var heightMode = heightMeasureSpec.GetMode();
        var fieldHeightSpec = heightMode == AMeasureSpecMode.Unspecified
            ? unspecified
            : heightMode.MakeMeasureSpec(Math.Max(0, heightMeasureSpec.GetSize() - headerHeight));
        Field.Measure(widthMeasureSpec, fieldHeightSpec);
        var width = Math.Max(headerWidth, Field.MeasuredWidth);
        var height = headerHeight + Field.MeasuredHeight;
        SetMeasuredDimension(ResolveSize(width, widthMeasureSpec), ResolveSize(height, heightMeasureSpec));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var width = r - l;
        var height = b - t;
        var top = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Layout(0, 0, Math.Min(width, Header.MeasuredWidth), Header.MeasuredHeight);
            top = Header.MeasuredHeight + HeaderGap;
        }

        var fieldHeight = Math.Max(0, height - top);
        if (Field.MeasuredWidth != width || Field.MeasuredHeight != fieldHeight)
        {
            Field.Measure(MeasureSpecExtensions.Exactly(width), MeasureSpecExtensions.Exactly(fieldHeight));
        }

        Field.Layout(0, top, width, top + fieldHeight);
    }
}

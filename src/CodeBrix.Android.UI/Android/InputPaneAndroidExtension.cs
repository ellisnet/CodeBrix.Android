using CodeBrix.Android.UI.Hosting;
using Windows.UI.ViewManagement;
using AContext = global::Android.Content.Context;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;
using AShowFlags = global::Android.Views.InputMethods.ShowFlags;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android <see cref="IInputPaneExtension"/>: InputPane.TryShow/TryHide show or hide the soft
/// keyboard for the current activity's focused view. The keyboard's height reaches Core the other way
/// (the root insets listener -> the window wrapper -> InputPane.OccludedRect).
/// </summary>
internal sealed class InputPaneAndroidExtension : IInputPaneExtension
{
    /// <inheritdoc />
    public bool TryShow()
    {
        var activity = ActivityRegistry.Current;
        var view = activity?.CurrentFocus ?? activity?.RootLayout;
        if (view == null || activity.GetSystemService(AContext.InputMethodService) is not AInputMethodManager ime)
        {
            return false;
        }

        return ime.ShowSoftInput(view, AShowFlags.Implicit);
    }

    /// <inheritdoc />
    public bool TryHide()
    {
        var activity = ActivityRegistry.Current;
        var token = (activity?.CurrentFocus ?? activity?.RootLayout)?.WindowToken;
        if (token == null || activity.GetSystemService(AContext.InputMethodService) is not AInputMethodManager ime)
        {
            return false;
        }

        return ime.HideSoftInputFromWindow(token, 0);
    }
}

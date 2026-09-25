using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using AContext = global::Android.Content.Context;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IFocusPlatform"/> - BASIC until element
/// handlers own native views (AP2). Clearing focus (null) clears the native focus of the
/// current activity and hides the soft keyboard; focusing an element keeps the native
/// focus on the activity's root layout, so key events reach the activity.
/// </summary>
internal sealed class FocusAndroidPlatform : IFocusPlatform
{
    /// <inheritdoc />
    public void FocusNative(UIElement element)
    {
        var activity = ActivityRegistry.Current;
        if (activity == null)
        {
            return;
        }

        if (element == null)
        {
            var focused = activity.CurrentFocus;
            focused?.ClearFocus();
            if (focused?.WindowToken != null && activity.GetSystemService(AContext.InputMethodService) is AInputMethodManager ime)
            {
                ime.HideSoftInputFromWindow(focused.WindowToken, 0);
            }

            return;
        }

        var root = activity.RootLayout;
        if (root != null && !root.HasFocus)
        {
            root.RequestFocus();
        }
    }
}

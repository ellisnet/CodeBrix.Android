#if __ANDROID__
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AAlertDialog = global::AndroidX.AppCompat.App.AlertDialog;
using AButton = global::Android.Widget.Button;
using ADialogButtonType = global::Android.Content.DialogButtonType;
using AMaterialAlertDialogBuilder = global::Google.Android.Material.Dialog.MaterialAlertDialogBuilder;
using AOnBackPressedCallback = global::AndroidX.Activity.OnBackPressedCallback;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// A ContentDialog shown as a Material 3 dialog (plan 2.9 tier 2; MaterialAlertDialogBuilder): title, message,
/// and the dialog's Primary / Secondary / Close buttons in the Material positive / neutral / negative slots
/// (<see cref="DialogButtonSlots"/>). Every button goes through Core (ContentDialog.RaiseButtonFromPlatform:
/// the click event and its deferral, the command, Closing and its cancel) and the native dialog closes only
/// when Core says so (<see cref="CloseFromCore"/>): the buttons' own auto-dismiss is replaced after Show. The
/// Android back button / gesture acts as WinUI's Escape (the close button's path). The dialog is modal: a tap
/// outside it does nothing.
/// </summary>
/// <remarks>
/// A plain AppCompat AlertDialog, not a DialogFragment: a CodeBrix activity is never re-created by a
/// configuration change (rotation, resize and theme are handled in place), so the dialog survives them, and a
/// DialogFragment would come back after process death with no ContentDialog behind it.
/// </remarks>
internal sealed class NativeContentDialog : NativeOverlay
{
    private readonly ContentDialog _dialog;
    private readonly List<(DependencyProperty Property, long Token)> _callbacks = new();
    private AAlertDialog _native;
    private DialogButtonSlots _slots;
    private BackCallback _back;
    private bool _closingFromCore;

    private NativeContentDialog(CodeBrixActivity activity, ContentDialog dialog)
        : base(activity, dialog)
    {
        _dialog = dialog;
    }

    /// <summary>The ContentDialog.</summary>
    internal ContentDialog Dialog => _dialog;

    /// <summary>The Material dialog (null once dismissed).</summary>
    internal AAlertDialog Native => _native;

    /// <summary>The slots the dialog's buttons were put in.</summary>
    internal DialogButtonSlots Slots => _slots;

    /// <inheritdoc />
    internal override bool IsShowing => _native?.IsShowing == true;

    /// <summary>
    /// Shows <paramref name="dialog"/> as a Material dialog when it can (text title and content, a live
    /// activity, native dialogs switched on); returns null otherwise (Core presents it).
    /// </summary>
    internal static NativeContentDialog TryShow(ContentDialog dialog, ILogger log)
    {
        if (!OverlayPresentation.NativeContentDialogs || dialog == null)
        {
            return null;
        }

        if (!DialogText.TryGetText(dialog.Title, dialog.TitleTemplate, out var title)
            || !DialogText.TryGetText(dialog.Content, dialog.ContentTemplate, out var message))
        {
            if (log.IsEnabled(LogLevel.Debug))
            {
                log.LogDebug("ContentDialog with XAML title or content: presented by Core (tier 1).");
            }

            return null;
        }

        if (HandlerContext.For(dialog) is not CodeBrixActivity { IsFinishing: false, IsDestroyed: false } activity)
        {
            return null;
        }

        var overlay = new NativeContentDialog(activity, dialog);
        overlay.Show(title, message);
        return overlay;
    }

    /// <summary>Presses one of the dialog's buttons the way a finger does (the native click path).</summary>
    /// <param name="button">The WinUI button.</param>
    /// <returns>False when the dialog shows no such button or it is disabled.</returns>
    internal bool PerformClick(ContentDialogButton button)
    {
        var native = NativeButton(SlotOf(button));
        if (native is not { Enabled: true })
        {
            return false;
        }

        return native.PerformClick();
    }

    /// <summary>The native button showing a WinUI button (null when the dialog shows none).</summary>
    internal AButton NativeButtonFor(ContentDialogButton button) => NativeButton(SlotOf(button));

    /// <summary>Handles the Android back button / gesture: WinUI's Escape (the close button's path).</summary>
    internal void OnBack() => _dialog.RaiseButtonFromPlatform(ContentDialogButton.None);

    /// <inheritdoc />
    internal override void CloseFromCore()
    {
        _closingFromCore = true;
        if (_native is { IsShowing: true } native)
        {
            native.Dismiss();
        }
        else
        {
            Forget();
        }
    }

    private void Show(string title, string message)
    {
        _slots = DialogButtonSlots.For(_dialog.PrimaryButtonText, _dialog.SecondaryButtonText, _dialog.CloseButtonText);

        var builder = new AMaterialAlertDialogBuilder(Activity);
        if (!string.IsNullOrEmpty(title))
        {
            builder.SetTitle(title);
        }

        if (!string.IsNullOrEmpty(message))
        {
            builder.SetMessage(message);
        }

        if (_slots.Positive != ContentDialogButton.None)
        {
            builder.SetPositiveButton(TextOf(_slots.Positive), (global::Android.Content.IDialogInterfaceOnClickListener)null);
        }

        if (_slots.Negative != ContentDialogButton.None)
        {
            builder.SetNegativeButton(TextOf(_slots.Negative), (global::Android.Content.IDialogInterfaceOnClickListener)null);
        }

        if (_slots.Neutral != ContentDialogButton.None)
        {
            builder.SetNeutralButton(TextOf(_slots.Neutral), (global::Android.Content.IDialogInterfaceOnClickListener)null);
        }

        var native = builder.Create();
        _native = native;
        native.SetCancelable(false);
        native.SetCanceledOnTouchOutside(false);
        native.ShowEvent += (_, _) => _dialog.RaiseOpenedFromPlatform();
        native.DismissEvent += (_, _) => OnNativeDismissed();

        _back = new BackCallback(this);
        native.OnBackPressedDispatcher.AddCallback(_back);

        NativeOverlays.Add(this);
        native.Show();

        // The app's re-keyed ContentDialog colours before the first frame (no Material-default flash); the
        // window-focus observer of the presentation policy stays as the fallback.
        Policy.ContentDialogPolicy.Recolor(this);

        // Replace each button's own click handling (which dismisses the dialog) with Core's path.
        WireButton(ADialogButtonType.Positive, _slots.Positive);
        WireButton(ADialogButtonType.Negative, _slots.Negative);
        WireButton(ADialogButtonType.Neutral, _slots.Neutral);
        UpdateButtonsEnabled();

        Watch(ContentDialog.IsPrimaryButtonEnabledProperty, UpdateButtonsEnabled);
        Watch(ContentDialog.IsSecondaryButtonEnabledProperty, UpdateButtonsEnabled);
        Watch(ContentDialog.TitleProperty, UpdateTexts);
        Watch(ContentDialog.ContentProperty, UpdateTexts);
    }

    private void WireButton(ADialogButtonType slot, ContentDialogButton button)
    {
        if (button == ContentDialogButton.None || _native?.GetButton((int)slot) is not { } native)
        {
            return;
        }

        native.Click += (_, _) => _dialog.RaiseButtonFromPlatform(button);
    }

    private void UpdateButtonsEnabled()
    {
        SetEnabled(ContentDialogButton.Primary, _dialog.IsPrimaryButtonEnabled);
        SetEnabled(ContentDialogButton.Secondary, _dialog.IsSecondaryButtonEnabled);
    }

    private void SetEnabled(ContentDialogButton button, bool enabled)
    {
        if (NativeButton(SlotOf(button)) is { } native)
        {
            native.Enabled = enabled;
        }
    }

    private void UpdateTexts()
    {
        if (_native == null)
        {
            return;
        }

        if (DialogText.TryGetText(_dialog.Title, _dialog.TitleTemplate, out var title))
        {
            _native.SetTitle(title);
        }

        if (DialogText.TryGetText(_dialog.Content, _dialog.ContentTemplate, out var message))
        {
            _native.SetMessage(message);
        }
    }

    private void Watch(DependencyProperty property, System.Action update)
    {
        var token = _dialog.RegisterPropertyChangedCallback(property, (_, _) => update());
        _callbacks.Add((property, token));
    }

    private void OnNativeDismissed()
    {
        var fromCore = _closingFromCore;
        Forget();
        if (!fromCore)
        {
            // Dismissed by Android (the activity went away): complete the ShowAsync with None as WinUI's Hide().
            _dialog.Hide();
        }
    }

    private void Forget()
    {
        foreach (var (property, token) in _callbacks)
        {
            _dialog.UnregisterPropertyChangedCallback(property, token);
        }

        _callbacks.Clear();
        _back?.Remove();
        _back = null;
        _native = null;
        NativeOverlays.Remove(this);
    }

    private ADialogButtonType? SlotOf(ContentDialogButton button)
    {
        if (button == ContentDialogButton.None)
        {
            return null;
        }

        if (_slots.Positive == button)
        {
            return ADialogButtonType.Positive;
        }

        if (_slots.Negative == button)
        {
            return ADialogButtonType.Negative;
        }

        if (_slots.Neutral == button)
        {
            return ADialogButtonType.Neutral;
        }

        return null;
    }

    private AButton NativeButton(ADialogButtonType? slot) => slot is { } s ? _native?.GetButton((int)s) : null;

    private string TextOf(ContentDialogButton button) => button switch
    {
        ContentDialogButton.Primary => _dialog.PrimaryButtonText,
        ContentDialogButton.Secondary => _dialog.SecondaryButtonText,
        ContentDialogButton.Close => _dialog.CloseButtonText,
        _ => string.Empty,
    };

    private sealed class BackCallback : AOnBackPressedCallback
    {
        private readonly NativeContentDialog _owner;

        internal BackCallback(NativeContentDialog owner)
            : base(true)
        {
            _owner = owner;
        }

        public override void HandleOnBackPressed() => _owner.OnBack();
    }
}
#endif

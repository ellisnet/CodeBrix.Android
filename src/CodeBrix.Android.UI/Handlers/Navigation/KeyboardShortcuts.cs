using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Overlay;
using AKeyboardShortcutGroup = global::Android.Views.KeyboardShortcutGroup;
using AKeyboardShortcutInfo = global::Android.Views.KeyboardShortcutInfo;
using AKeycode = global::Android.Views.Keycode;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The app's KeyboardAccelerators in Android's keyboard shortcuts helper (Meta + /, plan 2.15): one group titled with
/// the window title, one entry per labelled accelerator (<see cref="KeyboardShortcutCollector"/>). The accelerators
/// themselves already work - key events reach Core first (the activity's input hook) and Core matches them.
/// </summary>
/// <remarks>CodeBrixActivity.OnProvideKeyboardShortcuts calls <see cref="Provide"/> (a coordinator change, see the AP4 report).</remarks>
internal static class KeyboardShortcuts
{
    /// <summary>Adds the app's shortcut group to <paramref name="data"/>.</summary>
    internal static void Provide(CodeBrixActivity activity, IList<AKeyboardShortcutGroup> data)
    {
        if (activity?.XamlWindow?.Content is not { } content || data == null)
        {
            return;
        }

        var group = new AKeyboardShortcutGroup(activity.Title ?? string.Empty);
        foreach (var shortcut in KeyboardShortcutCollector.Collect(content))
        {
            var keyCode = VirtualKeyHelper.ToKeyCode(shortcut.Key);
            if (keyCode == AKeycode.Unknown)
            {
                continue;
            }

            group.AddItem(new AKeyboardShortcutInfo(new global::Java.Lang.String(shortcut.Label), keyCode, VirtualKeyHelper.ToMetaState(shortcut.Modifiers)));
        }

        if (group.Items.Count > 0)
        {
            data.Add(group);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using ASystemClock = Android.OS.SystemClock;
using ATextView = Android.Widget.TextView;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using AViewStates = Android.Views.ViewStates;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP10-B: the steps of the Android-only group AndroidElements10B (every remaining Platform element, second lane:
/// status and feedback, buttons and icons, text, surfaces / legacy / popovers / maps, and the rest). Elements are
/// built by sample name (<see cref="Samples10B"/>). The generic steps of AndroidElements10A (Core properties, Core
/// trees, NotImplemented markers, real-finger taps on native views) are shared.
/// </summary>
[Binding]
public sealed class Elements10BSteps
{
    private static readonly Dictionary<string, int> Tallies = new(StringComparer.Ordinal);

    /// <summary>Forgets the tallies after every scenario.</summary>
    [AfterScenario]
    public static void Forget_the_AP10B_state() => Tallies.Clear();

    // ------------------------------------------------------------------ building

    /// <summary>Shows one of the AP10-B samples, registered by name (its parts too, by their own names).</summary>
    [Given("the application shows the AP10B sample {string} named {string}")]
    public async Task Given_the_sample(string sample, string name)
    {
        FrameworkElement element = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            Samples10B.EnsureIconFile();
            element = Samples10B.Create(sample, name);
            element.Name = name;
            if (element.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty) == DependencyProperty.UnsetValue)
            {
                element.HorizontalAlignment = HorizontalAlignment.Left;
            }

            element.VerticalAlignment = VerticalAlignment.Top;
            ElementRegistry.Register(name, element);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    /// <summary>Tallies an event of an element (for the AP10-B elements).</summary>
    [Given("the {word} events of {string} are tallied")]
    public async Task Given_the_events_are_tallied(string eventName, string name)
    {
        Tallies[name + "." + eventName] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            void Tally() => Tallies[name + "." + eventName] = Tallies.GetValueOrDefault(name + "." + eventName) + 1;
            switch (element, eventName)
            {
                case (Microsoft.UI.Xaml.Controls.Primitives.ButtonBase button, "Click"):
                    button.Click += (_, _) => Tally();
                    break;
                case (SplitButton split, "Click"):
                    split.Click += (_, _) => Tally();
                    break;
                case (ToggleSplitButton toggle, "IsCheckedChanged"):
                    toggle.IsCheckedChanged += (_, _) => Tally();
                    break;
                case (InfoBar bar, "CloseButtonClick"):
                    bar.CloseButtonClick += (_, _) => Tally();
                    break;
                case (InfoBar bar, "Closed"):
                    bar.Closed += (_, _) => Tally();
                    break;
                case (RatingControl rating, "ValueChanged"):
                    rating.ValueChanged += (_, _) => Tally();
                    break;
                case (RefreshContainer refresh, "RefreshRequested"):
                    refresh.RefreshRequested += (sender, args) =>
                    {
                        Tally();
                        var deferral = args.GetDeferral();
                        CompleteLater(deferral);
                    };
                    break;
                case (Microsoft.UI.Xaml.Controls.Primitives.ColorSpectrum spectrum, "ColorChanged"):
                    spectrum.ColorChanged += (_, _) => Tally();
                    break;
                case (Microsoft.UI.Xaml.Controls.Primitives.RangeBase range, "ValueChanged"):
                    range.ValueChanged += (_, _) => Tally();
                    break;
                case (TeachingTip tip, "Closed"):
                    tip.Closed += (_, _) => Tally();
                    break;
                default:
                    throw new InvalidOperationException($"no tally for {eventName} of {element.GetType().Name}");
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Tallies the pointer and manipulation events a named template part of an element receives (diagnostics).</summary>
    [Given("the input of the part {string} of {string} is tallied")]
    public async Task Given_the_input_of_the_part_is_tallied(string part, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var element = FindNamedCore(ElementRegistry.Resolve(name), part)!;
            element.PointerPressed += (_, _) => Tallies[name + ".PointerPressed"] = Tallies.GetValueOrDefault(name + ".PointerPressed") + 1;
            element.ManipulationStarted += (_, _) => Tallies[name + ".ManipulationStarted"] = Tallies.GetValueOrDefault(name + ".ManipulationStarted") + 1;
            element.ManipulationDelta += (_, e) =>
            {
                Tallies[name + ".ManipulationDelta"] = Tallies.GetValueOrDefault(name + ".ManipulationDelta") + 1;
                var x = (int)((element.RenderTransform as TranslateTransform)?.X ?? 0);
                Tallies[name + ".MinX"] = Math.Min(Tallies.GetValueOrDefault(name + ".MinX"), x);
            };
            element.ManipulationCompleted += (_, e) => Tallies[name + ".CompletedX"] = (int)((element.RenderTransform as TranslateTransform)?.X ?? 0);
        }).ConfigureAwait(false);
    }

    /// <summary>Logs every tally (diagnostics).</summary>
    [Then("the tallies are logged")]
    public void Then_the_tallies_are_logged()
    {
        foreach (var pair in Tallies)
        {
            global::Android.Util.Log.Info("AP10B-DUMP", pair.Key + "=" + pair.Value);
        }
    }

    /// <summary>Asserts how many times a tallied event was raised.</summary>
    [Then("{string} raised {word} {int} times in all")]
    public async Task Then_raised_in_all(string name, string eventName, int times)
    {
        await SettleAsync().ConfigureAwait(false);
        Tallies.GetValueOrDefault(name + "." + eventName).Should().Be(times, "{0} must have raised {1} {2} times", name, eventName, times);
    }

    // ------------------------------------------------------------------ status and feedback

    /// <summary>Asserts whether the native widget a template-overlay handler draws over the template is shown.</summary>
    [Then("the native overlay of {string} is {word}")]
    public async Task Then_the_native_overlay_is(string name, string state)
    {
        var shown = state == "shown";
        var ok = await PollAsync(() => HandlerLookup.Of<ITemplateOverlayHandler>(ElementRegistry.Resolve(name))?.IsOverlayVisible == shown).ConfigureAwait(false);
        ok.Should().BeTrue("the native overlay of \"{0}\" must be {1}", name, state);
    }

    /// <summary>Asserts the fill of a native InfoBar card: the colour of a theme brush.</summary>
    [Then("the InfoBar card of {string} is filled with the theme brush {string}")]
    public async Task Then_the_InfoBar_card_is_filled_with(string name, string key)
    {
        var (fill, expected) = await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var card = (InfoBarCardView)HandlerLookup.Of<ITemplateOverlayHandler>(element)!.OverlayView;
            return (card.CardBackgroundColor!.DefaultColor, ThemeResources.FindColor(element, key) ?? 1);
        }).ConfigureAwait(false);
        fill.ToString("X8").Should().Be(expected.ToString("X8"));
    }

    /// <summary>Asserts what a native InfoBadge shows.</summary>
    [Then("the InfoBadge {string} shows a {word} badge {string}")]
    public async Task Then_the_InfoBadge_shows(string name, string kind, string text)
    {
        var (shownKind, shownText) = await OnUIThreadAsync(() =>
        {
            var view = (InfoBadgeView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;
            return (view.Kind.ToString(), view.Text);
        }).ConfigureAwait(false);
        shownKind.Should().Be(kind);
        shownText.Should().Be(text);
    }

    /// <summary>Asserts the size Core gave an element (DIPs, rounded).</summary>
    [Then("the Core size of {string} is {int} by {int}")]
    public async Task Then_the_Core_size_is(string name, int width, int height)
    {
        var size = await OnUIThreadAsync(() => (Math.Round(ElementRegistry.Resolve(name).ActualWidth), Math.Round(ElementRegistry.Resolve(name).ActualHeight))).ConfigureAwait(false);
        size.Should().Be(((double)width, (double)height));
    }

    /// <summary>Asserts that a native PersonPicture shows the bitmap Core opened for its ProfilePicture.</summary>
    [Then("the PersonPicture {string} shows its profile picture")]
    public async Task Then_the_PersonPicture_shows_its_profile_picture(string name)
    {
        var ok = await PollAsync(() => HandlerLookup.Of<PersonPictureHandler>(ElementRegistry.Resolve(name))?.ShowsPicture == true).ConfigureAwait(false);
        ok.Should().BeTrue("the PersonPicture \"{0}\" must show its profile picture", name);
    }

    /// <summary>Asserts that a native PersonPicture shows the group glyph.</summary>
    [Then("the PersonPicture {string} shows the group glyph")]
    public async Task Then_the_PersonPicture_shows_the_group_glyph(string name)
    {
        var text = await OnUIThreadAsync(() => ((PersonPictureView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!).Initials.Text).ConfigureAwait(false);
        text.Should().Be("\uE716");
    }

    /// <summary>Asserts the stars of the native rating bar drawn over a RatingControl.</summary>
    [Then("the rating bar of {string} shows {int} of {int} stars")]
    public async Task Then_the_rating_bar_shows(string name, int rating, int stars)
    {
        await SettleAsync().ConfigureAwait(false);
        var (shown, count) = await OnUIThreadAsync(() =>
        {
            var bar = (AndroidX.AppCompat.Widget.AppCompatRatingBar)HandlerLookup.Of<ITemplateOverlayHandler>(ElementRegistry.Resolve(name))!.OverlayView;
            return (bar.Rating, bar.NumStars);
        }).ConfigureAwait(false);
        shown.Should().Be((float)rating);
        count.Should().Be(stars);
    }

    /// <summary>A real finger taps a star (numbered from 1) of the native rating bar over a RatingControl.</summary>
    [When("a real finger taps star {int} of the rating bar {string}")]
    public async Task When_a_real_finger_taps_star(int star, string name)
    {
        var point = await OnUIThreadAsync(() =>
        {
            var bar = HandlerLookup.Of<ITemplateOverlayHandler>(ElementRegistry.Resolve(name))!.OverlayView;
            var r = WindowRect(bar);
            var stars = ((AndroidX.AppCompat.Widget.AppCompatRatingBar)bar).NumStars;
            return (X: r.Left + (int)Math.Round(r.Width() * (star - 0.5) / stars), Y: r.CenterY());
        }).ConfigureAwait(false);
        await RealTapAsync(point.X, point.Y).ConfigureAwait(false);
    }

    /// <summary>A real finger drags from the element's centre by a distance, in small steps.</summary>
    [When("a real finger drags {string} by {int} pixels {word}")]
    public async Task When_a_real_finger_drags(string name, int distance, string direction)
    {
        var (x, y) = await OnUIThreadAsync(() =>
        {
            var r = WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!);
            return direction == "down" ? (r.CenterX(), r.Top + 20) : (r.CenterX(), r.CenterY());
        }).ConfigureAwait(false);
        var (dx, dy) = direction switch
        {
            "down" => (0, distance),
            "left" => (-distance, 0),
            "right" => (distance, 0),
            _ => throw new ArgumentException(direction),
        };
        const int Steps = 20;
        var down = 0L;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
        }).ConfigureAwait(false);
        for (var i = 1; i <= Steps; i++)
        {
            var step = i;
            await Task.Delay(16).ConfigureAwait(false);
            await TestTargetFixture.RunOnUIThreadAsync(() =>
                Dispatch(down, down + (step * 16), AMotionEventActions.Move, x + (dx * step / Steps), y + (dy * step / Steps))).ConfigureAwait(false);
        }

        await TestTargetFixture.RunOnUIThreadAsync(() =>
            Dispatch(down, down + ((Steps + 1) * 16), AMotionEventActions.Up, x + dx, y + dy)).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    /// <summary>Calls RefreshContainer.RequestRefresh from Core.</summary>
    [When("RequestRefresh is called on {string}")]
    public async Task When_RequestRefresh_is_called(string name)
    {
        await OnUIThreadAsync(() =>
        {
            ((RefreshContainer)ElementRegistry.Resolve(name)).RequestRefresh();
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(100).ConfigureAwait(false);
    }

    /// <summary>Asserts whether the native refresh indicator of a RefreshContainer shows a refresh.</summary>
    [Then("the native refresh indicator of {string} is {word}")]
    public async Task Then_the_native_refresh_indicator_is(string name, string state)
    {
        var refreshing = state == "refreshing";
        var ok = await PollAsync(() => HandlerLookup.Of<RefreshContainerHandler>(ElementRegistry.Resolve(name))?.IsNativeRefreshing == refreshing).ConfigureAwait(false);
        ok.Should().BeTrue("the native refresh indicator of \"{0}\" must be {1}", name, state);
    }

    /// <summary>
    /// Asserts how far Core moved a SwipeControl's content (its TranslateTransform, replayed natively) while the real
    /// finger dragged it (tallied by "the input of the part ... is tallied").
    /// </summary>
    [Then("the content of the SwipeControl {string} followed the finger left by at least {int} DIPs")]
    public void Then_the_SwipeControl_content_followed(string name, int dips) =>
        (-Tallies.GetValueOrDefault(name + ".MinX")).Should().BeGreaterThanOrEqualTo(dips, "the content of \"{0}\" must follow the finger", name);

    // ------------------------------------------------------------------ buttons and icons

    /// <summary>Asserts that a Button-handler Material button shows a trailing chevron (a DropDownButton).</summary>
    [Then("the Material button of {string} has a trailing chevron")]
    public async Task Then_the_Material_button_has_a_trailing_chevron(string name)
    {
        var (icon, gravity) = await OnUIThreadAsync(() =>
        {
            var button = HandlerLookup.Of<ButtonHandler>(ElementRegistry.Resolve(name))!.MaterialButton!;
            return (button.Icon != null, button.IconGravity);
        }).ConfigureAwait(false);
        icon.Should().BeTrue();
        gravity.Should().Be(Google.Android.Material.Button.MaterialButton.IconGravityTextEnd);
    }

    /// <summary>Asserts that the element's flyout is presented as a native Material menu (popup menu or bottom sheet).</summary>
    [Then("the flyout of {string} is a native Material menu")]
    public async Task Then_the_flyout_is_a_native_Material_menu(string name)
    {
        var ok = await PollAsync(() =>
        {
            var flyout = ElementRegistry.Resolve(name) switch
            {
                Button b => b.Flyout,
                SplitButton s => s.Flyout,
                _ => null,
            };
            return flyout != null && CodeBrix.Android.UI.Overlay.PlatformOverlays.Flyouts.Contains(flyout);
        }).ConfigureAwait(false);
        ok.Should().BeTrue("the flyout of \"{0}\" must be shown as a native Material menu", name);
    }

    /// <summary>Hides the element's flyout from Core (clean-up).</summary>
    [When("the flyout of {string} is hidden")]
    public async Task When_the_flyout_is_hidden(string name)
    {
        await OnUIThreadAsync(() =>
        {
            (ElementRegistry.Resolve(name) switch { Button b => b.Flyout, SplitButton s => s.Flyout, _ => null })?.Hide();
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(300).ConfigureAwait(false);
    }

    /// <summary>A real finger taps the leading or trailing half of a native split button.</summary>
    [When("a real finger taps the {word} half of the split button {string}")]
    public async Task When_a_real_finger_taps_the_half(string half, string name)
    {
        var point = await OnUIThreadAsync(() =>
        {
            var view = (SplitButtonView)HandlerLookup.Of<ITemplateOverlayHandler>(ElementRegistry.Resolve(name))!.OverlayView;
            var r = WindowRect(half == "leading" ? view.Leading : view.Trailing);
            return (r.CenterX(), r.CenterY());
        }).ConfigureAwait(false);
        await RealTapAsync(point.Item1, point.Item2).ConfigureAwait(false);
    }

    /// <summary>Asserts that the halves of a native split button lie over the template's two buttons.</summary>
    [Then("the halves of the split button {string} lie over its template buttons")]
    public async Task Then_the_halves_lie_over_the_template_buttons(string name)
    {
        var rects = await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var view = (SplitButtonView)HandlerLookup.Of<ITemplateOverlayHandler>(element)!.OverlayView;
            var primary = NativeViewLocator.ViewOf(FindNamedCore(element, "PrimaryButton")!)!;
            var secondary = NativeViewLocator.ViewOf(FindNamedCore(element, "SecondaryButton")!)!;
            return (WindowRect(view.Leading), WindowRect(primary), WindowRect(view.Trailing), WindowRect(secondary));
        }).ConfigureAwait(false);
        Math.Abs(rects.Item1.Left - rects.Item2.Left).Should().BeLessThanOrEqualTo(2);
        Math.Abs(rects.Item1.Right - rects.Item2.Right).Should().BeLessThanOrEqualTo(2);
        Math.Abs(rects.Item3.Right - rects.Item4.Right).Should().BeLessThanOrEqualTo(2);
    }

    /// <summary>Asserts the checked state of the leading half of a native split button.</summary>
    [Then("the leading half of the split button {string} is {word}")]
    public async Task Then_the_leading_half_is(string name, string state)
    {
        var ok = await PollAsync(() =>
            ((SplitButtonView)HandlerLookup.Of<ITemplateOverlayHandler>(ElementRegistry.Resolve(name))!.OverlayView).Leading.Checked == (state == "checked")).ConfigureAwait(false);
        ok.Should().BeTrue("the leading half of \"{0}\" must be {1}", name, state);
    }

    /// <summary>Asserts the tint of a BitmapIcon's ImageView (a colour name, or "no" for none).</summary>
    [Then("the BitmapIcon {string} is tinted {word}")]
    public async Task Then_the_BitmapIcon_is_tinted(string name, string color)
    {
        var tint = await OnUIThreadAsync(() => HandlerLookup.Of<BitmapIconHandler>(ElementRegistry.Resolve(name))!.AppliedTint).ConfigureAwait(false);
        if (color == "no")
        {
            tint.Should().BeNull();
        }
        else
        {
            var expected = (Windows.UI.Color)typeof(Microsoft.UI.Colors).GetProperty(color)!.GetValue(null)!;
            tint.Should().Be((expected.A << 24) | (expected.R << 16) | (expected.G << 8) | expected.B);
        }
    }

    /// <summary>Asserts that the icon's native view shows a bitmap (an ImageView with a drawable).</summary>
    [Then("{string} shows a native bitmap")]
    public async Task Then_shows_a_native_bitmap(string name)
    {
        var ok = await PollAsync(() => Descendants(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!).OfType<global::Android.Widget.ImageView>().Any(i => i.Drawable != null)).ConfigureAwait(false);
        ok.Should().BeTrue("\"{0}\" must show a bitmap natively", name);
    }

    /// <summary>Asserts that an element's native views show the glyph of a Symbol.</summary>
    [Then("{string} shows the glyph of the symbol {word}")]
    public async Task Then_shows_the_glyph_of_the_symbol(string name, string symbol)
    {
        var glyph = IconDrawables.SymbolGlyph(Enum.Parse<Symbol>(symbol));
        var texts = await OnUIThreadAsync(() => Descendants(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!).OfType<ATextView>().Select(t => t.Text ?? string.Empty).ToList()).ConfigureAwait(false);
        texts.Should().Contain(glyph);
    }

    // ------------------------------------------------------------------ not in the Platform

    /// <summary>Asserts that a public type of the Platform (full name) carries the NotImplemented marker.</summary>
    [Then("the Platform type {word} is marked not implemented")]
    public void Then_the_Platform_type_is_marked_not_implemented(string fullName)
    {
        var type = PlatformType(fullName);
        type.Should().NotBeNull("the Platform must have a public type {0}", fullName);
        type!.GetCustomAttributes(false).Select(a => a.GetType().Name).Should().Contain("NotImplementedAttribute");
    }

    /// <summary>Asserts that every public property of a Platform type (declared on it) carries the NotImplemented marker.</summary>
    [Then("every property of the Platform type {word} is marked not implemented")]
    public void Then_every_property_is_marked_not_implemented(string fullName)
    {
        var type = PlatformType(fullName)!;
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
        properties.Should().NotBeEmpty();
        properties.Where(p => !p.GetCustomAttributes(false).Any(a => a.GetType().Name == "NotImplementedAttribute")).Select(p => p.Name).Should().BeEmpty();
    }

    /// <summary>Asserts that no assembly the Android app ships holds a public type of that simple name.</summary>
    [Then("no shipped assembly has a public type named {word}")]
    public void Then_no_shipped_assembly_has_a_type_named(string simpleName)
    {
        var found = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name!.StartsWith("CodeBrix.", StringComparison.Ordinal))
            .SelectMany(a =>
            {
                try
                {
                    return a.GetExportedTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => t.Name == simpleName)
            .Select(t => t.FullName)
            .ToList();
        found.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ scrolling and colour parts

    /// <summary>Opens a Popup (or NativePopupBase) from Core.</summary>
    [When("the popup {string} is opened")]
    public async Task When_the_popup_is_opened(string name)
    {
        await OnUIThreadAsync(() =>
        {
            switch (ElementRegistry.Resolve(name))
            {
                case NativePopupBase native:
                    native.IsOpen = true;
                    break;
                case Microsoft.UI.Xaml.Controls.Primitives.Popup popup:
                    popup.IsOpen = true;
                    break;
            }

            return true;
        }).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    /// <summary>A real finger drags the native scroller of an element up by a distance (the content scrolls down).</summary>
    [When("a real finger scrolls {string} down by {int} pixels")]
    public async Task When_a_real_finger_scrolls_down(string name, int distance)
    {
        var (x, y) = await OnUIThreadAsync(() =>
        {
            var r = WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!);
            return (r.CenterX(), r.Bottom - 20);
        }).ConfigureAwait(false);
        const int Steps = 20;
        var down = 0L;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
        }).ConfigureAwait(false);
        for (var i = 1; i <= Steps; i++)
        {
            var step = i;
            await Task.Delay(16).ConfigureAwait(false);
            await TestTargetFixture.RunOnUIThreadAsync(() => Dispatch(down, down + (step * 16), AMotionEventActions.Move, x, y - (distance * step / Steps))).ConfigureAwait(false);
        }

        // Hold still before lifting, so the scroll ends where the finger ends (no fling).
        await Task.Delay(150).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Dispatch(down, down + ((Steps + 12) * 16), AMotionEventActions.Move, x, y - distance)).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Dispatch(down, down + ((Steps + 13) * 16), AMotionEventActions.Up, x, y - distance)).ConfigureAwait(false);
        await SettleAsync(600).ConfigureAwait(false);
    }

    /// <summary>Asserts the vertical offset Core holds for a ScrollView (DIPs), at least a value.</summary>
    [Then("the ScrollView {string} is scrolled down by at least {int} DIPs in Core and natively")]
    public async Task Then_the_ScrollView_is_scrolled(string name, int dips)
    {
        var ok = await PollAsync(() =>
        {
            var view = (ScrollView)ElementRegistry.Resolve(name);
            var presenter = FindPresenter(view);
            var native = HandlerLookup.Of<ScrollPresenterHandler>(presenter!)!.NativeOffset.Y;
            return view.VerticalOffset >= dips && Math.Abs(native - (view.VerticalOffset * HandlerLookup.Of<ScrollPresenterHandler>(presenter!)!.Density)) <= 2;
        }).ConfigureAwait(false);
        var state = await OnUIThreadAsync(() =>
        {
            var view = (ScrollView)ElementRegistry.Resolve(name);
            return $"Core {view.VerticalOffset}, native {HandlerLookup.Of<ScrollPresenterHandler>(FindPresenter(view)!)!.NativeOffset.Y}px";
        }).ConfigureAwait(false);
        ok.Should().BeTrue("\"{0}\" must be scrolled by at least {1} DIPs in Core and natively; {2}", name, dips, state);
    }

    /// <summary>Scrolls a ScrollView from Core (ScrollTo, no animation).</summary>
    [When("the ScrollView {string} scrolls to {int} in Core")]
    public async Task When_the_ScrollView_scrolls_to(string name, int offset)
    {
        await OnUIThreadAsync(() =>
        {
            ((ScrollView)ElementRegistry.Resolve(name)).ScrollTo(0, offset, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    /// <summary>Asserts the native scroll position of a ScrollView's presenter (DIPs, rounded).</summary>
    [Then("the native scroller of {string} is at {int}")]
    public async Task Then_the_native_scroller_is_at(string name, int offset)
    {
        var ok = await PollAsync(() =>
        {
            var handler = HandlerLookup.Of<ScrollPresenterHandler>(FindPresenter((ScrollView)ElementRegistry.Resolve(name))!)!;
            return Math.Abs((handler.NativeOffset.Y / handler.Density) - offset) <= 1;
        }).ConfigureAwait(false);
        ok.Should().BeTrue("the native scroller of \"{0}\" must be at {1}", name, offset);
    }

    /// <summary>A real finger taps a point of a native ColorSpectrum (fractions of its size, in percent).</summary>
    [When("a real finger picks {string} at {int} percent across and {int} percent down")]
    public async Task When_a_real_finger_picks(string name, int across, int down)
    {
        var point = await OnUIThreadAsync(() =>
        {
            var r = WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!);
            return (r.Left + (int)Math.Round(r.Width() * across / 100.0), r.Top + (int)Math.Round(r.Height() * down / 100.0));
        }).ConfigureAwait(false);
        await RealTapAsync(point.Item1, point.Item2).ConfigureAwait(false);
    }

    private static Microsoft.UI.Xaml.Controls.Primitives.ScrollPresenter? FindPresenter(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Microsoft.UI.Xaml.Controls.Primitives.ScrollPresenter presenter)
            {
                return presenter;
            }

            if (FindPresenter(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static Type? PlatformType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name!.StartsWith("CodeBrix.Platform", StringComparison.Ordinal))
            .Select(a => a.GetType(fullName))
            .FirstOrDefault(t => t is { IsPublic: true });

    // ------------------------------------------------------------------ diagnostics

    /// <summary>Writes the element's Core tree and native view tree to logcat (tag AP10B-DUMP).</summary>
    [Then("the trees of {string} are logged")]
    public async Task Then_the_trees_are_logged(string name)
    {
        await SettleAsync().ConfigureAwait(false);
        var text = await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var sb = new StringBuilder();
            sb.Append("CORE ").Append(name).Append('\n');
            DumpCore(element, 0, sb);
            sb.Append("NATIVE ").Append(name).Append('\n');
            if (NativeViewLocator.ViewOf(element) is { } view)
            {
                DumpNative(view, 0, sb);
            }

            return sb.ToString();
        }).ConfigureAwait(false);
        foreach (var line in text.Split('\n'))
        {
            global::Android.Util.Log.Info("AP10B-DUMP", line);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static FrameworkElement? FindNamedCore(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name)
            {
                return fe;
            }

            if (FindNamedCore(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static void DumpCore(DependencyObject root, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2).Append(root.GetType().Name);
        if (root is FrameworkElement fe)
        {
            sb.Append(" name=").Append(fe.Name).Append(" size=").Append(fe.ActualWidth.ToString("0.#")).Append('x').Append(fe.ActualHeight.ToString("0.#"))
                .Append(" vis=").Append(fe.Visibility);
        }

        if (root is UIElement ui && HandlerLookup.Of<object>(ui) is { } handler)
        {
            sb.Append(" handler=").Append(handler.GetType().Name);
        }

        sb.Append('\n');
        if (depth > 14)
        {
            return;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            DumpCore(VisualTreeHelper.GetChild(root, i), depth + 1, sb);
        }
    }

    private static void DumpNative(AView view, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2).Append(view.GetType().Name).Append(' ').Append(view.Width).Append('x').Append(view.Height)
            .Append(" vis=").Append(view.Visibility);
        if (view is ATextView text)
        {
            sb.Append(" text=\"").Append(text.Text).Append('"');
        }

        if (!string.IsNullOrEmpty(view.ContentDescription))
        {
            sb.Append(" desc=\"").Append(view.ContentDescription).Append('"');
        }

        sb.Append('\n');
        if (view is AViewGroup group && depth < 16)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (group.GetChildAt(i) is { } child)
                {
                    DumpNative(child, depth + 1, sb);
                }
            }
        }
    }

    private static async void CompleteLater(Windows.Foundation.Deferral deferral)
    {
        await Task.Delay(1500).ConfigureAwait(true);
        deferral.Complete();
    }

    internal static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }

    internal static async Task<bool> PollAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await OnUIThreadAsync(condition).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    internal static async Task SettleAsync(int milliseconds = 200)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(milliseconds).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    internal static async Task RealTapAsync(int x, int y)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
            Dispatch(down, down + 50, AMotionEventActions.Up, x, y);
        }).ConfigureAwait(false);
        await SettleAsync(300).ConfigureAwait(false);
    }

    internal static void Dispatch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var e = AMotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
        e.SetSource(AInputSourceType.Touchscreen);
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    internal static global::Android.Graphics.Rect WindowRect(AView view)
    {
        var location = new int[2];
        view.GetLocationInWindow(location);
        return new global::Android.Graphics.Rect(location[0], location[1], location[0] + view.Width, location[1] + view.Height);
    }

    internal static IEnumerable<AView> Descendants(AView root)
    {
        var pending = new Stack<AView>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var view = pending.Pop();
            yield return view;
            if (view is AViewGroup group)
            {
                for (var i = group.ChildCount - 1; i >= 0; i--)
                {
                    if (group.GetChildAt(i) is { } child)
                    {
                        pending.Push(child);
                    }
                }
            }
        }
    }
}

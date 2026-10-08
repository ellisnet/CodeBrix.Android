#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Reqnroll;
using SilverAssertions;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP9-3 step 0: the steps of the Android-only "Automation names" feature (AndroidFeatures/AndroidAccessibility). They set
/// AutomationProperties.Name / AutomationId on an element (before it enters the tree, or while it is live) and read the
/// native views of the element in process - the content description TalkBack speaks and UI Automator matches, and the tag.
/// </summary>
[Binding]
public sealed class AccessibilitySteps
{
    /// <summary>Shows an element whose automation name is set before it enters the tree.</summary>
    [Given("the application shows a {word} named {string} with the automation name {string} and:")]
    public async Task Given_an_element_with_an_automation_name(string kind, string name, string automationName, DataTable properties)
    {
        var element = await CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory.CreateAsync(kind, name, Rows(properties)).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => AutomationProperties.SetName(element, automationName)).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Shows an element whose automation name and automation id are set before it enters the tree.</summary>
    [Given("the application shows a {word} named {string} with the automation name {string}, the automation id {string} and:")]
    public async Task Given_an_element_with_an_automation_name_and_id(string kind, string name, string automationName, string automationId, DataTable properties)
    {
        var element = await CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory.CreateAsync(kind, name, Rows(properties)).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            AutomationProperties.SetName(element, automationName);
            AutomationProperties.SetAutomationId(element, automationId);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Changes the automation name of a live element.</summary>
    [When("the automation name of {string} is set to {string}")]
    public async Task When_the_automation_name_is_set(string name, string automationName)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => AutomationProperties.SetName(ElementRegistry.Resolve(name), automationName)).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Clears the automation name of a live element (the local value is removed).</summary>
    [When("the automation name of {string} is cleared")]
    public async Task When_the_automation_name_is_cleared(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ElementRegistry.Resolve(name).ClearValue(AutomationProperties.NameProperty)).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Gives a live Button element content (a StackPanel of two TextBlocks), so its handler hosts that content.</summary>
    [When("the Button {string} is given element content")]
    public async Task When_the_Button_is_given_element_content(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "First" });
            panel.Children.Add(new TextBlock { Text = "Second" });
            ((Button)ElementRegistry.Resolve(name)).Content = panel;
        }).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the content description of the first native view of a type inside an element.</summary>
    [Then("the native {word} of {string} has the content description {string}")]
    public async Task Then_the_native_view_has_the_content_description(string widget, string name, string description) =>
        (await ReadAsync(widget, name, v => v.ContentDescription).ConfigureAwait(false)).Should().Be(description);

    /// <summary>Asserts that the first native view of a type inside an element has no content description.</summary>
    [Then("the native {word} of {string} has no content description")]
    public async Task Then_the_native_view_has_no_content_description(string widget, string name) =>
        (await ReadAsync(widget, name, v => v.ContentDescription).ConfigureAwait(false)).Should().BeNull();

    /// <summary>Asserts the tag (as text) of the first native view of a type inside an element.</summary>
    [Then("the native {word} of {string} has the tag {string}")]
    public async Task Then_the_native_view_has_the_tag(string widget, string name, string tag) =>
        (await ReadAsync(widget, name, v => v.Tag?.ToString()).ConfigureAwait(false)).Should().Be(tag);

    /// <summary>Asserts that no native view of an element carries a content description.</summary>
    [Then("no native view of {string} has the content description {string}")]
    public async Task Then_no_native_view_has_the_content_description(string name, string description)
    {
        var found = new List<string>();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            found.AddRange(Views(ElementRegistry.Resolve(name)).Where(v => v.ContentDescription == description).Select(v => v.GetType().Name))).ConfigureAwait(false);
        found.Should().BeEmpty("no native view of \"{0}\" may still say \"{1}\"", name, description);
    }

    /// <summary>Asserts which view the element's handler names as its accessibility view (by type name).</summary>
    [Then("the accessibility view of {string} is a native {word}")]
    public async Task Then_the_accessibility_view_is(string name, string widget)
    {
        var names = new List<string>();
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            if (NativeViewLocator.AccessibilityViewOf(ElementRegistry.Resolve(name)) is { } view)
            {
                names.AddRange(TypeNames(view));
            }
        }).ConfigureAwait(false);
        names.Should().Contain(widget);
    }

    private static IEnumerable<KeyValuePair<string, string>> Rows(DataTable table) =>
        table.Rows.Select(r => new KeyValuePair<string, string>(r[table.Header.First()], r[table.Header.Last()])).ToArray();

    private static async Task<TResult> ReadAsync<TResult>(string widget, string name, Func<AView, TResult> read)
    {
        TResult result = default;
        var found = new List<string>();
        var ok = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var view in Views(ElementRegistry.Resolve(name)))
            {
                var names = TypeNames(view).ToList();
                found.Add(names[0]);
                if (names.Contains(widget, StringComparer.Ordinal))
                {
                    result = read(view);
                    ok = true;
                    return;
                }
            }
        }).ConfigureAwait(false);
        ok.Should().BeTrue("\"{0}\" must have a native {1}; its views are [{2}]", name, widget, string.Join(", ", found));
        return result;
    }

    private static IEnumerable<AView> Views(FrameworkElement element)
    {
        var root = NativeViewLocator.ViewOf(element);
        if (root == null)
        {
            yield break;
        }

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

    private static IEnumerable<string> TypeNames(AView view)
    {
        for (var type = view.GetType(); type != null && type != typeof(Java.Lang.Object); type = type.BaseType)
        {
            yield return type.Name;
        }
    }
}

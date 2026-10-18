using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// The retained-disconnect mode of ElementHandler (ElementHandlerCapabilities.RetainedAcrossLeave, Core hook H16): a
/// retained handler's Disconnect unhooks its view but keeps the element and the view; the next Connect of the same
/// element shows the SAME view again (every mapper runs again); nothing is mapped while it is detached;
/// ReleaseRetained lets the view go for good, after which a Connect creates a new one; RetainedHandlers.ReleaseSubtree
/// releases every retained handler of a dropped list row (no kept native view leaks). Runs in the host-free Core
/// collection (Core elements).
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public sealed class RetainedElementHandlerTests
{
    private static readonly FieldInfo AttachmentField =
        typeof(UIElement).GetField("_handlerAttachment", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("UIElement no longer has a private _handlerAttachment field.");

    public RetainedElementHandlerTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void a_retained_disconnect_unhooks_the_view_but_keeps_the_element_and_the_view()
    {
        //Arrange
        var handler = new RetainedRecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        var border = new Border();
        handler.Connect(border);
        var view = ((IAndroidElementHandler)handler).PlatformView;

        //Act
        handler.Disconnect();

        //Assert
        handler.Log.Should().Contain("disconnect-view");
        handler.IsRetainedDetached.Should().BeTrue();
        handler.Element.Should().BeSameAs(border);
        ((IAndroidElementHandler)handler).PlatformView.Should().BeSameAs(view);
        handler.State.Should().Be(ElementHandlerState.Disconnected);
    }

    [Fact]
    public void the_next_connect_of_the_same_element_shows_the_same_view_and_maps_again()
    {
        //Arrange
        var maps = 0;
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => maps++ };
        var handler = new RetainedRecordingHandler(mapper);
        var border = new Border();
        handler.Connect(border);
        var view = ((IAndroidElementHandler)handler).PlatformView;
        handler.Disconnect();
        handler.Log.Clear();
        maps = 0;

        //Act
        handler.Connect(border);

        //Assert
        ((IAndroidElementHandler)handler).PlatformView.Should().BeSameAs(view);
        handler.CreatedViews.Should().Be(1);
        handler.Log.Should().Equal("connect-view", "connected");
        maps.Should().Be(1);
        handler.IsRetainedDetached.Should().BeFalse();
        handler.State.Should().Be(ElementHandlerState.Connected);
    }

    [Fact]
    public void nothing_is_mapped_or_invoked_while_the_handler_is_detached()
    {
        //Arrange
        var maps = 0;
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => maps++ };
        var handler = new RetainedRecordingHandler(mapper);
        var border = new Border();
        handler.Connect(border);
        handler.Disconnect();
        maps = 0;

        //Act
        border.Opacity = 0.5;
        handler.UpdateValue(UIElement.OpacityProperty);
        var invoked = handler.Invoke("ChangeView", null);

        //Assert
        maps.Should().Be(0);
        invoked.Should().BeFalse();
    }

    [Fact]
    public void a_released_handler_forgets_its_view_and_the_next_connect_creates_a_new_one()
    {
        //Arrange
        var handler = new RetainedRecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        var border = new Border();
        handler.Connect(border);
        var first = ((IAndroidElementHandler)handler).PlatformView;
        handler.Disconnect();

        //Act
        var released = handler.ReleaseRetained();
        var afterRelease = ((IAndroidElementHandler)handler).PlatformView;
        handler.Connect(border);

        //Assert
        released.Should().BeTrue();
        afterRelease.Should().BeNull();
        handler.CreatedViews.Should().Be(2);
        ((IAndroidElementHandler)handler).PlatformView.Should().NotBeSameAs(first);
    }

    [Fact]
    public void connecting_another_element_while_retained_does_not_reuse_the_kept_view()
    {
        //Arrange
        var handler = new RetainedRecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Connect(new Border());
        var first = ((IAndroidElementHandler)handler).PlatformView;
        handler.Disconnect();
        var other = new Border();

        //Act
        handler.Connect(other);

        //Assert
        handler.Element.Should().BeSameAs(other);
        handler.CreatedViews.Should().Be(2);
        ((IAndroidElementHandler)handler).PlatformView.Should().NotBeSameAs(first);
    }

    [Fact]
    public void release_does_nothing_for_a_connected_or_plain_handler()
    {
        //Arrange
        var retained = new RetainedRecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        retained.Connect(new Border());
        var plain = new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        plain.Connect(new Border());
        plain.Disconnect();

        //Act
        var releasedConnected = retained.ReleaseRetained();
        var releasedPlain = plain.ReleaseRetained();

        //Assert
        releasedConnected.Should().BeFalse();
        releasedPlain.Should().BeFalse();
        ((IAndroidElementHandler)retained).PlatformView.Should().NotBeNull();
    }

    [Fact]
    public void releasing_a_dropped_row_lets_every_kept_view_go()
    {
        //Arrange - a row (StackPanel) whose three descendants keep retained handlers, as Core leaves them after the row
        // left the tree (hook H16), and one plain element
        var row = new StackPanel();
        var inner = new Border();
        var text = new TextBlock();
        var image = new Image();
        var plainChild = new Border();
        inner.Child = text;
        row.Children.Add(inner);
        row.Children.Add(image);
        row.Children.Add(plainChild);
        var handlers = new List<RetainedRecordingHandler>();
        foreach (var element in new UIElement[] { row, text, image })
        {
            handlers.Add(KeepRetained(element));
        }

        //Act
        var released = RetainedHandlers.ReleaseSubtree(row);

        //Assert
        released.Should().Be(3);
        foreach (var handler in handlers)
        {
            handler.IsRetainedDetached.Should().BeFalse();
            ((IAndroidElementHandler)handler).PlatformView.Should().BeNull("a dropped row keeps no native view");
            handler.Element.Should().BeNull();
        }

        row.HasRetainedHandler.Should().BeFalse();
        text.HasRetainedHandler.Should().BeFalse();
        image.HasRetainedHandler.Should().BeFalse();
        RetainedHandlers.ReleaseSubtree(row).Should().Be(0);
    }

    [Fact]
    public void created_views_and_released_views_balance_over_many_rows()
    {
        //Arrange - rows recycled several times, then dropped
        var rows = new List<(TextBlock Element, RetainedRecordingHandler Handler)>();
        for (var i = 0; i < 20; i++)
        {
            var element = new TextBlock();
            rows.Add((element, KeepRetained(element)));
        }

        foreach (var (element, handler) in rows)
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                handler.Connect(element);
                handler.Disconnect();
            }

            SetRetainedAttachment(element, handler);
        }

        //Act
        var released = 0;
        foreach (var (element, _) in rows)
        {
            released += RetainedHandlers.ReleaseSubtree(element);
        }

        //Assert
        released.Should().Be(rows.Count);
        foreach (var (_, handler) in rows)
        {
            handler.CreatedViews.Should().Be(1, "a recycled row reuses its one native view");
            ((IAndroidElementHandler)handler).PlatformView.Should().BeNull();
        }
    }

    [Fact]
    public void the_adapter_releases_a_discarded_row_and_the_common_row_handlers_opt_in()
    {
        //Arrange - CoreItemsAdapter and the handlers are Android-only, so their source is the fence
        var root = SourceRoot();
        var adapter = File.ReadAllText(Path.Combine(root, "Platform", "Recycler", "CoreItemsAdapter.cs"));
        var discard = adapter.Substring(adapter.IndexOf("internal void Discard(", StringComparison.Ordinal));
        var release = discard.IndexOf("RetainedHandlers.ReleaseSubtree(item.Element)", StringComparison.Ordinal);
        var drop = discard.IndexOf("_owner.DiscardElement(item.Element, item.ViewType)", StringComparison.Ordinal);

        //Assert
        release.Should().BeGreaterThan(0);
        release.Should().BeLessThan(drop, "the kept views go before Core clears the container");
        File.ReadAllText(Path.Combine(root, "Handlers", "Text", "TextBlockHandler.cs")).Should().Contain("ElementHandlerCapabilities.RetainedAcrossLeave");
        File.ReadAllText(Path.Combine(root, "Handlers", "Media", "ImageHandler.cs")).Should().Contain("ElementHandlerCapabilities.RetainedAcrossLeave");
        File.ReadAllText(Path.Combine(root, "Handlers", "Buttons", "ButtonHandler.cs")).Should().Contain("ElementHandlerCapabilities.RetainedAcrossLeave");
    }

    // Connects a retained handler to the element, disconnects it (retained) and gives the element the detached
    // attachment Core keeps for it (hook H16).
    private static RetainedRecordingHandler KeepRetained(UIElement element)
    {
        var handler = new RetainedRecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Connect(element);
        handler.Disconnect();
        SetRetainedAttachment(element, handler);
        return handler;
    }

    private static void SetRetainedAttachment(UIElement element, IElementHandler handler)
    {
        var attachment = new UIElement.ElementHandlerAttachment(handler, ElementHandlerCapabilities.RetainedAcrossLeave) { IsDetached = true };
        AttachmentField.SetValue(element, attachment);
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return Path.Combine(directory.FullName, "src", "CodeBrix.Android.UI");
    }
}

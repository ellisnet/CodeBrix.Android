using System;
using CodeBrix.Android.UI.WebView.Portable;
using SilverAssertions;
using Xunit;
using SharedWebViewScripts = CodeBrix.Platform.UI.Xaml.Controls.WebViewScripts;

namespace CodeBrix.Android.UI.WebView.Tests.Portable;

/// <summary>
/// The Android web view's scripts, and the fence between them and the framework's shared host-to-page scripts (the pinned
/// CodeBrix.Platform.UI.Core, referenced by this project): AndroidNativeWebView.PostWebMessage evaluates the framework's
/// delivery scripts against the listener surface the Android document-start script installs, so the two must agree on
/// the dispatch function and its behaviour. The delivery scripts' own escaping is the framework's test subject.
/// </summary>
public class WebViewScriptsTests
{
    [Fact]
    public void An_invocation_passes_its_arguments_as_JSON_strings()
    {
        //Arrange
        //Act
        var call = WebViewScripts.Invocation("greet", ["Ada", "a \"quoted\" name"]);

        //Assert
        call.Should().Be("greet(\"Ada\",\"a \\u0022quoted\\u0022 name\")");
    }

    [Fact]
    public void An_invocation_without_arguments_is_an_empty_call()
    {
        //Arrange
        //Act
        var call = WebViewScripts.Invocation("refresh", null);

        //Assert
        call.Should().Be("refresh()");
    }

    [Fact]
    public void The_document_start_script_gives_a_page_both_names_it_posts_through()
    {
        //Arrange
        //Act
        var script = WebViewScripts.DocumentStartScript;

        //Assert
        script.Should().Contain("window.chrome.webview.postMessage=post");
        script.Should().Contain("window.webkit.messageHandlers.codebrixWebView={postMessage:post}");
        script.Should().Contain("window." + WebViewScripts.BridgeObjectName);
        script.Should().Contain("JSON.stringify");
    }

    [Fact]
    public void The_document_start_script_gives_a_page_the_host_to_page_listener_surface()
    {
        //Arrange
        //Act
        var script = WebViewScripts.DocumentStartScript;

        //Assert
        script.Should().Contain("v.addEventListener=function(t,f){if(t==='message'");
        script.Should().Contain("v.removeEventListener=function(t,f){if(t==='message')");
        script.Should().Contain("v.onmessage");
        script.Should().Contain("Object.defineProperty(v,'" + WebViewScripts.DispatchFunctionName + "'");
        script.Should().Contain("data:d");
    }

    [Fact]
    public void The_dispatch_function_is_the_one_the_frameworks_delivery_scripts_call()
    {
        //Arrange
        //Act
        var delivery = SharedWebViewScripts.StringMessageDelivery("x");

        //Assert
        WebViewScripts.DispatchFunctionName.Should().Be(SharedWebViewScripts.DispatchFunctionName);
        delivery.Should().Contain("return v." + WebViewScripts.DispatchFunctionName + "(");
        delivery.Should().Contain("typeof v." + WebViewScripts.DispatchFunctionName + "!=='function'){return " + SharedWebViewScripts.NoChannelResult + ";}");
    }

    [Fact]
    public void The_listener_surface_is_the_frameworks_message_listener_script_verbatim()
    {
        //Arrange: the framework's script is "(function(){<guard>var ls=[];<surface>})();"; the Android document-start
        //script carries <surface> after it has created window.chrome.webview and its postMessage.
        const string start = "var ls=[];";
        const string end = "})();";
        var shared = SharedWebViewScripts.MessageListenerScript;
        var from = shared.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, "the framework's listener script keeps its listeners in 'ls'");
        shared.Should().EndWith(end);

        //Act
        var surface = shared.Substring(from + start.Length, shared.Length - from - start.Length - end.Length);

        //Assert
        surface.Should().StartWith("v.addEventListener=");
        surface.Should().EndWith("return n;}});");
        WebViewScripts.DocumentStartScript.Should().Contain("ls=[];" + surface,
            "the Android document-start script installs the framework's listener surface verbatim (a change there is a change here)");
    }
}

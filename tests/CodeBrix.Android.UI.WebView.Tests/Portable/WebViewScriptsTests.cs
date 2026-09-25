using CodeBrix.Android.UI.WebView.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebView.Tests.Portable;

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
}
